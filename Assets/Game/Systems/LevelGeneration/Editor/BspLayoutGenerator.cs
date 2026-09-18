using System.Collections.Generic;
using Game.Systems.LevelSystem.Runtime;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Topology-only generator (SCARAB-004): BSP places grid-based room footprints
    // (open spaces merge leaves into irregular rectilinear shapes), then a
    // spanning tree plus a small number of injected loop edges gives
    // metroidvania-style backtracking. Room Shell/piece assembly is a separate
    // concern (RoomShellAssembler).
    public static class BspLayoutGenerator
    {
        private static readonly Vector2Int[] Orthogonal4 =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        private struct AdjacencyEdge
        {
            public string RoomA;
            public string RoomB;
            public Vector2Int GapCell;
            public List<Vector2Int> GapCells;
        }

        public static Layout Generate(GenerationParameters parameters)
        {
            parameters.RoomCount = Mathf.Max(1, parameters.RoomCount);
            parameters.MinLeafSize = Mathf.Max(1, parameters.MinLeafSize);

            var layout = new Layout { Seed = parameters.Seed, Parameters = parameters };

            var placementRandom = DeterministicRandom.ForStage(parameters.Seed, "placement");
            var connectivityRandom = DeterministicRandom.ForStage(parameters.Seed, "connectivity");
            var roleRandom = DeterministicRandom.ForStage(parameters.Seed, "roles");
            var pocketRandom = DeterministicRandom.ForStage(parameters.Seed, "pockets");

            var splitCorridors = new List<LayoutRoomNode>();
            var marginRooms = new List<LayoutRoomNode>();
            var leaves = SplitIntoLeaves(parameters, placementRandom, splitCorridors, marginRooms, DefaultAreaUnitSide);

            var rooms = new List<LayoutRoomNode>();
            for (int i = 0; i < leaves.Count; i++)
            {
                rooms.Add(new LayoutRoomNode
                {
                    RoomId = $"Room_{i:000}",
                    Role = RoomRole.Normal,
                    FootprintCells = new List<Vector2Int>(RectCells(leaves[i]))
                });
            }

            rooms.AddRange(splitCorridors);

            CarveNotches(rooms, parameters, placementRandom);
            MergeOpenSpaces(rooms, parameters, placementRandom);

            // Margin rooms exist purely to claim cells so RoomShellAssembler's
            // wall-halo has something to close a corridor's flank against —
            // they are not a gameplay room (no RoomDefinition/RoomController,
            // no doorway, no place in the connectivity graph; see
            // LevelBaker.BakeRoomShell). Role.Margin already keeps them out of
            // CarveNotches/MergeOpenSpaces/AssignPuzzleRooms (all Normal-only),
            // so they can be added right alongside splitCorridors.
            rooms.AddRange(marginRooms);

            // The connectivity graph (adjacency, spanning tree, loops,
            // entry/exit) only ever considers "real" rooms — a margin room has
            // no reason to need a door or to be reachable as its own
            // destination, and including it here would either hand it an
            // unwanted doorway or, worse, read as a disconnected component and
            // get bridged to by a spurious fallback corridor.
            var graphRooms = rooms.FindAll(r => r.Role != RoomRole.Margin);
            var edges = FindAdjacentPairs(graphRooms);

            // Start past whatever the BSP-split corridors already claimed so a
            // disconnection-fallback corridor never collides with one of those.
            int corridorCounter = splitCorridors.Count;
            int graphRoomCountBeforeConnect = graphRooms.Count;
            EnsureConnected(graphRooms, edges, connectivityRandom, ref corridorCounter);

            // EnsureConnected may have appended new disconnection-fallback
            // corridors directly to graphRooms — fold those into the master
            // list too, since layout.Rooms (used for cellOwner/wallCells and
            // baking) must include every room that actually claims cells.
            for (int i = graphRoomCountBeforeConnect; i < graphRooms.Count; i++)
                rooms.Add(graphRooms[i]);

            var tree = BuildSpanningTree(graphRooms, edges, connectivityRandom);
            var loopEdges = PickLoopEdges(edges, tree, parameters.LoopCount, connectivityRandom);

            int doorwayCounter = 0;
            foreach (var edge in tree)
                layout.Connections.Add(ToConnection(edge, ref doorwayCounter, isLoopEdge: false));
            foreach (var edge in loopEdges)
                layout.Connections.Add(ToConnection(edge, ref doorwayCounter, isLoopEdge: true));

            AssignPuzzleRooms(rooms, parameters, roleRandom);
            AssignEntryExit(graphRooms, layout.Connections, parameters.MinEntryExitDistance);

            // Pockets/Spare doors are appended after AssignEntryExit runs so they
            // never influence entry/exit BFS distance — see the "Curated
            // Reachability Survey" design artifact. Pockets are generated in
            // their own detached area (never part of graphRooms/edges above),
            // so no exclusion step is needed here — they were never candidates.
            var pockets = GenerateIsolatedPockets(parameters, rooms, layout.Connections, ref doorwayCounter,
                pocketRandom);
            AssignSpareDoors(rooms, graphRooms, parameters.SpareDoorCount, layout.Connections, ref doorwayCounter,
                pocketRandom);
            layout.Pockets = pockets;

            layout.Rooms = rooms;
            return layout;
        }

        // --- Placement -------------------------------------------------------

        // The "4" here is a tuned per-room area unit (side=4 -> ~16 cells/room on
        // average) — overridable so a detached Isolated Pocket area (SCARAB-014
        // follow-up, 2026-09-06) can be scaled to the main level's own measured
        // room sizes instead of always this fixed constant. Every existing
        // caller keeps passing 4 explicitly, so main-level generation is
        // byte-for-byte unchanged.
        private const int DefaultAreaUnitSide = 4;

        private static List<RectInt> SplitIntoLeaves(GenerationParameters p, DeterministicRandom rng,
            List<LayoutRoomNode> corridorsOut, List<LayoutRoomNode> marginRoomsOut, int areaUnitSide = DefaultAreaUnitSide)
        {
            int splitsRemaining = Mathf.Max(0, p.RoomCount - 1);
            int corridorBudget = Mathf.Clamp(p.CorridorCount, 0, splitsRemaining);

            // A corridor-gap split eats (CorridorMaxLength + 2) - 1 more cells than a
            // plain 1-cell gap along whichever axis it lands on. The base side
            // formula only ever assumed 1-cell gaps, so without this padding a
            // meaningful CorridorCount relative to RoomCount starves the split loop
            // of room and it silently undershoots the requested room count.
            int baseSide = Mathf.CeilToInt(Mathf.Sqrt(p.RoomCount)) * areaUnitSide;
            int corridorPadding = corridorBudget * (p.CorridorMaxLength + 1);
            int side = Mathf.Max(8, baseSide + corridorPadding);
            var leaves = new List<RectInt> { new(0, 0, side, side) };

            int corridorCounter = 0;
            int marginCounter = 0;

            int safety = 0;
            while (leaves.Count < p.RoomCount && safety < p.RoomCount * 20)
            {
                safety++;
                int splitIndex = FindLargestSplittableIndex(leaves, p.MinLeafSize);
                if (splitIndex < 0)
                {
                    Debug.LogWarning($"[LevelGeneration] Could not carve {p.RoomCount} rooms " +
                                      $"at MinLeafSize {p.MinLeafSize}; produced {leaves.Count}.");
                    break;
                }

                var rect = leaves[splitIndex];

                // A corridor gap on a large-enough leaf (especially the very first
                // split, which bisects the *entire* level) severs cross-adjacency
                // between everything on one side and everything on the other —
                // collapsing the whole room graph toward a tree with no cycles
                // left for loops, not just removing one room-pair's adjacency.
                // Only leaves already small relative to the starting area are
                // eligible, so a corridor cut stays localized. Ineligible splits
                // still count against splitsRemaining below, which — via the
                // reservoir math — concentrates the remaining corridorBudget onto
                // later, smaller, eligible splits instead of dropping it.
                bool sizeEligibleForCorridor = rect.width * rect.height <= (side * side) / 2;

                // Reservoir-style pick so ~corridorBudget of the remaining splits
                // end up as corridors, without needing to know split outcomes
                // ahead of time.
                bool wantCorridor = sizeEligibleForCorridor && corridorBudget > 0 && splitsRemaining > 0 &&
                                     rng.NextInt(0, splitsRemaining) < corridorBudget;

                var (a, b, corridor, marginRects) = SplitRect(rect, p, rng, wantCorridor, ref corridorCounter);
                if (corridor != null)
                {
                    corridorsOut.Add(corridor);
                    corridorBudget--;

                    foreach (var marginRect in marginRects)
                    {
                        marginRoomsOut.Add(new LayoutRoomNode
                        {
                            RoomId = $"Margin_{marginCounter:000}",
                            Role = RoomRole.Margin,
                            FootprintCells = new List<Vector2Int>(RectCells(marginRect))
                        });
                        marginCounter++;
                    }
                }

                splitsRemaining--;
                leaves.RemoveAt(splitIndex);
                leaves.Add(a);
                leaves.Add(b);
            }

            return leaves;
        }

        private static int FindLargestSplittableIndex(List<RectInt> leaves, int minLeafSize)
        {
            int bestIndex = -1;
            int bestArea = -1;
            for (int i = 0; i < leaves.Count; i++)
            {
                if (!IsSplittable(leaves[i], minLeafSize)) continue;
                int area = leaves[i].width * leaves[i].height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private static bool IsSplittable(RectInt rect, int minLeafSize)
        {
            int minRequired = minLeafSize * 2 + 1; // two leaves + a 1-cell gap
            return rect.width >= minRequired || rect.height >= minRequired;
        }

        private const int CorridorWidth = 1;

        // A leftover cross-axis strip this thin or thinner is left as bare void
        // instead of becoming its own room — not worth cluttering the layout
        // with a room too small to read as one. Anything thicker MUST be
        // reclaimed (see SplitRect's margin-rect carving below): the gap
        // reserved for a corridor's length only ever fills CorridorWidth cells
        // of its own cross-section, and leaving the rest permanently unclaimed
        // is exactly the corridor wall-thickness bug (SCARAB-MAP-01 follow-up,
        // 2026-09-03) — RoomShellAssembler's wall-halo can't tell "1-cell wall"
        // from "edge of an unbounded void" and produces garbage wall patterns
        // wherever that leftover void happens to sit close to another room.
        private const int MarginRoomMinThickness = 2;

        // A corridor-gap split reserves gapWidth = 1 (wall) + corridorLength +
        // 1 (wall) between the two leaves instead of the usual bare 1-cell gap,
        // and carves a CorridorWidth-wide, corridorLength-long Room out of the
        // middle of that gap — a real Corridor room, not a fallback bridge, wired
        // up by the same 1-cell-gap adjacency detection every other room uses.
        // Falls back to a plain 1-cell gap if the leaf is too small to fit one.
        //
        // The leaves themselves keep the rect's full cross-dimension (e.g. a
        // vertical split's left/right leaves stay rect.height tall), but the
        // corridor only claims CorridorWidth cells of that cross-section — the
        // rest of the reserved gap, above/below (or left/right of) the
        // corridor's own line, is returned as extra "margin" rects for the
        // caller to fold into the layout as ordinary rooms (see
        // MarginRoomMinThickness), so nothing in the gap is ever left
        // permanently unclaimed.
        private static (RectInt, RectInt, LayoutRoomNode, List<RectInt>) SplitRect(RectInt rect, GenerationParameters p,
            DeterministicRandom rng, bool wantCorridor, ref int corridorCounter)
        {
            var marginRects = new List<RectInt>();

            int minLeafSize = p.MinLeafSize;
            bool splitVertical = rect.width >= rect.height;

            int corridorLength = wantCorridor ? rng.NextInt(p.CorridorMinLength, p.CorridorMaxLength + 1) : 0;
            int gapWidth = wantCorridor ? corridorLength + 2 : 1;
            int minRequired = minLeafSize * 2 + gapWidth;

            if (wantCorridor && rect.width < minRequired && rect.height < minRequired)
            {
                wantCorridor = false;
                corridorLength = 0;
                gapWidth = 1;
                minRequired = minLeafSize * 2 + 1;
            }

            if (splitVertical && rect.width < minRequired) splitVertical = false;
            if (!splitVertical && rect.height < minRequired) splitVertical = true;

            // A second, independent size guard, this time on the CROSS axis
            // (perpendicular to the corridor's own travel direction): without
            // it, corridorX/corridorY below could land close enough to rect's
            // own edge that the leftover on that side is nonzero but still
            // under MarginRoomMinThickness — too thin to reclaim as a margin
            // room, yet not zero either. That thin sliver then sits directly
            // against rect's boundary, exactly where an unrelated sibling
            // split (from a completely different branch of the recursion)
            // already has its own independent 1-cell gap — two correct,
            // independently-reserved gaps stacking into the same "wall reads
            // as 2+ cells thick" defect this whole margin mechanism exists to
            // prevent, just via a different path (SCARAB-MAP-01 follow-up,
            // 2026-09-03: found live in Margin_001/Corridor_000/Room_003).
            // Constraining corridorX/corridorY's own range below to always
            // leave a full margin (never a sub-threshold sliver) closes it.
            if (wantCorridor)
            {
                int crossDimension = splitVertical ? rect.height : rect.width;
                int minCrossRequired = CorridorWidth + 2 * MarginRoomMinThickness + 2;
                if (crossDimension < minCrossRequired)
                {
                    wantCorridor = false;
                    corridorLength = 0;
                    gapWidth = 1;
                    minRequired = minLeafSize * 2 + 1;
                    if (splitVertical && rect.width < minRequired) splitVertical = false;
                    if (!splitVertical && rect.height < minRequired) splitVertical = true;
                }
            }

            LayoutRoomNode corridor = null;

            if (splitVertical)
            {
                int minSplit = rect.x + minLeafSize;
                int maxSplit = rect.xMax - minLeafSize - gapWidth;
                int splitX = rng.NextInt(minSplit, maxSplit + 1);

                var left = new RectInt(rect.x, rect.y, splitX - rect.x, rect.height);
                var right = new RectInt(splitX + gapWidth, rect.y, rect.xMax - (splitX + gapWidth), rect.height);

                if (wantCorridor)
                {
                    // Guaranteed non-empty by the crossDimension >= minCrossRequired
                    // check above: both bounds always leave room for a full
                    // MarginRoomMinThickness-thick margin on the far side.
                    int corridorY = rng.NextInt(rect.y + MarginRoomMinThickness + 1,
                        rect.yMax - CorridorWidth - MarginRoomMinThickness);
                    var cells = new List<Vector2Int>();
                    for (int cx = splitX + 1; cx <= splitX + corridorLength; cx++)
                    for (int cy = corridorY; cy < corridorY + CorridorWidth; cy++)
                        cells.Add(new Vector2Int(cx, cy));

                    corridor = new LayoutRoomNode
                        { RoomId = $"Corridor_{corridorCounter:000}", Role = RoomRole.Corridor, FootprintCells = cells };
                    corridorCounter++;

                    // Leftover cross-section (in Y) on either side of the corridor's
                    // row-band, spanning exactly the corridor's own X-range so it
                    // stays flanked by the same 1-cell wall columns the corridor
                    // itself already keeps clear of left/right.
                    int belowHeight = corridorY - 1 - rect.y;
                    if (belowHeight >= MarginRoomMinThickness)
                        marginRects.Add(new RectInt(splitX + 1, rect.y, corridorLength, belowHeight));

                    int aboveStart = corridorY + CorridorWidth + 1;
                    int aboveHeight = rect.yMax - aboveStart;
                    if (aboveHeight >= MarginRoomMinThickness)
                        marginRects.Add(new RectInt(splitX + 1, aboveStart, corridorLength, aboveHeight));
                }

                return (left, right, corridor, marginRects);
            }
            else
            {
                int minSplit = rect.y + minLeafSize;
                int maxSplit = rect.yMax - minLeafSize - gapWidth;
                int splitY = rng.NextInt(minSplit, maxSplit + 1);

                var bottom = new RectInt(rect.x, rect.y, rect.width, splitY - rect.y);
                var top = new RectInt(rect.x, splitY + gapWidth, rect.width, rect.yMax - (splitY + gapWidth));

                if (wantCorridor)
                {
                    // Guaranteed non-empty by the crossDimension >= minCrossRequired
                    // check above: both bounds always leave room for a full
                    // MarginRoomMinThickness-thick margin on the far side.
                    int corridorX = rng.NextInt(rect.x + MarginRoomMinThickness + 1,
                        rect.xMax - CorridorWidth - MarginRoomMinThickness);
                    var cells = new List<Vector2Int>();
                    for (int cy = splitY + 1; cy <= splitY + corridorLength; cy++)
                    for (int cx = corridorX; cx < corridorX + CorridorWidth; cx++)
                        cells.Add(new Vector2Int(cx, cy));

                    corridor = new LayoutRoomNode
                        { RoomId = $"Corridor_{corridorCounter:000}", Role = RoomRole.Corridor, FootprintCells = cells };
                    corridorCounter++;

                    // Leftover cross-section (in X) on either side of the corridor's
                    // column-band, spanning exactly the corridor's own Y-range so it
                    // stays flanked by the same 1-cell wall rows the corridor itself
                    // already keeps clear of bottom/top.
                    int leftWidth = corridorX - 1 - rect.x;
                    if (leftWidth >= MarginRoomMinThickness)
                        marginRects.Add(new RectInt(rect.x, splitY + 1, leftWidth, corridorLength));

                    int rightStart = corridorX + CorridorWidth + 1;
                    int rightWidth = rect.xMax - rightStart;
                    if (rightWidth >= MarginRoomMinThickness)
                        marginRects.Add(new RectInt(rightStart, splitY + 1, rightWidth, corridorLength));
                }

                return (bottom, top, corridor, marginRects);
            }
        }

        // Shape variety independent of open-space merging (SCARAB-010 follow-up):
        // removes a rectangular notch from one corner of a Normal room's
        // footprint. Always leaves a contiguous L-shape (a corner notch out of a
        // rectangle can never disconnect it), so no reachability recheck needed.
        private static void CarveNotches(List<LayoutRoomNode> rooms, GenerationParameters p, DeterministicRandom rng)
        {
            var eligible = rooms.FindAll(r => r.Role == RoomRole.Normal);
            ShuffleRooms(eligible, rng);

            int count = Mathf.Min(p.IrregularRoomCount, eligible.Count);
            for (int i = 0; i < count; i++)
                TryCarveNotch(eligible[i], p.MinLeafSize, rng);
        }

        private static void TryCarveNotch(LayoutRoomNode room, int minLeafSize, DeterministicRandom rng)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var c in room.FootprintCells)
            {
                minX = Mathf.Min(minX, c.x);
                maxX = Mathf.Max(maxX, c.x);
                minY = Mathf.Min(minY, c.y);
                maxY = Mathf.Max(maxY, c.y);
            }

            int width = maxX - minX + 1;
            int height = maxY - minY + 1;

            int notchW = Mathf.Min(Mathf.Max(1, width / 3), width - minLeafSize);
            int notchH = Mathf.Min(Mathf.Max(1, height / 3), height - minLeafSize);
            if (notchW <= 0 || notchH <= 0 || notchW >= width || notchH >= height) return;

            int cornerPick = rng.NextInt(0, 4);
            int notchMinX = cornerPick is 1 or 2 ? maxX - notchW + 1 : minX;
            int notchMinY = cornerPick is 2 or 3 ? maxY - notchH + 1 : minY;

            var toRemove = new HashSet<Vector2Int>();
            for (int x = notchMinX; x < notchMinX + notchW; x++)
            for (int y = notchMinY; y < notchMinY + notchH; y++)
                toRemove.Add(new Vector2Int(x, y));

            room.FootprintCells.RemoveAll(c => toRemove.Contains(c));
        }

        private static IEnumerable<Vector2Int> RectCells(RectInt rect)
        {
            for (int x = rect.x; x < rect.xMax; x++)
            for (int y = rect.y; y < rect.yMax; y++)
                yield return new Vector2Int(x, y);
        }

        private static void MergeOpenSpaces(List<LayoutRoomNode> rooms, GenerationParameters p, DeterministicRandom rng)
        {
            int merged = 0;
            int attempts = 0;
            while (merged < p.OpenSpaceCount && attempts < rooms.Count * 4 && rooms.Count > 1)
            {
                attempts++;
                var normalRooms = rooms.FindAll(r => r.Role == RoomRole.Normal);
                if (normalRooms.Count < 2) break;

                var a = rng.Pick(normalRooms);

                // Only Normal rooms merge into an OpenSpace — a Corridor room's
                // narrow-hallway shape (or another OpenSpace's own irregular one)
                // shouldn't get absorbed into this.
                var candidates = new List<LayoutRoomNode>();
                foreach (var b in normalRooms)
                {
                    if (b == a) continue;
                    if (TryFindGapAdjacentCell(a, b, out _, out _))
                        candidates.Add(b);
                }

                if (candidates.Count == 0) continue;

                var absorbed = rng.Pick(candidates);
                a.FootprintCells.AddRange(absorbed.FootprintCells);
                a.Role = RoomRole.OpenSpace;
                rooms.Remove(absorbed);
                merged++;
            }
        }

        // A gap cell is empty space exactly one step outside room A whose next
        // step lands inside room B — the wall/doorway cell between two rooms.
        private static bool TryFindGapAdjacentCell(LayoutRoomNode a, LayoutRoomNode b, out Vector2Int gapCell,
            out Vector2Int dir)
        {
            var aCells = new HashSet<Vector2Int>(a.FootprintCells);
            var bCells = new HashSet<Vector2Int>(b.FootprintCells);

            foreach (var cell in a.FootprintCells)
            foreach (var d in Orthogonal4)
            {
                var gap = cell + d;
                if (aCells.Contains(gap) || bCells.Contains(gap)) continue;

                var beyond = gap + d;
                if (bCells.Contains(beyond))
                {
                    gapCell = gap;
                    dir = d;
                    return true;
                }
            }

            gapCell = default;
            dir = default;
            return false;
        }

        // Door.prefab's mesh is a true 3-cell-wide module (SCARAB-010 follow-up:
        // measured directly off the prefab, not assumed), unlike the 1-cell wall
        // kit. Where the shared border is long enough, widen the doorway to 3
        // cells centered on the primary gap cell so the door fits without
        // overlapping neighboring wall pieces. Grows both directions along the
        // border alternately and deterministically (no RNG) so layouts stay
        // seed-reproducible; falls back to fewer cells if the border is short.
        private const int DoorwayWidthCells = 3;

        private static List<Vector2Int> WidenDoorway(LayoutRoomNode a, LayoutRoomNode b, Vector2Int gapCell,
            Vector2Int dir)
        {
            var aCells = new HashSet<Vector2Int>(a.FootprintCells);
            var bCells = new HashSet<Vector2Int>(b.FootprintCells);

            bool IsValidBorderCell(Vector2Int candidate) =>
                !aCells.Contains(candidate) && !bCells.Contains(candidate) &&
                bCells.Contains(candidate + dir) && aCells.Contains(candidate - dir);

            return GrowDoorwayCells(gapCell, dir, IsValidBorderCell);
        }

        // Same border-cell as WidenDoorway (SCARAB-014's Spare Door / Pocket door
        // widening), except there is no second room — the far side must be
        // confirmed void instead of confirmed-occupied-by-b, so a Spare Door
        // never silently lines up with a real, merely-unselected neighboring room.
        private static List<Vector2Int> WidenDoorwayAgainstVoid(LayoutRoomNode room,
            HashSet<Vector2Int> allClaimedCells, Vector2Int gapCell, Vector2Int dir)
        {
            var roomCells = new HashSet<Vector2Int>(room.FootprintCells);

            bool IsValidBorderCell(Vector2Int candidate) =>
                !allClaimedCells.Contains(candidate) &&
                roomCells.Contains(candidate - dir) &&
                !allClaimedCells.Contains(candidate + dir);

            return GrowDoorwayCells(gapCell, dir, IsValidBorderCell);
        }

        // Shared growth loop behind WidenDoorway/WidenDoorwayAgainstVoid — grows
        // both directions along the shared border alternately and
        // deterministically (no RNG) so layouts stay seed-reproducible; falls
        // back to fewer cells if the border is short. The two callers differ
        // only in what counts as a valid border cell.
        private static List<Vector2Int> GrowDoorwayCells(Vector2Int gapCell, Vector2Int dir,
            System.Func<Vector2Int, bool> isValidBorderCell)
        {
            var perp = new Vector2Int(-dir.y, dir.x);
            var cells = new List<Vector2Int> { gapCell };
            int posOffset = 1, negOffset = 1;
            bool grewPos = true, grewNeg = true;

            while (cells.Count < DoorwayWidthCells && (grewPos || grewNeg))
            {
                grewPos = false;
                if (cells.Count < DoorwayWidthCells)
                {
                    var posCandidate = gapCell + perp * posOffset;
                    if (isValidBorderCell(posCandidate))
                    {
                        cells.Add(posCandidate);
                        posOffset++;
                        grewPos = true;
                    }
                }

                grewNeg = false;
                if (cells.Count < DoorwayWidthCells)
                {
                    var negCandidate = gapCell - perp * negOffset;
                    if (isValidBorderCell(negCandidate))
                    {
                        cells.Add(negCandidate);
                        negOffset++;
                        grewNeg = true;
                    }
                }
            }

            return cells;
        }

        // SCARAB-014: a free wall-perimeter cell on `room` with nothing behind
        // it at all — no room claims the gap cell, and none claims one further
        // step out either (so a Spare Door never silently lines up with a real
        // neighboring room that just wasn't picked for the connectivity graph).
        // Generation-time only: cannot reuse RoomShellAssembler's wall-cell
        // detection, which runs at bake time against GameObjects/rule
        // resolution, not against plain LayoutRoomNode.FootprintCells.
        private static bool TryFindFreeWallCell(LayoutRoomNode room, HashSet<Vector2Int> allClaimedCells,
            HashSet<Vector2Int> alreadyUsedDoorCells, out Vector2Int gapCell, out Vector2Int dir)
        {
            foreach (var cell in room.FootprintCells)
            foreach (var d in Orthogonal4)
            {
                var gap = cell + d;
                if (allClaimedCells.Contains(gap)) continue;
                if (alreadyUsedDoorCells.Contains(gap)) continue;

                var beyond = gap + d;
                if (allClaimedCells.Contains(beyond)) continue;

                gapCell = gap;
                dir = d;
                return true;
            }

            gapCell = default;
            dir = default;
            return false;
        }

        private static bool TryFindDoorway(LayoutRoomNode a, LayoutRoomNode b, out Vector2Int gapCell,
            out List<Vector2Int> doorCells)
        {
            if (!TryFindGapAdjacentCell(a, b, out gapCell, out var dir))
            {
                doorCells = null;
                return false;
            }

            doorCells = WidenDoorway(a, b, gapCell, dir);
            return true;
        }

        // --- Connectivity ------------------------------------------------------

        private static List<AdjacencyEdge> FindAdjacentPairs(List<LayoutRoomNode> rooms)
        {
            var edges = new List<AdjacencyEdge>();
            for (int i = 0; i < rooms.Count; i++)
            for (int j = i + 1; j < rooms.Count; j++)
            {
                if (TryFindDoorway(rooms[i], rooms[j], out var gapCell, out var gapCells))
                    edges.Add(new AdjacencyEdge
                        { RoomA = rooms[i].RoomId, RoomB = rooms[j].RoomId, GapCell = gapCell, GapCells = gapCells });
            }

            return edges;
        }

        // BSP splitting normally leaves every room reachable via its own 1-cell
        // gap boundaries; this only does real work when an open-space merge
        // consumes the leaf that would otherwise have bridged two areas.
        private static void EnsureConnected(List<LayoutRoomNode> rooms, List<AdjacencyEdge> edges,
            DeterministicRandom rng, ref int corridorCounter)
        {
            int safety = 0;
            while (safety < rooms.Count + 2)
            {
                safety++;

                var uf = new UnionFind();
                foreach (var r in rooms) uf.MakeSet(r.RoomId);
                foreach (var e in edges) uf.Union(e.RoomA, e.RoomB);

                var components = GroupByComponent(rooms, uf);
                if (components.Count <= 1) return;

                LayoutRoomNode bestA = null, bestB = null;
                Vector2Int bestCellA = default, bestCellB = default;
                int bestDist = int.MaxValue;

                foreach (var a in components[0])
                for (int c = 1; c < components.Count; c++)
                foreach (var b in components[c])
                {
                    int dist = ClosestCellDistance(a, b, out var cellA, out var cellB);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestA = a;
                        bestB = b;
                        bestCellA = cellA;
                        bestCellB = cellB;
                    }
                }

                if (bestA == null || bestB == null) return;

                var corridor = BuildCorridor(bestCellA, bestCellB, rooms, corridorCounter);
                if (corridor == null)
                {
                    // Couldn't carve a clean path — connect directly rather than
                    // leaving part of the Level unreachable.
                    edges.Add(new AdjacencyEdge { RoomA = bestA.RoomId, RoomB = bestB.RoomId, GapCell = bestCellA });
                    continue;
                }

                corridorCounter++;
                rooms.Add(corridor);

                if (TryFindDoorway(corridor, bestA, out var gapToA, out var gapCellsToA))
                    edges.Add(new AdjacencyEdge
                        { RoomA = corridor.RoomId, RoomB = bestA.RoomId, GapCell = gapToA, GapCells = gapCellsToA });
                if (TryFindDoorway(corridor, bestB, out var gapToB, out var gapCellsToB))
                    edges.Add(new AdjacencyEdge
                        { RoomA = corridor.RoomId, RoomB = bestB.RoomId, GapCell = gapToB, GapCells = gapCellsToB });
            }
        }

        private static LayoutRoomNode BuildCorridor(Vector2Int cellA, Vector2Int cellB,
            List<LayoutRoomNode> existingRooms, int counter)
        {
            var occupied = new HashSet<Vector2Int>();
            foreach (var room in existingRooms)
            foreach (var cell in room.FootprintCells)
                occupied.Add(cell);

            foreach (var horizontalFirst in new[] { true, false })
            {
                var path = BuildLPath(cellA, cellB, horizontalFirst);
                var interior = new List<Vector2Int>();
                bool blocked = false;

                foreach (var cell in path)
                {
                    if (cell == cellA || cell == cellB) continue;
                    if (occupied.Contains(cell))
                    {
                        blocked = true;
                        break;
                    }

                    interior.Add(cell);
                }

                if (blocked || interior.Count == 0) continue;

                return new LayoutRoomNode
                {
                    RoomId = $"Corridor_{counter:000}",
                    Role = RoomRole.Corridor,
                    FootprintCells = interior
                };
            }

            return null;
        }

        private static List<Vector2Int> BuildLPath(Vector2Int a, Vector2Int b, bool horizontalFirst)
        {
            var corner = horizontalFirst ? new Vector2Int(b.x, a.y) : new Vector2Int(a.x, b.y);

            var path = new List<Vector2Int> { a };
            AppendLine(path, a, corner);
            if (corner != a) path.Add(corner);
            AppendLine(path, corner, b);
            path.Add(b);
            return path;
        }

        private static void AppendLine(List<Vector2Int> path, Vector2Int from, Vector2Int to)
        {
            var dir = new Vector2Int(System.Math.Sign(to.x - from.x), System.Math.Sign(to.y - from.y));
            var current = from;
            while (current != to)
            {
                current += dir;
                if (current != to) path.Add(current);
            }
        }

        private static List<List<LayoutRoomNode>> GroupByComponent(List<LayoutRoomNode> rooms, UnionFind uf)
        {
            var groups = new Dictionary<string, List<LayoutRoomNode>>();
            foreach (var room in rooms)
            {
                var root = uf.Find(room.RoomId);
                if (!groups.TryGetValue(root, out var list))
                {
                    list = new List<LayoutRoomNode>();
                    groups[root] = list;
                }

                list.Add(room);
            }

            return new List<List<LayoutRoomNode>>(groups.Values);
        }

        private static int ClosestCellDistance(LayoutRoomNode a, LayoutRoomNode b, out Vector2Int cellA, out Vector2Int cellB)
        {
            int best = int.MaxValue;
            cellA = default;
            cellB = default;

            foreach (var ca in a.FootprintCells)
            foreach (var cb in b.FootprintCells)
            {
                int dist = System.Math.Abs(ca.x - cb.x) + System.Math.Abs(ca.y - cb.y);
                if (dist < best)
                {
                    best = dist;
                    cellA = ca;
                    cellB = cb;
                }
            }

            return best;
        }

        private sealed class UnionFind
        {
            private readonly Dictionary<string, string> _parent = new();

            public void MakeSet(string id) => _parent.TryAdd(id, id);

            public string Find(string id)
            {
                if (_parent[id] != id)
                    _parent[id] = Find(_parent[id]);
                return _parent[id];
            }

            public bool Union(string a, string b)
            {
                var ra = Find(a);
                var rb = Find(b);
                if (ra == rb) return false;
                _parent[ra] = rb;
                return true;
            }
        }

        // --- Spanning tree + loops ----------------------------------------------

        private static List<AdjacencyEdge> BuildSpanningTree(List<LayoutRoomNode> rooms, List<AdjacencyEdge> edges,
            DeterministicRandom rng)
        {
            var shuffled = new List<AdjacencyEdge>(edges);
            ShuffleEdges(shuffled, rng);

            var uf = new UnionFind();
            foreach (var r in rooms) uf.MakeSet(r.RoomId);

            var tree = new List<AdjacencyEdge>();
            foreach (var edge in shuffled)
            {
                if (uf.Union(edge.RoomA, edge.RoomB))
                    tree.Add(edge);
            }

            return tree;
        }

        private static void ShuffleEdges(List<AdjacencyEdge> edges, DeterministicRandom rng)
        {
            for (int i = edges.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (edges[i], edges[j]) = (edges[j], edges[i]);
            }
        }

        private static List<AdjacencyEdge> PickLoopEdges(List<AdjacencyEdge> edges, List<AdjacencyEdge> tree,
            int loopCount, DeterministicRandom rng)
        {
            var treeSet = new HashSet<(string, string)>();
            foreach (var e in tree)
            {
                treeSet.Add((e.RoomA, e.RoomB));
                treeSet.Add((e.RoomB, e.RoomA));
            }

            var candidates = new List<AdjacencyEdge>();
            foreach (var e in edges)
                if (!treeSet.Contains((e.RoomA, e.RoomB)))
                    candidates.Add(e);

            ShuffleEdges(candidates, rng);

            var picked = new List<AdjacencyEdge>();
            for (int i = 0; i < loopCount && i < candidates.Count; i++)
                picked.Add(candidates[i]);
            return picked;
        }

        private static LayoutConnection ToConnection(AdjacencyEdge edge, ref int doorwayCounter, bool isLoopEdge)
        {
            var connection = new LayoutConnection
            {
                FromRoomId = edge.RoomA,
                ToRoomId = edge.RoomB,
                DoorCell = edge.GapCell,
                DoorCells = edge.GapCells ?? new List<Vector2Int> { edge.GapCell },
                DoorwayId = $"Doorway_{doorwayCounter:000}",
                Kind = ConnectionKind.Euclidean,
                IsLoopEdge = isLoopEdge
            };
            doorwayCounter++;
            return connection;
        }

        // --- Roles -------------------------------------------------------------

        private static void AssignPuzzleRooms(List<LayoutRoomNode> rooms, GenerationParameters p, DeterministicRandom rng)
        {
            var eligible = rooms.FindAll(r => r.Role == RoomRole.Normal);
            ShuffleRooms(eligible, rng);

            int count = Mathf.Min(p.PuzzleRoomCount, eligible.Count);
            for (int i = 0; i < count; i++)
                eligible[i].Role = RoomRole.Puzzle;
        }

        private static void ShuffleRooms(List<LayoutRoomNode> rooms, DeterministicRandom rng)
        {
            for (int i = rooms.Count - 1; i > 0; i--)
            {
                int j = rng.NextInt(0, i + 1);
                (rooms[i], rooms[j]) = (rooms[j], rooms[i]);
            }
        }

        // --- Pockets -------------------------------------------------------

        // A gap, in cells, between the main blob and the first Pocket's own
        // detached area, and between consecutive Pockets' own areas — enough
        // that no flat top-down view could read a Pocket as still part of the
        // main blob's footprint (Michael, 2026-09-05 follow-up: a Pocket is a
        // genuine sub-level, spatially apart, not merely missing a door).
        private const int PocketAreaGap = 4;

        // Generates up to pocketCount Isolated Pockets (SCARAB-014): each is its
        // own scaled-down mini-BSP pass (reusing SplitIntoLeaves directly, with
        // CorridorCount=0 and RoomCount set to a random 1-3 target), placed in a
        // detached area to the right of everything already placed — never
        // selected from the main level's own rooms, so it's never spatially
        // adjacent to the main blob at all, let alone connected to it. A BSP
        // split's own leaves are always mutually reachable by construction (see
        // EnsureConnected's comment on the main level), so a Pocket's internal
        // connectivity needs no extra bridging step here either.
        private static List<PocketInfo> GenerateIsolatedPockets(GenerationParameters parameters,
            List<LayoutRoomNode> rooms, List<LayoutConnection> connections, ref int doorwayCounter,
            DeterministicRandom pocketRandom)
        {
            var pockets = new List<PocketInfo>();
            if (parameters.IsolatedPocketCount <= 0) return pockets;

            var allClaimedCells = new HashSet<Vector2Int>();
            foreach (var room in rooms)
            foreach (var cell in room.FootprintCells)
                allClaimedCells.Add(cell);

            var usedDoorCells = new HashSet<Vector2Int>();
            foreach (var c in connections)
            foreach (var cell in c.DoorCells)
                usedDoorCells.Add(cell);

            int nextAreaX = MaxClaimedX(rooms) + PocketAreaGap;

            // A Pocket room is "any type of room... any size" (SCARAB-014
            // grilling), not capped to a small fixed area regardless of how big
            // the main level's own rooms turned out — scale each Pocket's
            // bounding area from the main level's own measured average room size
            // (Margin/Corridor excluded — filler and long-thin, not representative
            // of "room size"), with its own random per-pocket multiplier so
            // different Pockets vary in size the same way main-level rooms do
            // via merges/notches, rather than every Pocket landing on the same
            // handful of sizes. Each individual Pocket is capped to the main
            // blob's own total area (Michael, 2026-09-06: "every pocket by
            // itself should not be bigger [than the main blob], it is a pocket
            // after all") — the combined total across several Pockets is free to
            // exceed that; only any one Pocket outweighing the whole level it's
            // attached to is the thing being prevented here.
            int avgMainRoomArea = ComputeAverageRoomArea(rooms);
            long mainBlobTotalArea = 0;
            foreach (var room in rooms)
                mainBlobTotalArea += room.FootprintCells.Count;

            for (int pocketIndex = 0; pocketIndex < parameters.IsolatedPocketCount; pocketIndex++)
            {
                int targetSize = pocketRandom.NextInt(1, 4); // 1-3 inclusive

                double sizeMultiplier = 0.5 + pocketRandom.NextDouble() * 1.5; // 0.5x-2x
                long desiredTotalArea = (long)(avgMainRoomArea * (float)sizeMultiplier * targetSize);
                long cappedTotalArea = System.Math.Min(desiredTotalArea, mainBlobTotalArea);
                int desiredAreaPerRoom = Mathf.Max(9, Mathf.RoundToInt(cappedTotalArea / (float)targetSize));
                int areaUnitSide = Mathf.Max(DefaultAreaUnitSide, Mathf.CeilToInt(Mathf.Sqrt(desiredAreaPerRoom)));

                // Same size/shape-variety knobs as the main level — a Pocket room
                // is eligible for every generation pass, same as any other room
                // (SCARAB-014 grilling: "isolated rooms can be anything just like
                // all the other rooms"), not just a plain BSP-leaf rectangle.
                // CarveNotches/MergeOpenSpaces already self-clamp against however
                // many rooms actually exist, so passing these through unscaled is
                // safe even for a 1-3 room pocket.
                var pocketParams = new GenerationParameters
                {
                    Seed = parameters.Seed,
                    RoomCount = targetSize,
                    CorridorCount = 0,
                    MinLeafSize = parameters.MinLeafSize,
                    IrregularRoomCount = parameters.IrregularRoomCount,
                    OpenSpaceCount = parameters.OpenSpaceCount
                };
                var leafRandom = DeterministicRandom.ForStage(parameters.Seed, $"pocket-{pocketIndex}-leaves");
                var leaves = SplitIntoLeaves(pocketParams, leafRandom, new List<LayoutRoomNode>(),
                    new List<LayoutRoomNode>(), areaUnitSide);

                int leafMinX = int.MaxValue, leafMaxX = int.MinValue;
                foreach (var leaf in leaves)
                {
                    leafMinX = Mathf.Min(leafMinX, leaf.x);
                    leafMaxX = Mathf.Max(leafMaxX, leaf.xMax);
                }
                var offset = new Vector2Int(nextAreaX - leafMinX, 0);
                nextAreaX += (leafMaxX - leafMinX) + PocketAreaGap;

                var pocketRooms = new List<LayoutRoomNode>();
                for (int i = 0; i < leaves.Count; i++)
                {
                    var cells = new List<Vector2Int>();
                    foreach (var cell in RectCells(leaves[i])) cells.Add(cell + offset);
                    pocketRooms.Add(new LayoutRoomNode
                        { RoomId = $"Pocket_{pocketIndex:000}_Room_{i:000}", Role = RoomRole.Normal, FootprintCells = cells });
                }

                CarveNotches(pocketRooms, pocketParams, leafRandom);
                MergeOpenSpaces(pocketRooms, pocketParams, leafRandom);

                foreach (var room in pocketRooms)
                {
                    rooms.Add(room);
                    foreach (var cell in room.FootprintCells) allClaimedCells.Add(cell);
                }

                string entranceRoomId, exitRoomId;
                if (pocketRooms.Count == 1)
                {
                    entranceRoomId = exitRoomId = pocketRooms[0].RoomId;
                }
                else
                {
                    var pocketEdges = FindAdjacentPairs(pocketRooms);
                    var connectivityRandom =
                        DeterministicRandom.ForStage(parameters.Seed, $"pocket-{pocketIndex}-connectivity");
                    var tree = BuildSpanningTree(pocketRooms, pocketEdges, connectivityRandom);
                    foreach (var edge in tree)
                        connections.Add(ToConnection(edge, ref doorwayCounter, isLoopEdge: false));

                    var adjacency = BuildAdjacencyMap(pocketRooms, connections);
                    (entranceRoomId, exitRoomId) = FindFarthestPair(pocketRooms, adjacency);
                }

                var pocket = new PocketInfo
                    { PocketId = $"Pocket_{pocketIndex:000}", RoomIds = pocketRooms.ConvertAll(r => r.RoomId) };

                var entranceRoom = pocketRooms.Find(r => r.RoomId == entranceRoomId);
                pocket.EntranceDoorwayId = PlacePocketDoor(entranceRoom, allClaimedCells, usedDoorCells, connections,
                    ref doorwayCounter, pocket.PocketId);

                pocket.ExitDoorwayId = exitRoomId == entranceRoomId
                    ? pocket.EntranceDoorwayId
                    : PlacePocketDoor(pocketRooms.Find(r => r.RoomId == exitRoomId), allClaimedCells, usedDoorCells,
                        connections, ref doorwayCounter, pocket.PocketId);

                pockets.Add(pocket);
            }

            return pockets;
        }

        private static int MaxClaimedX(List<LayoutRoomNode> rooms)
        {
            int maxX = 0;
            foreach (var room in rooms)
            foreach (var cell in room.FootprintCells)
                maxX = Mathf.Max(maxX, cell.x);
            return maxX;
        }

        // Margin (pure filler) and Corridor (long-thin by design) rooms are
        // excluded — neither is representative of what "a room's size" means
        // for scaling a Pocket's own area. Falls back to DefaultAreaUnitSide's
        // implicit ~16-cell average (the main level's own baseline before any
        // merges) if there's nothing else to measure from.
        private static int ComputeAverageRoomArea(List<LayoutRoomNode> rooms)
        {
            int count = 0;
            long totalCells = 0;
            foreach (var room in rooms)
            {
                if (room.Role == RoomRole.Margin || room.Role == RoomRole.Corridor) continue;
                count++;
                totalCells += room.FootprintCells.Count;
            }

            return count > 0 ? Mathf.Max(1, (int)(totalCells / count)) : DefaultAreaUnitSide * DefaultAreaUnitSide;
        }

        // Decorative Spare Doors (SCARAB-014) on otherwise-normal, already-
        // reachable rooms — main graph only, never a Pocket room. Purely
        // optional extra material for curation, unrelated to any room's own
        // reachability.
        private static void AssignSpareDoors(List<LayoutRoomNode> allRooms, List<LayoutRoomNode> graphRooms,
            int spareDoorCount, List<LayoutConnection> connections, ref int doorwayCounter, DeterministicRandom rng)
        {
            if (spareDoorCount <= 0) return;

            var allClaimedCells = new HashSet<Vector2Int>();
            foreach (var room in allRooms)
            foreach (var cell in room.FootprintCells)
                allClaimedCells.Add(cell);

            var usedDoorCells = new HashSet<Vector2Int>();
            foreach (var c in connections)
            foreach (var cell in c.DoorCells)
                usedDoorCells.Add(cell);

            var eligible = graphRooms.FindAll(r => r.Role == RoomRole.Normal);
            ShuffleRooms(eligible, rng);

            int placed = 0;
            foreach (var room in eligible)
            {
                if (placed >= spareDoorCount) break;

                if (!TryFindFreeWallCell(room, allClaimedCells, usedDoorCells, out var gapCell, out var dir))
                    continue;

                var doorCells = WidenDoorwayAgainstVoid(room, allClaimedCells, gapCell, dir);
                foreach (var cell in doorCells) usedDoorCells.Add(cell);

                var doorwayId = $"Doorway_{doorwayCounter:000}";
                doorwayCounter++;

                connections.Add(new LayoutConnection
                {
                    FromRoomId = room.RoomId,
                    ToRoomId = null,
                    DoorCell = gapCell,
                    DoorCells = doorCells,
                    DoorwayId = doorwayId,
                    Kind = ConnectionKind.Euclidean,
                    IsSpareDoor = true
                });

                placed++;
            }

            if (placed < spareDoorCount)
                Debug.LogWarning($"[LevelGeneration] Requested {spareDoorCount} Spare Doors but only " +
                                  $"{placed} eligible free wall cells were found.");
        }

        private static string PlacePocketDoor(LayoutRoomNode room, HashSet<Vector2Int> allClaimedCells,
            HashSet<Vector2Int> usedDoorCells, List<LayoutConnection> connections, ref int doorwayCounter,
            string pocketId)
        {
            if (room == null) return null;

            if (!TryFindFreeWallCell(room, allClaimedCells, usedDoorCells, out var gapCell, out var dir))
            {
                Debug.LogWarning($"[LevelGeneration] Pocket {pocketId}: no free wall cell available for a door " +
                                  $"on {room.RoomId} — pocket will ship with one fewer door than intended.");
                return null;
            }

            var doorCells = WidenDoorwayAgainstVoid(room, allClaimedCells, gapCell, dir);
            foreach (var cell in doorCells) usedDoorCells.Add(cell);

            var doorwayId = $"Doorway_{doorwayCounter:000}";
            doorwayCounter++;

            connections.Add(new LayoutConnection
            {
                FromRoomId = room.RoomId,
                ToRoomId = null,
                DoorCell = gapCell,
                DoorCells = doorCells,
                DoorwayId = doorwayId,
                Kind = ConnectionKind.Euclidean,
                IsSpareDoor = false
            });

            return doorwayId;
        }

        private static void AssignEntryExit(List<LayoutRoomNode> rooms, List<LayoutConnection> connections, int minDistance)
        {
            if (rooms.Count == 0) return;

            var adjacency = BuildAdjacencyMap(rooms, connections);
            var (farthestFromThat, farthestFromFirst) = FindFarthestPair(rooms, adjacency);

            var entry = rooms.Find(r => r.RoomId == farthestFromThat);
            var exit = rooms.Find(r => r.RoomId == farthestFromFirst);

            if (entry != null) entry.IsEntry = true;
            if (exit != null && exit != entry) exit.IsExit = true;

            // Double-BFS already picks the maximum achievable spread, so this is a
            // feedback signal, not a constraint the algorithm re-solves for.
            if (entry != null && exit != null && entry != exit)
            {
                int achieved = BfsDistance(entry.RoomId, exit.RoomId, adjacency);
                if (achieved < minDistance)
                    Debug.LogWarning($"[LevelGeneration] Entry/exit distance {achieved} is below the requested " +
                                      $"minimum {minDistance} — this is already the maximum spread this Layout allows.");
            }
        }

        private static int BfsDistance(string start, string target, Dictionary<string, List<string>> adjacency)
        {
            var visited = new HashSet<string> { start };
            var queue = new Queue<(string id, int dist)>();
            queue.Enqueue((start, 0));

            while (queue.Count > 0)
            {
                var (current, dist) = queue.Dequeue();
                if (current == target) return dist;
                foreach (var neighbor in adjacency[current])
                    if (visited.Add(neighbor))
                        queue.Enqueue((neighbor, dist + 1));
            }

            return int.MaxValue;
        }

        private static Dictionary<string, List<string>> BuildAdjacencyMap(List<LayoutRoomNode> rooms,
            List<LayoutConnection> connections)
        {
            var map = new Dictionary<string, List<string>>();
            foreach (var room in rooms) map[room.RoomId] = new List<string>();

            foreach (var c in connections)
            {
                // A Spare/Pocket door (SCARAB-014) has no destination yet — not an
                // edge to traverse, and ContainsKey(null) would throw besides.
                if (c.ToRoomId == null) continue;

                if (map.ContainsKey(c.FromRoomId)) map[c.FromRoomId].Add(c.ToRoomId);
                if (map.ContainsKey(c.ToRoomId)) map[c.ToRoomId].Add(c.FromRoomId);
            }

            return map;
        }

        // The room-graph-diameter endpoint pair: farthest-from-an-arbitrary-start,
        // then farthest-from-that. Shared by AssignEntryExit (whole level) and
        // AssignPocketDoors (a single Pocket's own local rooms) so the BFS logic
        // lives in one place.
        private static (string, string) FindFarthestPair(List<LayoutRoomNode> rooms,
            Dictionary<string, List<string>> adjacency)
        {
            var first = rooms[0].RoomId;
            var farthestFromFirst = BfsFarthest(first, adjacency);
            var farthestFromThat = BfsFarthest(farthestFromFirst, adjacency);
            return (farthestFromThat, farthestFromFirst);
        }

        private static string BfsFarthest(string start, Dictionary<string, List<string>> adjacency)
        {
            var visited = new HashSet<string> { start };
            var queue = new Queue<string>();
            queue.Enqueue(start);
            var last = start;

            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                last = current;
                foreach (var neighbor in adjacency[current])
                {
                    if (visited.Add(neighbor))
                        queue.Enqueue(neighbor);
                }
            }

            return last;
        }
    }
}

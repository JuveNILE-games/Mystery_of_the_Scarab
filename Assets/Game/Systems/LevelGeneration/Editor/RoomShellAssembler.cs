using System;
using System.Collections.Generic;
using Core.Editor.Tools.PrefabBrush.Rules;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Room Shell assembly (SCARAB-005): calls PrefabRuleResolver.Resolve directly
    // with a NeighborSnapshot built from the Layout grid — no BrushEngine, no
    // PrefabBrushTool, no scene-scanning. Doorways are handled as a Scarab-local
    // post-process: the resolver picks a normal wall piece, which then gets
    // swapped for the Door prefab (SCARAB-006).
    public static class RoomShellAssembler
    {
        public const string WallPlacementGroup = "LevelGeneration";

        // Kept distinct from walls' group so a floor rebake's cleanup
        // (LevelBaker.BakeFloors) never touches hand-edited wall pieces, and
        // vice versa — floor is a deliberately separate bake step from
        // wall/Room Shell assembly (Michael, 2026-09-04): walls get baked and
        // hand-cleaned up first, floor gets baked afterward on top of
        // whatever the walls ended up looking like.
        public const string FloorPlacementGroup = "LevelGenerationFloor";

        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0) // N, E, S, W
        };

        private static readonly Vector2Int[] EightWayDirs =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1) // N, NE, E, SE, S, SW, W, NW
        };

        // Run axis for ReconcileRunOutwardDirs — the direction a
        // Straight-shaped piece's own flat face runs along, perpendicular to
        // its thickness axis (thicknessIsZ ? X : Z, same convention
        // DetermineOutwardDir already uses for yRotation).
        private static readonly Vector2Int[] RunAxisX = { new(1, 0), new(-1, 0) };
        private static readonly Vector2Int[] RunAxisZ = { new(0, 1), new(0, -1) };

        // Returns the exact wall cell each placed instance was resolved for —
        // callers that need to know which room a piece belongs to (LevelBaker,
        // for hierarchy grouping) must use this, not reverse-engineer it from
        // the instance's final transform: ShellAlignmentPass deliberately
        // shifts every exterior piece by close to half a cell toward its
        // outward edge (SCARAB-010 follow-up), so recovering the cell from a
        // post-alignment world position rounds to the wrong (void) cell for
        // most exterior walls — this was the actual dominant cause of wall
        // pieces landing ungrouped at the level root, not the smaller 4-way
        // vs 8-way ownership gap (SCARAB-MAP-01 follow-up, 2026-09-03).
        public static Dictionary<GameObject, Vector2Int> BuildShells(Layout layout, ShellAssemblyConfig config,
            Transform parent)
        {
            var cellByInstance = new Dictionary<GameObject, Vector2Int>();

            if (config?.RuleSet == null)
            {
                Debug.LogError("[LevelGeneration] RoomShellAssembler requires a PrefabRuleSet.");
                return cellByInstance;
            }

            var cellOwner = new Dictionary<Vector2Int, string>();
            foreach (var room in layout.Rooms)
            foreach (var cell in room.FootprintCells)
                cellOwner[cell] = room.RoomId;

            // 8-way here regardless of the RuleSet's own Mode: a room's diagonal
            // corner cells (e.g. (0,0) relative to a floor cell at (1,1)) are not
            // orthogonally adjacent to any floor cell, so a 4-way-only pass leaves
            // a gap at every corner. This is wall *detection*, separate from the
            // NeighborSnapshot's own directions used for rule *matching* below.
            //
            // Recorded per-room, not as a flat set (SCARAB-MAP-01 follow-up,
            // 2026-09-04): a void cell one step from Room_004's edge and a void
            // cell one step from an unrelated Corridor/Margin's edge are both
            // "wall cells" in the old flat sense even when they're nowhere near
            // each other's actual boundary run — e.g. the row immediately south
            // of a corridor's own end-cap sits diagonally adjacent to that
            // corridor's corner AND to Room_004's straight edge at the same
            // time, corrupting Room_004's otherwise-clean 2-opposite-tag
            // pattern into a 3-tag one that a loosely-specified Corner rule
            // matches instead of Straight — the corner mesh then doesn't align
            // with Room_004's actual wall and clips into the doorway next to
            // it. Tracking which room(s) each wall cell borders lets
            // BuildSnapshot ask the right question: not "is this neighbor void
            // near something," but "is this neighbor void near the *same*
            // room's boundary I'm on."
            var wallCellRooms = new Dictionary<Vector2Int, HashSet<string>>(); // full 8-way membership
            var directRoomsByCell = new Dictionary<Vector2Int, HashSet<string>>(); // 4-way-only membership
            foreach (var ownedCell in cellOwner.Keys)
            {
                var owningRoom = cellOwner[ownedCell];
                foreach (var dir in EightWayDirs)
                    AddRoomMembership(wallCellRooms, ownedCell + dir, owningRoom, cellOwner);
                foreach (var dir in FourWayDirs)
                    AddRoomMembership(directRoomsByCell, ownedCell + dir, owningRoom, cellOwner);
            }

            var wallCells = wallCellRooms.Keys;

            // A wall cell's actual boundary room — its 4-way-owned neighbor(s) —
            // takes priority over incidental 8-way diagonal touches from
            // unrelated rooms: (4,23) above borders Room_004 directly (4-way
            // N) but also happens to sit diagonally next to Corridor_000's and
            // Margin_000's corners, which have nothing to do with Room_004's
            // wall run. Only cells with no direct 4-way neighbor at all (a
            // true diagonal convex corner, where the room touches only via one
            // diagonal step) fall back to the full 8-way membership.
            var primaryRoomsByCell = new Dictionary<Vector2Int, HashSet<string>>();
            foreach (var cell in wallCells)
                primaryRoomsByCell[cell] = directRoomsByCell.TryGetValue(cell, out var direct) && direct.Count > 0
                    ? direct
                    : wallCellRooms[cell];

            // Door.prefab's mesh is a true 3-cell-wide module (unlike the 1-cell
            // wall kit), so a widened doorway's cells all map to the same
            // connection and must resolve to a single Door instance spanning them.
            var doorwayByCell = new Dictionary<Vector2Int, LayoutConnection>();
            foreach (var connection in layout.Connections)
            foreach (var doorCell in connection.DoorCells)
                doorwayByCell[doorCell] = connection;

            var directions = config.RuleSet.Mode == NeighborMode.FourWay ? FourWayDirs : EightWayDirs;
            var placedDoorways = new HashSet<string>();
            var placedInstances = new List<GameObject>();
            var tJunctionCornerCells = new List<Vector2Int>();
            var outwardDirByInstance = new Dictionary<GameObject, Vector2Int>();
            var doorCellsByInstance = new Dictionary<GameObject, List<Vector2Int>>();
            var straightPrefab = FindRulePrefabByName(config.RuleSet, "Straight");

            foreach (var cell in wallCells)
            {
                // A door always wins, regardless of multi-room-seam classification —
                // still routed through the resolver first, since PlaceDoorway reuses
                // its computed rotation for the door module's own orientation.
                if (doorwayByCell.TryGetValue(cell, out var connection))
                {
                    var doorSnapshot = BuildSnapshot(cell, primaryRoomsByCell, directions, config.RuleSet.RuleTag);
                    var doorResult = PrefabRuleResolver.Resolve(config.RuleSet, doorSnapshot);
                    if (doorResult.Prefab == null) continue;
                    var doorYRotation = doorResult.Matched
                        ? doorResult.YRotation
                        : ResolveFallbackYRotation(doorSnapshot, doorResult.YRotation);

                    if (!placedDoorways.Add(connection.DoorwayId)) continue; // other cell already placed the door
                    var door = PlaceDoorway(connection, doorYRotation, config, parent, cell);
                    if (door != null)
                    {
                        placedInstances.Add(door);
                        cellByInstance[door] = cell;
                        doorCellsByInstance[door] = connection.DoorCells;

                        var doorOutward = DetermineOutwardDir(cell, cellOwner, doorYRotation);
                        if (doorOutward.HasValue) outwardDirByInstance[door] = doorOutward.Value;
                    }
                    continue;
                }

                if (config.MultiRoomSeamPrefab != null &&
                    TryComputeMultiRoomSeamRotation(cell, primaryRoomsByCell, FourWayDirs, out var seamRotation))
                {
                    var seamInstance = PlacePiece(config.MultiRoomSeamPrefab, cell, seamRotation, config, parent);
                    if (seamInstance == null) continue;

                    AttachShellMarker(seamInstance, config.MultiRoomSeamPrefab, config.RuleSet, cell, RoomShellSlotRole.Wall);
                    placedInstances.Add(seamInstance);
                    cellByInstance[seamInstance] = cell;

                    var seamOutward = DetermineOutwardDir(cell, cellOwner, seamRotation);
                    if (seamOutward.HasValue) outwardDirByInstance[seamInstance] = seamOutward.Value;
                    continue; // never falls through to the resolver for this cell
                }

                if (TryComputeTJunctionCornerSubstitution(cell, primaryRoomsByCell, config.RuleSet,
                        out var cornerPrefab, out var cornerRotation))
                {
                    var cornerInstance = PlacePiece(cornerPrefab, cell, cornerRotation, config, parent);
                    if (cornerInstance == null) continue;

                    AttachShellMarker(cornerInstance, cornerPrefab, config.RuleSet, cell, RoomShellSlotRole.Wall);
                    placedInstances.Add(cornerInstance);
                    cellByInstance[cornerInstance] = cell;
                    tJunctionCornerCells.Add(cell);

                    var cornerOutward = DetermineOutwardDir(cell, cellOwner, cornerRotation);
                    if (cornerOutward.HasValue) outwardDirByInstance[cornerInstance] = cornerOutward.Value;
                    continue; // never falls through to the resolver for this cell
                }

                var snapshot = BuildSnapshot(cell, primaryRoomsByCell, directions, config.RuleSet.RuleTag);
                var result = PrefabRuleResolver.Resolve(config.RuleSet, snapshot);
                if (result.Prefab == null) continue;
                var yRotation = result.Matched
                    ? result.YRotation
                    : ResolveFallbackYRotation(snapshot, result.YRotation);

                var instance = PlacePiece(result.Prefab, cell, yRotation, config, parent);
                if (instance == null) continue;

                AttachShellMarker(instance, result.Prefab, config.RuleSet, cell, RoomShellSlotRole.Wall);
                placedInstances.Add(instance);
                cellByInstance[instance] = cell;

                var outward = DetermineOutwardDir(cell, cellOwner, yRotation);
                if (outward.HasValue) outwardDirByInstance[instance] = outward.Value;
            }

            ReconcileRunOutwardDirs(cellByInstance, doorCellsByInstance, straightPrefab, outwardDirByInstance);
            ShellAlignmentPass.Align(placedInstances, config.CellSize, outwardDirByInstance);
            RemoveWallPiecesOverlappingDoors(cellByInstance, doorCellsByInstance);

            // WallTjunctionPrefab is retired (2026-09-23): its branch connector
            // was authored ~0.32 units off the true cell-boundary midpoint, vs.
            // ~0.07 off for Corner — see plans/tjunction-corner-research.md.
            // Every former T-junction cell now gets a fixed, deterministic
            // Corner substitution (no contextual/topological inference — no
            // established autotiling/WFC/RuleTile convention validated one),
            // so some of these are expected to need a human's eye afterward,
            // same as any other generator output. Logged so a human knows
            // exactly which cells to check rather than scanning the whole level.
            if (tJunctionCornerCells.Count > 0)
                Debug.Log("[LevelGeneration] " + tJunctionCornerCells.Count +
                    " cell(s) resolved via T-junction-to-Corner substitution (worth a visual check): " +
                    string.Join(", ", tJunctionCornerCells));

            return cellByInstance;
        }

        // A door's mesh is always a fixed-width module (Door.prefab is
        // authored as a true 3-cell-wide piece regardless of how many cells
        // its connection actually widened into — WidenDoorway can correctly
        // refuse to widen when the neighboring topology doesn't support a
        // clean border on one or both perpendicular sides, and DoorCells then
        // falls back to a single cell). The normal per-cell placement loop
        // above only excludes cells actually listed in DoorCells, so an
        // unwidened door's real mesh footprint can physically overlap an
        // independently-placed wall piece at its immediate neighbor cell(s).
        // Runs after alignment so it checks final, real geometry rather than
        // trying to predict the door's footprint from topology — found
        // 2026-09-22 live-inspecting seed 555555 (Door at (13,9),
        // DoorCells=[(13,9)] only, clipped into independently-placed
        // WallStraightPrefab instances at both (12,9) and (14,9)).
        private static void RemoveWallPiecesOverlappingDoors(Dictionary<GameObject, Vector2Int> cellByInstance,
            Dictionary<GameObject, List<Vector2Int>> doorCellsByInstance)
        {
            var doors = new List<GameObject>();
            var others = new List<GameObject>();
            foreach (var instance in cellByInstance.Keys)
            {
                if (instance == null) continue;
                if (doorCellsByInstance.ContainsKey(instance)) doors.Add(instance);
                else others.Add(instance);
            }

            if (doors.Count == 0) return;

            var toRemove = new List<GameObject>();
            foreach (var door in doors)
            {
                var doorBounds = CombinedInstanceBounds(door);
                foreach (var other in others)
                {
                    if (toRemove.Contains(other)) continue;
                    var otherBounds = CombinedInstanceBounds(other);

                    float overlapX = Mathf.Min(doorBounds.max.x, otherBounds.max.x) - Mathf.Max(doorBounds.min.x, otherBounds.min.x);
                    float overlapZ = Mathf.Min(doorBounds.max.z, otherBounds.max.z) - Mathf.Max(doorBounds.min.z, otherBounds.min.z);
                    // Genuine volumetric overlap on both axes, not merely touching at a shared edge.
                    if (overlapX > 0.02f && overlapZ > 0.02f)
                        toRemove.Add(other);
                }
            }

            Debug.Log("[DEBUG-door01] toRemove.Count = " + toRemove.Count);
            foreach (var instance in toRemove)
            {
                cellByInstance.Remove(instance);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // Combined world-space mesh bounds across every MeshFilter in an
        // instance's children — mirrors WorldMeshBounds/ReadSceneWallGeometry's
        // own accumulation pattern for a composite prefab like
        // WallTjunctionPrefab, which has multiple sub-meshes.
        private static Bounds CombinedInstanceBounds(GameObject instance)
        {
            Bounds combined = default;
            bool any = false;
            foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                if (filter.gameObject.name.Trim().StartsWith("WallConnection")) continue; // DEBUG-door02 temp
                var b = WorldMeshBounds(filter);
                if (!any) { combined = b; any = true; }
                else combined.Encapsulate(b);
            }

            return combined;
        }

        // The only place that reads back what BuildShells/PlaceDoorway wrote,
        // mirroring the marker convention this same class owns on the write
        // side — keeps "what counts as a wall vs. a doorway" in one file
        // instead of splintering it across a writer and a separate reader.
        // Deliberately returns plain grid data (no UnityEngine scene types
        // beyond Vector2Int) so RoomFloorFloodFill's actual flood-fill/merge/
        // split/door-stops-propagation logic can be unit-tested without a
        // scene, prefabs, or colliders at all.
        //
        // Cell membership is decided by AABB overlap between each piece's
        // world-space mesh bounds and a per-cell query box, not by
        // reconstructing a cell from a piece's transform — ShellAlignmentPass
        // deliberately shifts exterior wall pieces close to half a cell
        // toward their outward edge, and reverse-engineering a cell from that
        // shifted position was already tried and already rounds wrong for
        // most exterior walls (SCARAB-MAP-01 follow-up, 2026-09-03). No
        // Physics.*/layer-based query either: this project has no dedicated
        // "Wall" physics layer, and Torch/Pillar/PushBlock/stairs/Door all
        // share the Default layer with real walls, so a layer query would
        // false-positive on ordinary room dressing. Component-based
        // collection (PrefabBrushObject marker, not layer) sidesteps both
        // problems with no ProjectSettings changes at all.
        //
        // Deliberately reads MeshFilter + transform math instead of
        // Collider.bounds: Collider.bounds does not reflect a transform
        // change made by script until PhysX syncs (no simulation runs in
        // Edit Mode), so right after RoomShellAssembler places/moves a piece
        // it can report stale, near-origin bounds instead of the piece's
        // actual world position (confirmed live — every wall piece resolved
        // to the same wrong bounds regardless of its real position). Mesh
        // bounds transformed via Transform.TransformPoint are always current
        // and need no sync, exactly like ShellAlignmentPass.TransformBounds
        // already does elsewhere in this file's neighborhood.
        public static (HashSet<Vector2Int> wallCells, HashSet<Vector2Int> doorCells) ReadSceneWallGeometry(
            GameObject levelRoot, float cellSize)
        {
            var wallBounds = new List<Bounds>();
            var doorBounds = new List<Bounds>();

            foreach (var marker in levelRoot.GetComponentsInChildren<PrefabBrushObject>(true))
            {
                if (marker.PlacementGroup != WallPlacementGroup) continue;

                var boundsList = marker.GetComponent<DoorwayMarker>() != null ? doorBounds : wallBounds;
                foreach (var filter in marker.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh == null) continue;
                    boundsList.Add(WorldMeshBounds(filter));
                }
            }

            var wallCells = CellsOverlapping(wallBounds, cellSize);
            var doorCells = CellsOverlapping(doorBounds, cellSize);
            return (wallCells, doorCells);
        }

        // Mirrors ShellAlignmentPass.TransformBounds (transform all 8 corners
        // of the mesh's local bounds, not just its center) — safe for
        // whatever rotation this grid-aligned kit's pieces carry.
        private static Bounds WorldMeshBounds(MeshFilter filter)
        {
            var localBounds = filter.sharedMesh.bounds;
            var t = filter.transform;
            var result = new Bounds(t.TransformPoint(localBounds.min), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) != 0 ? localBounds.max.x : localBounds.min.x,
                    (i & 2) != 0 ? localBounds.max.y : localBounds.min.y,
                    (i & 4) != 0 ? localBounds.max.z : localBounds.min.z);
                result.Encapsulate(t.TransformPoint(corner));
            }

            return result;
        }

        private static HashSet<Vector2Int> CellsOverlapping(List<Bounds> boundsList, float cellSize)
        {
            var cells = new HashSet<Vector2Int>();
            if (boundsList.Count == 0) return cells;

            // Generous Y range (props/doors are already excluded by the
            // marker filter above, not by height) but a TIGHT XZ margin — a
            // wall/door piece is authored to fit exactly within its own
            // cell(s) along its run axis (confirmed live, 2026-09-04: a
            // WallStraightPrefab instance measures exactly CellSize=1 unit
            // along its run axis, not the ~2 units a raw, import-scale-blind
            // .obj read wrongly suggested). A piece's bounds edge therefore
            // often sits EXACTLY on a shared cell boundary — the previous
            // ±1-cell index padding plus a 5% GROW margin double-counted
            // that shared edge into the neighboring cell too, which is what
            // actually caused a border row of a room's own cells to get
            // wrongly classified as wall (not a real pre-existing wall-
            // spacing mismatch, as first — wrongly — suspected). A slight
            // SHRINK instead of a grow absorbs ordinary floating-point jitter
            // without re-introducing that double-count.
            const float yMin = -1f, yMax = 4f, shrink = 0.98f;

            foreach (var bounds in boundsList)
            {
                int minX = Mathf.FloorToInt((bounds.min.x / cellSize) + 0.5f);
                int maxX = Mathf.CeilToInt((bounds.max.x / cellSize) + 0.5f) - 1;
                int minZ = Mathf.FloorToInt((bounds.min.z / cellSize) + 0.5f);
                int maxZ = Mathf.CeilToInt((bounds.max.z / cellSize) + 0.5f) - 1;

                for (int x = minX; x <= maxX; x++)
                for (int z = minZ; z <= maxZ; z++)
                {
                    var cell = new Vector2Int(x, z);
                    if (cells.Contains(cell)) continue;

                    var cellBox = new Bounds(new Vector3(x * cellSize, (yMin + yMax) * 0.5f, z * cellSize),
                        new Vector3(cellSize * shrink, yMax - yMin, cellSize * shrink));
                    if (bounds.Intersects(cellBox)) cells.Add(cell);
                }
            }

            return cells;
        }

        // A wall cell's run-axis neighbors are guaranteed wallCells (void), never
        // rooms, by construction of the Straight/Corner match that got it here —
        // so any room-cell neighbor found here is on the thickness axis. Exactly
        // one room neighbor with a non-room opposite is an unambiguous exterior
        // boundary (confirmed live against 8 hand-calibrated wall/corner pairs).
        //
        // Two room neighbors on OPPOSITE sides, or on different rooms, is a
        // genuine room-vs-room seam with no single thickness axis to resolve —
        // deliberately left alone (no data to resolve it), falls back to plain
        // centering. But two room neighbors on PERPENDICULAR sides belonging to
        // the SAME room is a convex room corner, not a seam: the piece placed
        // here is Straight-shaped (Corner/TJunction/seam cells never reach this
        // ambiguous branch in practice for this wall kit), so it has exactly one
        // real thickness axis, determined by its own resolved rotation — the
        // room neighbor lying on the OTHER axis is irrelevant to the shift
        // decision. Verified live 2026-09-21 against seed 1959606433's cells
        // (19,26) and (15,10) — the last 2 of 23 real world-space gaps found
        // this session, both this exact shape.
        private static Vector2Int? DetermineOutwardDir(Vector2Int cell, Dictionary<Vector2Int, string> cellOwner,
            float yRotation)
        {
            var roomDirs = new List<(Vector2Int dir, string room)>();
            foreach (var dir in FourWayDirs)
                if (cellOwner.TryGetValue(cell + dir, out var room))
                    roomDirs.Add((dir, room));

            if (roomDirs.Count == 0) return null;

            if (roomDirs.Count == 1)
            {
                var inward = roomDirs[0].dir;
                var outward = -inward;
                return cellOwner.ContainsKey(cell + outward) ? null : outward;
            }

            if (roomDirs.Count == 2)
            {
                var (dirA, roomA) = roomDirs[0];
                var (dirB, roomB) = roomDirs[1];
                bool isOppositePair = dirA == -dirB;
                if (!isOppositePair && roomA == roomB)
                {
                    bool thicknessIsZ = ThicknessIsZ(yRotation);
                    Vector2Int? inward = thicknessIsZ
                        ? (dirA.x == 0 ? dirA : dirB.x == 0 ? dirB : (Vector2Int?)null)
                        : (dirA.y == 0 ? dirA : dirB.y == 0 ? dirB : (Vector2Int?)null);

                    if (inward.HasValue)
                    {
                        var outward = -inward.Value;
                        return cellOwner.ContainsKey(cell + outward) ? null : outward;
                    }
                }
            }

            return null; // ambiguous — room on more than one side with no single thickness axis to resolve it
        }

        // A resolved yRotation's thickness axis — 0°/180° keep the wall kit's
        // Z-thickness authoring as-is, 90°/270° swap it onto X. Shared by
        // DetermineOutwardDir and ReconcileRunOutwardDirs so both agree on
        // which axis a given piece's flat face actually runs along.
        private static bool ThicknessIsZ(float yRotation) => Mathf.RoundToInt(yRotation / 90f) % 2 == 0;

        // Finds a named rule's prefab in the RuleSet (e.g. "Straight") —
        // data-driven, not a hardcoded prefab-asset reference, so it keeps
        // working if the ruleset's Straight prefab is ever swapped out.
        private static GameObject FindRulePrefabByName(PrefabRuleSet ruleSet, string ruleName)
        {
            if (ruleSet?.Rules == null) return null;
            foreach (var rule in ruleSet.Rules)
                if (rule != null && rule.Name == ruleName)
                    return rule.Prefab;
            return null;
        }

        // Resolves DetermineOutwardDir's per-cell verdicts (computed above,
        // already in outwardDirByInstance) into one consistent choice per
        // connected wall run (found 2026-09-22, fresh-eyes pass over the open
        // wall-alignment findings): DetermineOutwardDir decides per cell in
        // isolation, so a divider cell (genuine 2-room-opposite, deliberately
        // centered — see that method's own comment) sitting next to a
        // single-room cell (deliberately flush) in the same physical wall row
        // produces a visible step exactly at the boundary. Every door is a
        // divider cell by construction (a doorway is exactly the shared
        // boundary of 2 rooms), so this hit doors constantly, not just
        // occasional same-room-run transitions.
        //
        // Walks each maximal same-orientation, run-axis-adjacent chain of
        // Straight-shaped wall/door instances (Corner/T-junction/multi-room-
        // seam pieces are deliberately excluded — their footprint isn't a
        // simple flat slab spanning the cell, so "matching a neighbor's
        // thickness-axis center" doesn't have the same meaning, and they act
        // as natural run boundaries instead) in cell order, and fills a
        // null-verdict cell only when it's sandwiched between (or trailing) an
        // anchor whose own verdict is known — for an interior gap between two
        // anchors, only when they agree. A run whose anchors disagree (e.g. a
        // door between two different rooms with no common flush side) is left
        // exactly as DetermineOutwardDir decided for each cell — correct, not
        // a bug (see plans/README.md's "Item 4" finding for the separate,
        // deliberately unaddressed class of seam this does NOT try to fix:
        // where the room bordering the run switches sides mid-run, so even
        // the *direction* to push disagrees, not just whether to push).
        //
        // A door's own recorded cell (the single Vector2Int cellByInstance
        // uses) is only one of potentially several DoorCells its mesh
        // actually spans — using only that cell for adjacency would let the
        // door "swallow" its other cells without registering them as
        // occupied, breaking the chain across itself and making a genuinely
        // two-sided door look one-sided to this pass. doorCellsByInstance
        // carries the door's full span in for that reason.
        private static void ReconcileRunOutwardDirs(Dictionary<GameObject, Vector2Int> cellByInstance,
            Dictionary<GameObject, List<Vector2Int>> doorCellsByInstance, GameObject straightPrefab,
            Dictionary<GameObject, Vector2Int> outwardDirByInstance)
        {
            if (straightPrefab == null) return;

            var occupiedCells = new Dictionary<GameObject, List<Vector2Int>>();
            foreach (var kvp in cellByInstance)
            {
                var instance = kvp.Key;
                if (instance == null) continue;

                if (doorCellsByInstance.TryGetValue(instance, out var doorCells))
                {
                    occupiedCells[instance] = doorCells;
                    continue;
                }

                var marker = instance.GetComponent<PrefabBrushObject>();
                if (marker != null && marker.SourcePrefab == straightPrefab)
                    occupiedCells[instance] = new List<Vector2Int> { kvp.Value };
            }

            var instanceByCell = new Dictionary<Vector2Int, GameObject>();
            foreach (var kvp in occupiedCells)
                foreach (var cell in kvp.Value)
                    instanceByCell[cell] = kvp.Key;

            var visited = new HashSet<GameObject>();
            foreach (var seed in occupiedCells.Keys)
            {
                if (visited.Contains(seed)) continue;

                var runMembers = new List<GameObject>();
                var queue = new Queue<GameObject>();
                queue.Enqueue(seed);
                visited.Add(seed);
                bool runThicknessIsZ = ThicknessIsZ(seed.transform.eulerAngles.y);

                while (queue.Count > 0)
                {
                    var cur = queue.Dequeue();
                    runMembers.Add(cur);
                    var curRotY = cur.transform.eulerAngles.y;
                    var runDirs = ThicknessIsZ(curRotY) ? RunAxisX : RunAxisZ;

                    foreach (var ownCell in occupiedCells[cur])
                    foreach (var dir in runDirs)
                    {
                        if (!instanceByCell.TryGetValue(ownCell + dir, out var neighbor)) continue;
                        if (neighbor == cur || visited.Contains(neighbor)) continue;
                        if (!Mathf.Approximately(curRotY, neighbor.transform.eulerAngles.y)) continue;
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }

                runMembers.Sort((a, b) => runThicknessIsZ
                    ? cellByInstance[a].x.CompareTo(cellByInstance[b].x)
                    : cellByInstance[a].y.CompareTo(cellByInstance[b].y));

                var anchorIdxs = new List<int>();
                for (int i = 0; i < runMembers.Count; i++)
                    if (outwardDirByInstance.ContainsKey(runMembers[i])) anchorIdxs.Add(i);

                if (anchorIdxs.Count == 0) continue;

                var firstDir = outwardDirByInstance[runMembers[anchorIdxs[0]]];
                for (int j = 0; j < anchorIdxs[0]; j++) outwardDirByInstance[runMembers[j]] = firstDir;

                var lastDir = outwardDirByInstance[runMembers[anchorIdxs[^1]]];
                for (int j = anchorIdxs[^1] + 1; j < runMembers.Count; j++) outwardDirByInstance[runMembers[j]] = lastDir;

                for (int a = 0; a < anchorIdxs.Count - 1; a++)
                {
                    int lo = anchorIdxs[a], hi = anchorIdxs[a + 1];
                    var dirLo = outwardDirByInstance[runMembers[lo]];
                    var dirHi = outwardDirByInstance[runMembers[hi]];
                    if (dirLo == dirHi)
                        for (int j = lo + 1; j < hi; j++)
                            outwardDirByInstance[runMembers[j]] = dirLo;
                }
            }
        }

        private static GameObject PlaceDoorway(LayoutConnection connection, float yRotation, ShellAssemblyConfig config,
            Transform parent, Vector2Int cell)
        {
            if (config.DoorPrefab == null) return null;

            float sumX = 0f, sumY = 0f;
            foreach (var doorCell in connection.DoorCells)
            {
                sumX += doorCell.x;
                sumY += doorCell.y;
            }

            var placementCell = new Vector2(sumX / connection.DoorCells.Count, sumY / connection.DoorCells.Count);

            var instance = PlacePiece(config.DoorPrefab, placementCell, yRotation, config, parent);
            if (instance == null) return null;

            // Anchored to the outer loop's own wall cell, not placementCell's averaged position —
            // this is the exact Vector2Int BuildShells records in cellByInstance for this door, so
            // the anchor and cellByInstance never disagree about this door's identity.
            AttachShellMarker(instance, config.DoorPrefab, config.RuleSet, cell, RoomShellSlotRole.Door);
            var doorwayMarker = instance.AddComponent<DoorwayMarker>();
            doorwayMarker.DoorwayId = connection.DoorwayId;
            return instance;
        }

        private static void AddRoomMembership(Dictionary<Vector2Int, HashSet<string>> membership, Vector2Int cell,
            string owningRoom, Dictionary<Vector2Int, string> cellOwner)
        {
            if (cellOwner.ContainsKey(cell)) return; // only unclaimed cells are wall cells

            if (!membership.TryGetValue(cell, out var rooms))
            {
                rooms = new HashSet<string>();
                membership[cell] = rooms;
            }

            rooms.Add(owningRoom);
        }

        // A wall cell whose own primaryRoomsByCell membership already lists 2+
        // distinct rooms/margins is never a genuine single-room branch — it's two
        // (or more) different rooms' corners landing on the same or adjacent cells
        // across a narrow shared gap. PrefabRuleResolver's 4-tag pattern can't
        // distinguish this from a real T-junction (both look like "3 neighbors
        // tagged This"), so this is decided here, before the resolver is ever
        // consulted, using the room-identity data BuildSnapshot would otherwise
        // discard (SCARAB-013 follow-up, 2026-09-21: verified live against seed
        // 1959606433 — 8 of 10 misclassified cells were this case).
        private static bool TryComputeMultiRoomSeamRotation(Vector2Int cell,
            Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell, Vector2Int[] fourWayDirs, out float yRotation)
        {
            yRotation = 0f;
            if (!primaryRoomsByCell.TryGetValue(cell, out var ownRooms) || ownRooms.Count < 2)
                return false;

            var continuing = new List<int>();
            for (int i = 0; i < fourWayDirs.Length; i++)
            {
                var neighbor = cell + fourWayDirs[i];
                if (primaryRoomsByCell.TryGetValue(neighbor, out var neighborRooms) && neighborRooms.Overlaps(ownRooms))
                    continuing.Add(i);
            }

            if (continuing.Count != 2) return false;

            // continuing[0]/[1] are indices into fourWayDirs (N=0,E=1,S=2,W=3).
            // JunctionWithCorner's authored shape: straight run along one axis +
            // one corner arm. If both continuing directions are an opposite pair,
            // this is a straight run, not a seam corner — bail out rather than
            // guess a rotation for a shape JunctionWithCorner doesn't represent.
            bool isOppositePair = (continuing[0] + 2) % 4 == continuing[1];
            if (isOppositePair) return false;

            yRotation = continuing[0] * 90f;
            return true;
        }

        // Substitutes Corner for what used to match the retired TJunction
        // rule (2026-09-23): a cell with 3 continuing wall-network directions
        // always has one "mandatory" direction (whose opposite is the single
        // missing direction — a Corner structurally can't represent an
        // opposite pair) plus one full opposite-pair axis to choose from.
        // Which pair member joins the mandatory direction has no established
        // convention in any surveyed autotiling/WFC/RuleTile system (see
        // plans/tjunction-corner-research.md) — grilled with Michael
        // 2026-09-23: ship a simple fixed deterministic rule and let a human
        // hand-correct the occasional wrong cell, exactly like RuleTile's own
        // first-match-wins ambiguity resolution. Fixed rule: "clockwise-first"
        // — keep the pair member one clockwise step past the mandatory
        // direction, in this file's own N=0,E=1,S=2,W=3 indexing.
        //
        // The rotation itself is never hand-derived — a synthetic 2-tag
        // snapshot is matched against the ruleset's own Corner rule via
        // PrefabRuleResolver, so this stays correct however Corner's own
        // rotation convention is actually authored (this file's own
        // ResolveFallbackYRotation comment documents two prior hand-derived
        // rotation guesses that were wrong).
        private static bool TryComputeTJunctionCornerSubstitution(Vector2Int cell,
            Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell, PrefabRuleSet ruleSet,
            out GameObject prefab, out float yRotation)
        {
            prefab = null;
            yRotation = 0f;
            if (!primaryRoomsByCell.TryGetValue(cell, out var ownRooms)) return false;

            var continuing = new List<int>();
            for (int i = 0; i < FourWayDirs.Length; i++)
            {
                var neighbor = cell + FourWayDirs[i];
                if (primaryRoomsByCell.TryGetValue(neighbor, out var neighborRooms) && neighborRooms.Overlaps(ownRooms))
                    continuing.Add(i);
            }

            if (continuing.Count != 3) return false;

            int missing = -1;
            for (int i = 0; i < 4; i++)
                if (!continuing.Contains(i)) missing = i;

            int mandatory = (missing + 2) % 4; // opposite of the single missing direction
            int keptPair = (mandatory + 1) % 4; // clockwise-first tiebreak

            var syntheticSnapshot = new NeighborSnapshot(4);
            syntheticSnapshot.Tags[mandatory] = ruleSet.RuleTag;
            syntheticSnapshot.Tags[keptPair] = ruleSet.RuleTag;

            var result = PrefabRuleResolver.Resolve(ruleSet, syntheticSnapshot);
            if (!result.Matched) return false;

            prefab = result.Prefab;
            yRotation = result.YRotation;
            return true;
        }

        // PrefabRuleResolver's DefaultPrefab fallback (Matched=false) always
        // returns YRotation=0 regardless of topology — correct for the 0-tag
        // (isolated) and 4-tag (fully enclosed) cases, which have no single
        // continuing direction, but wrong for the far more common 1-tag
        // "dead-end/spur" case: the wall still needs to run toward its one
        // real neighbor's axis, or it visibly separates from it. Verified
        // live, 2026-09-21: every one of 23 real world-space gaps found
        // across seed 1959606433 bordered an unmatched 1-tag cell placed at
        // rotation 0 regardless of which axis it actually needed to run
        // along; ground-truthed against the real WallPrefabRuleSet's own
        // matched Straight-rule outputs (E/W tagged -> 0°, N/S tagged -> 90°
        // — the opposite polarity a naive per-direction reading suggests) to
        // get this right empirically rather than re-deriving it from
        // PrefabRuleResolver's rotation-search formula, which had already
        // produced two wrong, regression-causing guesses. Fixing this closed
        // 21 of the 23 gaps; the remaining 2 are a separate, much smaller
        // ShellAlignmentPass outward-shift mismatch, not a rotation issue.
        private static float ResolveFallbackYRotation(NeighborSnapshot snapshot, float defaultYRotation)
        {
            int taggedIndex = -1, taggedCount = 0;
            for (int i = 0; i < snapshot.Tags.Length; i++)
            {
                if (string.IsNullOrEmpty(snapshot.Tags[i])) continue;
                taggedCount++;
                taggedIndex = i;
            }

            if (taggedCount != 1 || snapshot.Tags.Length != 4) return defaultYRotation;
            return taggedIndex == 1 || taggedIndex == 3 ? 0f : 90f; // E(1)/W(3) -> 0°, N(0)/S(2) -> 90°
        }

        private static NeighborSnapshot BuildSnapshot(Vector2Int cell, Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell,
            Vector2Int[] directions, string ruleTag)
        {
            // `cell` is itself a wall cell (it came from the wallCellRooms-keyed
            // loop in BuildShells), so it always has an entry here.
            var ownRooms = primaryRoomsByCell[cell];

            var snapshot = new NeighborSnapshot(directions.Length);
            for (int i = 0; i < directions.Length; i++)
            {
                var neighbor = cell + directions[i];
                snapshot.Tags[i] = primaryRoomsByCell.TryGetValue(neighbor, out var neighborRooms) &&
                                    neighborRooms.Overlaps(ownRooms)
                    ? ruleTag
                    : null;
            }

            return snapshot;
        }

        private static GameObject PlacePiece(GameObject prefab, Vector2 cell, float yRotation,
            ShellAssemblyConfig config, Transform parent, float yPosition = 0f)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) return null;

            // Preserve the prefab's own baked-in rotation (e.g. WallCornerPrefab's
            // 180°) composed with the resolver's computed rotation, but place the
            // piece at the bare cell position — the baked *position* offset some
            // pieces carry is tuned for interactive raycast placement, not this
            // grid convention, and re-adding it double-counts against the mesh's
            // own asymmetric bounds.
            var bakedRotation = instance.transform.localRotation;
            var extraRotation = Quaternion.Euler(0f, yRotation, 0f);

            Undo.RegisterCreatedObjectUndo(instance, "Bake Room Shell");
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = new Vector3(cell.x * config.CellSize, yPosition, cell.y * config.CellSize);
            instance.transform.localRotation = extraRotation * bakedRotation;

            return instance;
        }

        private static void AttachShellMarker(GameObject instance, GameObject sourcePrefab, PrefabRuleSet ruleSet,
            Vector2Int cell, RoomShellSlotRole role, string placementGroup = WallPlacementGroup)
        {
            var marker = instance.AddComponent<PrefabBrushObject>();
            marker.SourcePrefab = sourcePrefab;
            marker.PlacementGroup = placementGroup;
            marker.PlacementTime = DateTime.UtcNow.ToString("o");
            marker.RuleTag = ruleSet.RuleTag;
            marker.RuleSet = ruleSet;
            marker.SurfaceNormal = Vector3.up;

            var anchor = instance.AddComponent<RoomShellSlotAnchor>();
            anchor.Cell = cell;
            anchor.Role = role;
        }
    }
}

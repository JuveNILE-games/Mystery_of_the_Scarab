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
        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0) // N, E, S, W
        };

        private static readonly Vector2Int[] EightWayDirs =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1) // N, NE, E, SE, S, SW, W, NW
        };

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
            var outwardDirByInstance = new Dictionary<GameObject, Vector2Int>();

            foreach (var cell in wallCells)
            {
                var snapshot = BuildSnapshot(cell, primaryRoomsByCell, directions, config.RuleSet.RuleTag);
                var result = PrefabRuleResolver.Resolve(config.RuleSet, snapshot);
                if (result.Prefab == null) continue;

                if (doorwayByCell.TryGetValue(cell, out var connection))
                {
                    if (!placedDoorways.Add(connection.DoorwayId)) continue; // other cell already placed the door
                    var door = PlaceDoorway(connection, result.YRotation, config, parent);
                    if (door != null)
                    {
                        placedInstances.Add(door);
                        cellByInstance[door] = cell;
                    }
                    continue;
                }

                var instance = PlacePiece(result.Prefab, cell, result.YRotation, config, parent);
                if (instance == null) continue;

                AttachShellMarker(instance, result.Prefab, config.RuleSet);
                placedInstances.Add(instance);
                cellByInstance[instance] = cell;

                var outward = DetermineOutwardDir(cell, cellOwner);
                if (outward.HasValue) outwardDirByInstance[instance] = outward.Value;
            }

            ShellAlignmentPass.Align(placedInstances, config.CellSize, outwardDirByInstance);
            return cellByInstance;
        }

        // A wall cell's run-axis neighbors are guaranteed wallCells (void), never
        // rooms, by construction of the Straight/Corner match that got it here —
        // so any room-cell neighbor found here is on the thickness axis. Exactly
        // one room neighbor with a non-room opposite is an unambiguous exterior
        // boundary (confirmed live against 8 hand-calibrated wall/corner pairs);
        // zero or two room neighbors is the room-vs-room seam case that was
        // deliberately left alone before (no data to resolve it), so this returns
        // null and the piece falls back to plain centering.
        private static Vector2Int? DetermineOutwardDir(Vector2Int cell, Dictionary<Vector2Int, string> cellOwner)
        {
            Vector2Int? inward = null;
            foreach (var dir in FourWayDirs)
            {
                if (!cellOwner.ContainsKey(cell + dir)) continue;
                if (inward.HasValue) return null; // room on more than one side — ambiguous
                inward = dir;
            }

            if (!inward.HasValue) return null;

            var outward = -inward.Value;
            return cellOwner.ContainsKey(cell + outward) ? null : outward;
        }

        private static GameObject PlaceDoorway(LayoutConnection connection, float yRotation, ShellAssemblyConfig config,
            Transform parent)
        {
            if (config.DoorPrefab == null) return null;

            float sumX = 0f, sumY = 0f;
            foreach (var doorCell in connection.DoorCells)
            {
                sumX += doorCell.x;
                sumY += doorCell.y;
            }

            var cell = new Vector2(sumX / connection.DoorCells.Count, sumY / connection.DoorCells.Count);

            var instance = PlacePiece(config.DoorPrefab, cell, yRotation, config, parent);
            if (instance == null) return null;

            AttachShellMarker(instance, config.DoorPrefab, config.RuleSet);
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
            ShellAssemblyConfig config, Transform parent)
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
            instance.transform.localPosition = new Vector3(cell.x * config.CellSize, 0f, cell.y * config.CellSize);
            instance.transform.localRotation = extraRotation * bakedRotation;

            return instance;
        }

        private static void AttachShellMarker(GameObject instance, GameObject sourcePrefab, PrefabRuleSet ruleSet)
        {
            var marker = instance.AddComponent<PrefabBrushObject>();
            marker.SourcePrefab = sourcePrefab;
            marker.PlacementGroup = "LevelGeneration";
            marker.PlacementTime = DateTime.UtcNow.ToString("o");
            marker.RuleTag = ruleSet.RuleTag;
            marker.RuleSet = ruleSet;
            marker.SurfaceNormal = Vector3.up;
        }
    }
}

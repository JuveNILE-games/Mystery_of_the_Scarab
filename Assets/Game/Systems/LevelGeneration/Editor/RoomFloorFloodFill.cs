using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Pure grid algorithm behind LevelBaker.BakeFloors — deliberately has no
    // UnityEngine scene/component dependency beyond Vector2Int (the same
    // plain value type LayoutRoomNode.FootprintCells already uses), so the
    // tricky part (merge-on-gap, split-into-two-pockets, door-stops-
    // propagation) is unit-testable with hand-authored data, no scene/
    // prefab/collider setup required. RoomShellAssembler.ReadSceneWallGeometry
    // is the only place that turns actual scene geometry into the
    // wallCells/doorCells sets this takes as input.
    public static class RoomFloorFloodFill
    {
        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        // Flood-fills each room's *current* floor extent from its original
        // Layout footprint cells, using actual scene wall/door geometry as the
        // boundary rather than trusting FootprintCells directly — a designer
        // may have hand-reshaped a room's walls since the last Bake (Michael,
        // 2026-09-04). A hand-removed wall (a genuine gap: neither a wall nor
        // a door cell) naturally merges two rooms' floors, since whichever
        // room's flood reaches the shared area first claims it (first-claimed-
        // wins via the global `visited` set) — no special-casing needed. A
        // Door cell is floor but does NOT propagate the flood further: a Door
        // is an intentional, separate connection with its own
        // RoomConnection/locking data, not a hole in the wall, so an ordinary
        // doorway between two rooms must not merge them on an otherwise
        // pristine, unedited bake.
        //
        // Deliberately uncapped in the normal case (Michael, 2026-09-04:
        // "trust the walls" — a real gap is allowed to leak floor arbitrarily
        // far) EXCEPT for a defensive sanity circuit-breaker: that decision
        // was scoped to a small gap leaking into an adjacent area, not to
        // "zero wall geometry exists at all" (e.g. Bake Floor clicked before
        // Bake ever ran for this seed), where the BFS would otherwise have no
        // natural stopping boundary and can hang the Editor. The cap only
        // ever fires on what is already a bug, never in normal use.
        public static Dictionary<Vector2Int, string> ComputeFloorCellsByRoom(Layout layout,
            HashSet<Vector2Int> wallCells, HashSet<Vector2Int> doorCells)
        {
            var roomIdByCell = new Dictionary<Vector2Int, string>();
            var visited = new HashSet<Vector2Int>();

            int totalFootprintCells = 0;
            foreach (var room in layout.Rooms)
                totalFootprintCells += room.FootprintCells.Count;
            int sanityCap = Math.Max(500, totalFootprintCells * 20);

            foreach (var room in layout.Rooms)
            {
                // Margin is structural cell-ownership filler, not a gameplay
                // room — it must not become a RoomDefinition/RoomController/
                // graph node (feedback_margin_not_a_gameplay_room), and by the
                // same reasoning must not become floor either (Michael,
                // 2026-09-04, live on Level_1399816449: "margins have a floor
                // too, which aren't rooms"). Not seeding from a margin's own
                // cells is enough — a real room's flood can still legitimately
                // reach into a margin's cells through a genuine gap, same as
                // reaching any other room, and gets attributed to the real
                // room in that case.
                if (room.Role == RoomRole.Margin) continue;

                foreach (var originalCell in room.FootprintCells)
                {
                    if (visited.Contains(originalCell)) continue;
                    if (wallCells.Contains(originalCell)) continue; // seed cell itself got walled off entirely

                    if (!FloodFrom(originalCell, room.RoomId, wallCells, doorCells, visited, roomIdByCell, sanityCap))
                    {
                        Debug.LogError($"[LevelGeneration] Floor flood-fill exceeded its sanity cap ({sanityCap} cells) " +
                                        $"starting from {room.RoomId} at {originalCell} — likely missing/incomplete wall " +
                                        "geometry (was Bake run for this seed before Bake Floor?). Stopped early.");
                        return roomIdByCell;
                    }
                }
            }

            return roomIdByCell;
        }

        // Invariant: only non-door cells ever get enqueued, so every dequeued
        // `cell` is always safe to expand from. A door neighbor gets marked
        // visited/floored (roomIdByCell) but is never itself enqueued — that's
        // the entire mechanism that stops the flood from crossing an ordinary
        // doorway into the room on its other side.
        private static bool FloodFrom(Vector2Int seed, string roomId, HashSet<Vector2Int> wallCells,
            HashSet<Vector2Int> doorCells, HashSet<Vector2Int> visited, Dictionary<Vector2Int, string> roomIdByCell,
            int sanityCap)
        {
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(seed);
            visited.Add(seed);
            roomIdByCell[seed] = roomId;

            while (queue.Count > 0)
            {
                var cell = queue.Dequeue();

                foreach (var dir in FourWayDirs)
                {
                    var neighbor = cell + dir;
                    if (visited.Contains(neighbor) || wallCells.Contains(neighbor)) continue;

                    visited.Add(neighbor);
                    roomIdByCell[neighbor] = roomId;
                    if (visited.Count > sanityCap) return false;

                    if (!doorCells.Contains(neighbor)) queue.Enqueue(neighbor);
                }
            }

            return true;
        }
    }
}

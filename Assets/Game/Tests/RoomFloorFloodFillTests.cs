using System.Collections.Generic;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    // Pure-algorithm coverage for RoomFloorFloodFill — no scene, no prefabs,
    // no colliders, just hand-authored Layout/wallCells/doorCells data. This
    // is deliberately where the tricky logic (merge-on-gap, split-into-two-
    // pockets, door-stops-propagation, the sanity cap) gets tested, since it
    // needs no scene setup at all (design-review outcome, 2026-09-04: keep
    // the scene-reading half and the pure BFS half separate so this class of
    // bug is testable with plain data).
    public class RoomFloorFloodFillTests
    {
        [Test]
        public void TwoRoomsSeparatedByAGap_Merge()
        {
            // Room_A: (0,0)-(0,2). Room_B: (2,0)-(2,2). Cell (1,y) between them
            // is neither a wall nor a door for y=1 (a hand-removed wall), but
            // IS a wall for y=0 and y=2 — a single-cell gap, not a wide-open
            // field. Fully enclosed by EnclosingRing so the flood can't escape
            // around the outside and hit unrelated (untested) territory.
            var layout = new Layout
            {
                Rooms =
                {
                    Room("Room_A", (0, 0), (0, 1), (0, 2)),
                    Room("Room_B", (2, 0), (2, 1), (2, 2)),
                }
            };
            var wallCells = EnclosingRing(0, 2, 0, 2);
            wallCells.Add(new Vector2Int(1, 0));
            wallCells.Add(new Vector2Int(1, 2));
            var doorCells = new HashSet<Vector2Int>();

            var result = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCells, doorCells);

            Assert.AreEqual(result[new Vector2Int(0, 1)], result[new Vector2Int(1, 1)]);
            Assert.AreEqual(result[new Vector2Int(1, 1)], result[new Vector2Int(2, 1)]);
        }

        [Test]
        public void TwoRoomsSeparatedByADoor_DoNotMerge()
        {
            var layout = new Layout
            {
                Rooms =
                {
                    Room("Room_A", (0, 0), (0, 1), (0, 2)),
                    Room("Room_B", (2, 0), (2, 1), (2, 2)),
                }
            };
            var wallCells = EnclosingRing(0, 2, 0, 2);
            wallCells.Add(new Vector2Int(1, 0));
            wallCells.Add(new Vector2Int(1, 2));
            var doorCells = new HashSet<Vector2Int> { new(1, 1) }; // an ordinary doorway, not a gap

            var result = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCells, doorCells);

            Assert.AreEqual("Room_A", result[new Vector2Int(0, 1)]);
            Assert.AreEqual("Room_B", result[new Vector2Int(2, 1)]);
            Assert.AreNotEqual(result[new Vector2Int(0, 1)], result[new Vector2Int(2, 1)]);
            Assert.IsTrue(result.ContainsKey(new Vector2Int(1, 1)), "The door cell itself should still be floor.");
        }

        [Test]
        public void RoomSplitByAHandAddedWall_BothPocketsKeepTheirOriginalRoomId()
        {
            // Room_A originally spans (0,0)-(2,0), but a wall was hand-added
            // at (1,0), splitting it into two disconnected 1-cell pockets.
            var layout = new Layout
            {
                Rooms = { Room("Room_A", (0, 0), (1, 0), (2, 0)) }
            };
            var wallCells = EnclosingRing(0, 2, 0, 0);
            wallCells.Add(new Vector2Int(1, 0));
            var doorCells = new HashSet<Vector2Int>();

            var result = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCells, doorCells);

            Assert.AreEqual("Room_A", result[new Vector2Int(0, 0)]);
            Assert.AreEqual("Room_A", result[new Vector2Int(2, 0)]);
            Assert.IsFalse(result.ContainsKey(new Vector2Int(1, 0)), "The hand-added wall cell itself must not be floor.");
        }

        // The full 4-way-blocking ring one cell beyond a rectangle's bounds —
        // every cell orthogonally reachable from the rectangle's own boundary
        // going outward, so a flood-fill seeded inside can never escape past
        // it. Cells strictly inside (minX..maxX, minY..maxY) are excluded;
        // callers add any internal separator walls (e.g. a shared wall
        // between two rooms) on top of this.
        private static HashSet<Vector2Int> EnclosingRing(int minX, int maxX, int minY, int maxY)
        {
            var ring = new HashSet<Vector2Int>();
            for (int x = minX - 1; x <= maxX + 1; x++)
            for (int y = minY - 1; y <= maxY + 1; y++)
            {
                bool inside = x >= minX && x <= maxX && y >= minY && y <= maxY;
                if (!inside) ring.Add(new Vector2Int(x, y));
            }

            return ring;
        }

        [Test]
        public void SeedCellWalledOffEntirely_RoomGetsNoFloor()
        {
            var layout = new Layout
            {
                Rooms = { Room("Room_A", (0, 0)) }
            };
            var wallCells = new HashSet<Vector2Int> { new(0, 0) }; // the room's only cell got walled over
            var doorCells = new HashSet<Vector2Int>();

            var result = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, wallCells, doorCells);

            Assert.IsEmpty(result);
        }

        [Test]
        public void NoWallGeometryAtAll_StopsAtSanityCapInsteadOfHanging()
        {
            var layout = new Layout
            {
                Rooms = { Room("Room_A", (0, 0)) }
            };

            LogAssert.ignoreFailingMessages = true;
            var result = RoomFloorFloodFill.ComputeFloorCellsByRoom(layout, new HashSet<Vector2Int>(), new HashSet<Vector2Int>());
            LogAssert.ignoreFailingMessages = false;

            // Sanity cap is Math.Max(500, totalFootprintCells * 20); with a
            // 1-cell Layout that's 500 — assert it actually stopped there
            // instead of running away.
            Assert.LessOrEqual(result.Count, 501);
            Assert.Greater(result.Count, 1, "Should have flooded well past the single seed cell before the cap kicked in.");
        }

        private static LayoutRoomNode Room(string roomId, params (int x, int y)[] cells)
        {
            var node = new LayoutRoomNode { RoomId = roomId };
            foreach (var (x, y) in cells)
                node.FootprintCells.Add(new Vector2Int(x, y));
            return node;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Regression/behavior coverage for SCARAB-014's Isolated Pocket + Spare Door
    // generation (see CONTEXT.md's "Curated Reachability Gaps" section and the
    // published "Curated Reachability Survey" design artifact, 2026-09-05).
    // Asserts entirely against BspLayoutGenerator.Generate's returned Layout —
    // the only public seam for generation behavior.
    public class BspLayoutGeneratorPocketTests
    {
        private static GenerationParameters BaseParameters(int seed) => new()
        {
            Seed = seed,
            RoomCount = 8,
            PuzzleRoomCount = 1,
            OpenSpaceCount = 1,
            MinEntryExitDistance = 1,
            LoopCount = 1,
        };

        // A Pocket is a genuine sub-level (Michael, 2026-09-05 follow-up): spatially
        // detached from the main blob, not merely connectivity-cut-off while still
        // sitting in the main level's own footprint — otherwise two rooms can share
        // a wall with nothing marking the boundary as sealed, which reads as "still
        // part of the same blob" in any flat top-down view.
        // Confirmed empirically, 2026-09-06 (15 seeds, IsolatedPocketCount=6):
        // a single Pocket's own area could run well past the main blob's total
        // area before this cap — a Pocket is meant to be smaller side content,
        // not outweigh the level it's attached to. The combined total across
        // several Pockets is explicitly allowed to exceed the main blob's total
        // (Michael, 2026-09-06: "the total of all pockets can of course be
        // greater... but every pocket by itself should not be") — only a single
        // Pocket individually outweighing the whole level is prevented.
        [Test]
        public void EachIndividualPocketArea_NeverExceedsTotalMainBlobArea([Values(1, 3, 5, 10)] int seed)
        {
            var parameters = BaseParameters(seed);
            parameters.IsolatedPocketCount = 6;

            var layout = BspLayoutGenerator.Generate(parameters);
            var pocketIds = new HashSet<string>(layout.Pockets.SelectMany(p => p.RoomIds));

            long mainTotal = 0;
            var pocketAreaByRoomId = new Dictionary<string, long>();
            foreach (var room in layout.Rooms)
            {
                if (pocketIds.Contains(room.RoomId)) pocketAreaByRoomId[room.RoomId] = room.FootprintCells.Count;
                else mainTotal += room.FootprintCells.Count;
            }

            foreach (var pocket in layout.Pockets)
            {
                long thisPocketArea = pocket.RoomIds.Sum(id => pocketAreaByRoomId[id]);
                Assert.LessOrEqual(thisPocketArea, mainTotal,
                    $"seed {seed}: {pocket.PocketId}'s own area ({thisPocketArea}) must not exceed the main " +
                    $"blob's own total area ({mainTotal}) — it's a pocket, not a second level.");
            }
        }

        // Measured empirically, 2026-09-06 (10 seeds, RoomCount=12,
        // IsolatedPocketCount=4): main-level room areas ranged 18-196 cells;
        // Pocket room areas were capped at 12-60, clustering repeatedly on the
        // same handful of values (many pockets landing on exactly 54 or 60) —
        // Pockets were never scaled relative to the main level's own room
        // sizes at all, just a small fixed area regardless of level scale.
        [Test]
        public void PocketRoomSizes_ScaleWithAndVaryRelativeToMainLevelRoomSizes()
        {
            var mainAreas = new List<int>();
            var pocketAreas = new List<int>();

            for (int seed = 1; seed <= 10; seed++)
            {
                var parameters = BaseParameters(seed);
                parameters.RoomCount = 12;
                parameters.OpenSpaceCount = 2;
                parameters.IsolatedPocketCount = 4;

                var layout = BspLayoutGenerator.Generate(parameters);
                var pocketIds = new HashSet<string>(layout.Pockets.SelectMany(p => p.RoomIds));

                foreach (var room in layout.Rooms)
                {
                    if (room.Role == RoomRole.Margin || room.Role == RoomRole.Corridor) continue;
                    if (pocketIds.Contains(room.RoomId)) pocketAreas.Add(room.FootprintCells.Count);
                    else mainAreas.Add(room.FootprintCells.Count);
                }
            }

            Assert.IsNotEmpty(pocketAreas);
            double mainAvg = mainAreas.Average();
            double pocketAvg = pocketAreas.Average();

            Assert.Greater(pocketAvg, mainAvg * 0.4,
                "Pocket rooms should scale with the main level's own room sizes, not sit capped near a small " +
                "fixed ceiling regardless of how big the main level's rooms got.");
            Assert.Greater(pocketAreas.Max(), mainAreas.Max() * 0.5,
                "At least some Pocket rooms should be able to approach the main level's largest room sizes, " +
                "not top out at a fixed small ceiling.");
        }

        // A Pocket room is eligible for every generation pass, same as any other
        // room (Michael, 2026-09-05 grilling: "isolated rooms can be anything
        // just like all the other rooms") — including shape/size variety, not
        // just plain BSP-leaf rectangles. Swept across many seeds since which
        // exact pocket gets which treatment is randomized.
        [Test]
        public void PocketRooms_CanBeOpenSpaceOrIrregular_LikeAnyOtherRoom()
        {
            bool foundOpenSpacePocketRoom = false;
            bool foundIrregularPocketRoom = false;

            for (int seed = 1; seed <= 60 && !(foundOpenSpacePocketRoom && foundIrregularPocketRoom); seed++)
            {
                var parameters = BaseParameters(seed);
                parameters.IsolatedPocketCount = 3;
                parameters.OpenSpaceCount = 2;
                parameters.IrregularRoomCount = 2;

                var layout = BspLayoutGenerator.Generate(parameters);
                var pocketRoomIds = new HashSet<string>(layout.Pockets.SelectMany(p => p.RoomIds));

                foreach (var room in layout.Rooms)
                {
                    if (!pocketRoomIds.Contains(room.RoomId)) continue;
                    if (room.Role == RoomRole.OpenSpace) foundOpenSpacePocketRoom = true;

                    int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
                    foreach (var cell in room.FootprintCells)
                    {
                        minX = Mathf.Min(minX, cell.x);
                        maxX = Mathf.Max(maxX, cell.x);
                        minY = Mathf.Min(minY, cell.y);
                        maxY = Mathf.Max(maxY, cell.y);
                    }

                    int boundingArea = (maxX - minX + 1) * (maxY - minY + 1);
                    if (room.FootprintCells.Count < boundingArea) foundIrregularPocketRoom = true;
                }
            }

            Assert.IsTrue(foundOpenSpacePocketRoom,
                "Across many pocket generations, at least one Pocket room should end up OpenSpace-merged.");
            Assert.IsTrue(foundIrregularPocketRoom,
                "Across many pocket generations, at least one Pocket room should get a carved notch " +
                "(irregular footprint).");
        }

        // Seed/params match the live repro that first surfaced the bug (Michael,
        // 2026-09-05): Pocket_000 (Room_000+Room_002) directly bordered Room_004.
        [Test]
        public void PocketRooms_ShareNoBorderWithAnyMainGraphRoom()
        {
            var parameters = new GenerationParameters
            {
                Seed = 251410180,
                RoomCount = 5,
                PuzzleRoomCount = 2,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 3,
                LoopCount = 2,
                CorridorCount = 2,
                CorridorMinLength = 4,
                CorridorMaxLength = 8,
                IrregularRoomCount = 2,
                SpareDoorCount = 2,
                IsolatedPocketCount = 1
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var pocketRoomIds = new HashSet<string>(layout.Pockets.SelectMany(p => p.RoomIds));
            Assert.IsNotEmpty(pocketRoomIds, "Repro parameters should produce at least one pocket room.");

            var mainRooms = layout.Rooms.FindAll(r => !pocketRoomIds.Contains(r.RoomId));
            var pocketRooms = layout.Rooms.FindAll(r => pocketRoomIds.Contains(r.RoomId));

            int mainMaxX = int.MinValue;
            foreach (var room in mainRooms)
            foreach (var cell in room.FootprintCells)
                mainMaxX = Mathf.Max(mainMaxX, cell.x);

            int pocketMinX = int.MaxValue;
            foreach (var room in pocketRooms)
            foreach (var cell in room.FootprintCells)
                pocketMinX = Mathf.Min(pocketMinX, cell.x);

            // A gap, not just "no shared cell" — a Pocket reading as "still part of
            // the same blob" in a flat top-down view is exactly the bug: bounding
            // boxes touching or overlapping looks identical to "same area" even
            // when no single cell is literally adjacent.
            Assert.Greater(pocketMinX, mainMaxX + 1,
                "A Pocket's bounding area must sit clearly apart from the main blob's, not merely avoid sharing " +
                "a wall — it's a detached sub-level, not an unreachable room embedded in the same footprint.");
        }

        [Test]
        public void IsolatedPocketCount_One_ProducesExactlyOnePocket()
        {
            var parameters = BaseParameters(42);
            parameters.IsolatedPocketCount = 1;

            var layout = BspLayoutGenerator.Generate(parameters);

            Assert.AreEqual(1, layout.Pockets.Count,
                "IsolatedPocketCount = 1 should produce exactly one PocketInfo.");
        }

        [Test]
        public void PocketRooms_AreUnreachableFromTheEntryRoomViaOrdinaryConnections()
        {
            var parameters = BaseParameters(42);
            parameters.IsolatedPocketCount = 1;

            var layout = BspLayoutGenerator.Generate(parameters);
            var pocketRoomIds = new HashSet<string>(layout.Pockets.SelectMany(p => p.RoomIds));
            Assert.IsNotEmpty(pocketRoomIds, "Repro parameters should produce at least one pocket room.");

            var entry = layout.Rooms.Find(r => r.IsEntry);
            Assert.NotNull(entry, "Layout should have an entry room.");

            var adjacency = new Dictionary<string, List<string>>();
            foreach (var room in layout.Rooms) adjacency[room.RoomId] = new List<string>();
            foreach (var c in layout.Connections)
            {
                if (c.ToRoomId == null) continue; // Spare/Pocket door — no ordinary destination yet.
                adjacency[c.FromRoomId].Add(c.ToRoomId);
                adjacency[c.ToRoomId].Add(c.FromRoomId);
            }

            var reached = new HashSet<string> { entry.RoomId };
            var queue = new Queue<string>();
            queue.Enqueue(entry.RoomId);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in adjacency[current])
                    if (reached.Add(neighbor))
                        queue.Enqueue(neighbor);
            }

            foreach (var pocketRoomId in pocketRoomIds)
                Assert.IsFalse(reached.Contains(pocketRoomId),
                    $"{pocketRoomId} is a Pocket room and must not be reachable from the entry room via " +
                    "ordinary connections.");
        }

        [Test]
        public void Pocket_EntranceAndExitDoorwaysExist_WithNoDestinationYet()
        {
            var parameters = BaseParameters(42);
            parameters.IsolatedPocketCount = 1;

            var layout = BspLayoutGenerator.Generate(parameters);
            var pocket = layout.Pockets[0];

            Assert.NotNull(pocket.EntranceDoorwayId, "Pocket should have an Entrance doorway id.");
            Assert.NotNull(pocket.ExitDoorwayId, "Pocket should have an Exit doorway id.");

            var entranceConnection = layout.Connections.Find(c => c.DoorwayId == pocket.EntranceDoorwayId);
            var exitConnection = layout.Connections.Find(c => c.DoorwayId == pocket.ExitDoorwayId);

            Assert.NotNull(entranceConnection, "Entrance doorway id should resolve to a real LayoutConnection.");
            Assert.NotNull(exitConnection, "Exit doorway id should resolve to a real LayoutConnection.");
            Assert.IsNull(entranceConnection.ToRoomId, "Entrance door has no destination until curated.");
            Assert.IsNull(exitConnection.ToRoomId, "Exit door has no destination until curated.");
        }

        // Seed 8 confirmed (empirical sweep, 2026-09-06, after Pockets became
        // detached mini-BSP areas) to produce a size-1 pocket under these repro
        // parameters — see seeds 1/2 below for size-3/2.
        [Test]
        public void SizeOnePocket_EntranceAndExitAreTheSameDoor()
        {
            var parameters = BaseParameters(8);
            parameters.IsolatedPocketCount = 1;

            var layout = BspLayoutGenerator.Generate(parameters);
            var pocket = layout.Pockets[0];

            Assert.AreEqual(1, pocket.RoomIds.Count, "Repro parameters should produce a size-1 pocket.");
            Assert.AreEqual(pocket.EntranceDoorwayId, pocket.ExitDoorwayId,
                "A size-1 Pocket has no separate 'path through' to have two ends of — one door serves both roles.");
        }

        // Seeds 3 and 5 confirmed (empirical sweep, 2026-09-06, after Pockets
        // gained CarveNotches/MergeOpenSpaces eligibility) to produce a size-2
        // pocket under these repro parameters.
        [Test]
        public void MultiRoomPocket_HasDistinctEntranceAndExit_WithAllRoomsMutuallyReachableInternally(
            [Values(3, 5)] int seed)
        {
            var parameters = BaseParameters(seed);
            parameters.IsolatedPocketCount = 1;

            var layout = BspLayoutGenerator.Generate(parameters);
            var pocket = layout.Pockets[0];
            Assert.GreaterOrEqual(pocket.RoomIds.Count, 2, $"seed {seed} should produce a multi-room pocket.");

            Assert.AreNotEqual(pocket.EntranceDoorwayId, pocket.ExitDoorwayId,
                "A multi-room Pocket must have two distinct doors — one is not a stand-in for the other.");

            // BFS the pocket's own internal ordinary connections only.
            var memberIds = new HashSet<string>(pocket.RoomIds);
            var adjacency = new Dictionary<string, List<string>>();
            foreach (var id in memberIds) adjacency[id] = new List<string>();
            foreach (var c in layout.Connections)
            {
                if (c.ToRoomId == null) continue;
                if (!memberIds.Contains(c.FromRoomId) || !memberIds.Contains(c.ToRoomId)) continue;
                adjacency[c.FromRoomId].Add(c.ToRoomId);
                adjacency[c.ToRoomId].Add(c.FromRoomId);
            }

            var reached = new HashSet<string> { pocket.RoomIds[0] };
            var queue = new Queue<string>();
            queue.Enqueue(pocket.RoomIds[0]);
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();
                foreach (var neighbor in adjacency[current])
                    if (reached.Add(neighbor))
                        queue.Enqueue(neighbor);
            }

            foreach (var roomId in pocket.RoomIds)
                Assert.IsTrue(reached.Contains(roomId),
                    $"seed {seed}: {roomId} should be mutually reachable from every other room within its own Pocket.");
        }

        [Test]
        public void SpareDoorCount_Three_ProducesThreeVoidBackedDecorativeDoors()
        {
            var parameters = BaseParameters(42);
            parameters.SpareDoorCount = 3;

            var layout = BspLayoutGenerator.Generate(parameters);
            var spareDoors = layout.Connections.FindAll(c => c.IsSpareDoor);

            Assert.AreEqual(3, spareDoors.Count, "SpareDoorCount = 3 should produce exactly 3 Spare Doors.");

            var claimedCells = new HashSet<Vector2Int>();
            foreach (var room in layout.Rooms)
            foreach (var cell in room.FootprintCells)
                claimedCells.Add(cell);

            var orthogonal = new[] { new Vector2Int(0, 1), new Vector2Int(1, 0), new Vector2Int(0, -1), new Vector2Int(-1, 0) };

            foreach (var door in spareDoors)
            {
                Assert.IsNull(door.ToRoomId, $"{door.DoorwayId} should have no destination until curated.");

                var fromRoom = layout.Rooms.Find(r => r.RoomId == door.FromRoomId);
                Assert.NotNull(fromRoom, $"{door.DoorwayId}'s FromRoomId should resolve to a real room.");
                var fromCells = new HashSet<Vector2Int>(fromRoom.FootprintCells);

                Assert.IsFalse(claimedCells.Contains(door.DoorCell),
                    $"{door.DoorwayId}'s door cell must itself be unclaimed void.");

                // Reconstruct the outward direction (the one where DoorCell - dir
                // sits inside the room) and confirm the cell one step further out
                // is also void — a true dead end, not a real, merely-unselected
                // neighboring room.
                bool foundOutwardDir = false;
                foreach (var dir in orthogonal)
                {
                    if (!fromCells.Contains(door.DoorCell - dir)) continue;
                    foundOutwardDir = true;
                    Assert.IsFalse(claimedCells.Contains(door.DoorCell + dir),
                        $"{door.DoorwayId}: the cell beyond its door must also be void, not a real room.");
                }
                Assert.IsTrue(foundOutwardDir, $"{door.DoorwayId}'s door cell should border its own room.");
            }
        }
    }
}

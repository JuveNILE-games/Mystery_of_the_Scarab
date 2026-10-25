using System.Collections.Generic;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // Regression for the notch-carving inconsistent-gap bug (found
    // 2026-09-22 while live-inspecting seed 555555 in the Editor):
    // BspLayoutGenerator.TryCarveNotch removes a rectangular block from one
    // corner of a room's footprint (CarveNotches, the IrregularRoomCount
    // shape-variety feature) with no awareness of neighboring rooms. When
    // the notch happens to face a room that the un-notched rectangle shared
    // a normal 1-cell wall with, the notch widens that stretch of the
    // boundary to a 2-cell gap instead — RoomShellAssembler then places two
    // independent walls that shift flush toward each other and meet with
    // zero gap, reading as one solid double-thickness dividing wall instead
    // of the single shared wall every OTHER room pair in the level gets.
    //
    // Confirmed live: Room_002/Room_004 share a normal 1-cell wall at
    // z=16, but a 2-cell gap at z=10-15 — exactly the height of a notch
    // carved from Room_002's west edge. Every other room-adjacency pair in
    // that same seed/params bake was a consistent 1-cell wall throughout.
    public class BspLayoutGeneratorNotchAdjacencyTests
    {
        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        [Test]
        public void NoRoomPair_HasBothA1CellSharedWallAndA2CellGap()
        {
            // Exact repro parameters (2026-09-22) — the seed/params this bug
            // was ground-truthed against live in the Unity Editor.
            var parameters = new GenerationParameters
            {
                Seed = 555555, RoomCount = 8, PuzzleRoomCount = 2, OpenSpaceCount = 1,
                MinEntryExitDistance = 3, LoopCount = 2, CorridorCount = 2, CorridorMinLength = 4,
                CorridorMaxLength = 8, IrregularRoomCount = 2, MinLeafSize = 3, SpareDoorCount = 0,
                IsolatedPocketCount = 0,
            };

            var layout = BspLayoutGenerator.Generate(parameters);

            var cellOwner = new Dictionary<Vector2Int, string>();
            foreach (var room in layout.Rooms)
                foreach (var cell in room.FootprintCells)
                    cellOwner[cell] = room.RoomId;

            var oneCellPairs = new HashSet<string>();
            var twoCellPairs = new HashSet<string>();
            var twoCellExamples = new Dictionary<string, Vector2Int>();

            foreach (var kvp in cellOwner)
            {
                var cell = kvp.Key;
                var room = kvp.Value;
                foreach (var dir in FourWayDirs)
                {
                    var n1 = cell + dir;
                    if (cellOwner.ContainsKey(n1)) continue; // must be a wall cell to count as a gap

                    if (cellOwner.TryGetValue(n1 + dir, out var otherRoom1) && otherRoom1 != room)
                    {
                        oneCellPairs.Add(PairKey(room, otherRoom1));
                        continue;
                    }

                    var n2 = n1 + dir;
                    if (cellOwner.ContainsKey(n2)) continue; // n2 must also be a wall cell for a clean 2-cell gap
                    if (cellOwner.TryGetValue(n2 + dir, out var otherRoom2) && otherRoom2 != room)
                    {
                        var key = PairKey(room, otherRoom2);
                        twoCellPairs.Add(key);
                        if (!twoCellExamples.ContainsKey(key)) twoCellExamples[key] = cell;
                    }
                }
            }

            foreach (var pair in oneCellPairs)
            {
                Assert.IsFalse(twoCellPairs.Contains(pair),
                    $"Room pair {pair} shares a normal 1-cell wall somewhere, but also has a 2-cell gap " +
                    $"elsewhere (near {(twoCellExamples.TryGetValue(pair, out var ex) ? ex.ToString() : "?")}) — " +
                    $"a notch likely widened part of what should be one consistent shared wall.");
            }

            Assert.Greater(oneCellPairs.Count, 0,
                "No 1-cell-shared-wall room pairs were found for this seed/params — the test no longer " +
                "exercises the case it was written to guard, has the layout drifted?");
        }

        private static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    }
}

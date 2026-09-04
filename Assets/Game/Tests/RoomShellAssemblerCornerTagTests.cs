using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Regression for the "inner corner doesn't line up / clips through the
    // door" bug (SCARAB-MAP-01 follow-up, 2026-09-04, found live selecting a
    // WallCornerPrefab_Grid in the scene). Root cause: wallCells detection
    // recorded a flat "is this neighbor void-and-near-something" boolean, so
    // a cell bordering exactly one room directly (an unambiguous straight
    // exterior boundary — Room_004 in the live repro) could still pick up
    // extra Wall tags from a completely unrelated feature's corner sitting
    // diagonally nearby (a corridor's and a margin's own end-caps, in the
    // repro), corrupting a clean 2-opposite-tag Straight pattern into a
    // 3-tag one that a loosely-specified Corner rule matched instead. Fixed
    // by tagging each wall cell with which room(s) it actually borders and
    // requiring a shared room before two cells count as continuing the same
    // run — see RoomShellAssembler.BuildShells's primaryRoomsByCell.
    //
    // Needs a real bake against the project's actual WallPrefabRuleSet (not
    // a synthetic tag check): the bug was specifically that the wrong piece
    // got chosen by the real resolver, and a synthetic proxy could hide that
    // the ruleset itself would still (wrongly) accept the corrupted pattern.
    public class RoomShellAssemblerCornerTagTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string CornerPrefabName = "WallCornerPrefab_Grid";

        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        [Test]
        public void UnambiguousExteriorBoundaryCells_NeverResolveToACornerPiece()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            // Exact repro parameters (2026-09-04): the seed/params baked into
            // _CornerRotationTest where the bug was found live.
            var parameters = new GenerationParameters
            {
                Seed = 1399816449,
                RoomCount = 5,
                PuzzleRoomCount = 2,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 2,
                LoopCount = 1,
                CorridorCount = 2,
                CorridorMinLength = 5,
                CorridorMaxLength = 10,
                IrregularRoomCount = 3,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = null, CellSize = 1f };

            var tempParent = new GameObject("RoomShellAssemblerCornerTagTests_TempParent");
            try
            {
                var cellByInstance = RoomShellAssembler.BuildShells(layout, shellConfig, tempParent.transform);

                var cellOwner = new Dictionary<Vector2Int, string>();
                foreach (var room in layout.Rooms)
                foreach (var cell in room.FootprintCells)
                    cellOwner[cell] = room.RoomId;

                // Scoped to rooms that are still a perfect, unmodified BSP-leaf
                // rectangle: CarveNotches and MergeOpenSpaces each have their
                // own separate, already-deferred instance of "an irregular
                // room shape can produce a cell with more than one tagged
                // side" (confirmed live: Room_004 here is an OpenSpace merge
                // at 82% bounding-box fill, and cell (14,25) sits in the
                // resulting internal notch/seam, not near any corridor or
                // margin at all). That's a different root cause from the one
                // this fix targets and stays out of scope for it.
                var rectangularRooms = new HashSet<string>();
                foreach (var room in layout.Rooms)
                    if (IsPerfectRectangle(room))
                        rectangularRooms.Add(room.RoomId);

                foreach (var kvp in cellByInstance)
                {
                    var instance = kvp.Key;
                    var cell = kvp.Value;
                    if (instance == null) continue;
                    if (!IsUnambiguousExteriorBoundary(cell, cellOwner, rectangularRooms, out var boundaryRoom)) continue;

                    var marker = instance.GetComponent<PrefabBrushObject>();
                    var prefabName = marker != null && marker.SourcePrefab != null ? marker.SourcePrefab.name : "?";
                    Assert.AreNotEqual(CornerPrefabName, prefabName,
                        $"cell {cell} borders exactly one room ({boundaryRoom}, a perfect rectangle) with a clean " +
                        "opposite void side (an unambiguous straight exterior boundary) but resolved to a corner piece.");
                }
            }
            finally
            {
                Object.DestroyImmediate(tempParent);
            }
        }

        // Mirrors RoomShellAssembler.DetermineOutwardDir's own definition of
        // an unambiguous exterior boundary: exactly one 4-way room neighbor,
        // with the opposite side open (not itself a room) — restricted here
        // to neighbors that are still a perfect rectangle (see rectangularRooms
        // above).
        private static bool IsUnambiguousExteriorBoundary(Vector2Int cell, Dictionary<Vector2Int, string> cellOwner,
            HashSet<string> rectangularRooms, out string boundaryRoom)
        {
            boundaryRoom = null;
            Vector2Int? inward = null;
            foreach (var dir in FourWayDirs)
            {
                if (!cellOwner.TryGetValue(cell + dir, out var owner)) continue;
                if (inward.HasValue) return false;
                inward = dir;
                boundaryRoom = owner;
            }

            if (!inward.HasValue || !rectangularRooms.Contains(boundaryRoom)) return false;
            return !cellOwner.ContainsKey(cell - inward.Value);
        }

        private static bool IsPerfectRectangle(LayoutRoomNode room)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            foreach (var cell in room.FootprintCells)
            {
                if (cell.x < minX) minX = cell.x;
                if (cell.x > maxX) maxX = cell.x;
                if (cell.y < minY) minY = cell.y;
                if (cell.y > maxY) maxY = cell.y;
            }

            int area = (maxX - minX + 1) * (maxY - minY + 1);
            return room.FootprintCells.Count == area;
        }
    }
}

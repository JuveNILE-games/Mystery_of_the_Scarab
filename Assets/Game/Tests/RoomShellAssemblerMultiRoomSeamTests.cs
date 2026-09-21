using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Regression for the multi-room wall-cell seam misclassification (Plan 001,
    // 2026-09-21): a wall cell bordering two distinct rooms/margins directly
    // (their corners landing on the same or adjacent cells across a narrow
    // shared gap) looked identical to PrefabRuleResolver's flat 4-tag pattern
    // as a genuine single-room T-junction branch. Fixed by routing multi-
    // room-seam cells to a dedicated MultiRoomSeamPrefab before the resolver
    // is ever consulted — see RoomShellAssembler.TryComputeMultiRoomSeamRotation.
    //
    // CORRECTED 2026-09-21 (live Scene-view + world-space AABB verification):
    // "2+ distinct rooms" is not by itself the defect class. A cell with
    // exactly 2 continuing wall-cell directions at 90° (an L-shaped seam) is
    // the real JunctionWithCorner case. A cell with 3 continuing directions
    // (e.g. seed 1959606433's cell (14,-1): two rooms' walls converging into
    // one shared wall continuing in a third direction) is a genuine symmetric
    // T — confirmed via combined-mesh world AABB touching cleanly on every
    // side — and correctly stays on WallTjunctionPrefab via the normal
    // resolver path. TryComputeMultiRoomSeamRotation already encodes this
    // (bails when continuing.Count != 2); this test now asserts the same
    // condition instead of the broader, wrong "any 2+-room cell" premise.
    public class RoomShellAssemblerMultiRoomSeamTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string SeamPrefabPath = "Assets/Game/World/Prefabs/Environment/JunctionWithCorner.prefab";
        private const string SeamPrefabName = "JunctionWithCorner";

        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0)
        };

        private static readonly Vector2Int[] EightWayDirs =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1)
        };

        [Test]
        public void MultiRoomSeamCells_ResolveToTheSeamPrefab_SingleRoomCellsDoNot()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            var seamPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SeamPrefabPath);
            Assert.NotNull(seamPrefab, $"Could not load {SeamPrefabPath} — is the path still correct?");

            // Exact repro parameters (2026-09-21) — the seed this plan was
            // verified against live in the Unity Editor Scene view.
            var parameters = new GenerationParameters
            {
                Seed = 1959606433,
                RoomCount = 8,
                PuzzleRoomCount = 2,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 3,
                LoopCount = 2,
                CorridorCount = 2,
                CorridorMinLength = 4,
                CorridorMaxLength = 8,
                IrregularRoomCount = 2,
                MinLeafSize = 3,
                SpareDoorCount = 0,
                IsolatedPocketCount = 0,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig
            {
                RuleSet = ruleSet, DoorPrefab = null, MultiRoomSeamPrefab = seamPrefab, CellSize = 1f
            };

            var tempParent = new GameObject("RoomShellAssemblerMultiRoomSeamTests_TempParent");
            try
            {
                var cellByInstance = RoomShellAssembler.BuildShells(layout, shellConfig, tempParent.transform);
                var primaryRoomsByCell = ComputePrimaryRoomsByCell(layout);

                var knownSingleRoomCells = new HashSet<Vector2Int>
                {
                    new(24, 6), new(30, 6)
                };

                int multiRoomSeamCellsChecked = 0;

                foreach (var kvp in cellByInstance)
                {
                    var instance = kvp.Key;
                    var cell = kvp.Value;
                    if (instance == null) continue;
                    if (!primaryRoomsByCell.TryGetValue(cell, out var ownRooms)) continue;

                    var marker = instance.GetComponent<PrefabBrushObject>();
                    var prefabName = marker != null && marker.SourcePrefab != null ? marker.SourcePrefab.name : "?";

                    if (ownRooms.Count >= 2)
                    {
                        multiRoomSeamCellsChecked++;
                        if (IsLShapedSeam(cell, primaryRoomsByCell))
                        {
                            Assert.AreEqual(SeamPrefabName, prefabName,
                                $"cell {cell} borders {ownRooms.Count} distinct rooms/margins with exactly 2 " +
                                $"continuing directions at 90° (an L-shaped seam) but resolved to \"{prefabName}\" " +
                                $"instead of \"{SeamPrefabName}\".");
                        }
                        else
                        {
                            Assert.AreNotEqual(SeamPrefabName, prefabName,
                                $"cell {cell} borders {ownRooms.Count} distinct rooms/margins but is not an " +
                                $"L-shaped (2-continuing-direction) seam, so it should never resolve to " +
                                $"\"{SeamPrefabName}\" — it resolved to it anyway.");
                        }
                    }

                    if (knownSingleRoomCells.Contains(cell))
                    {
                        Assert.AreEqual(1, ownRooms.Count,
                            $"cell {cell} was expected to be a known single-room cell for this seed/params, " +
                            $"but borders {ownRooms.Count} distinct rooms/margins — has the layout drifted?");
                        Assert.AreNotEqual(SeamPrefabName, prefabName,
                            $"cell {cell} is a genuine single-room cell but resolved to \"{SeamPrefabName}\".");
                    }
                }

                Assert.Greater(multiRoomSeamCellsChecked, 0,
                    "No multi-room-seam cells were found for this seed/params — the test no longer exercises the " +
                    "case it was written to guard, has the layout drifted?");
            }
            finally
            {
                Object.DestroyImmediate(tempParent);
            }
        }

        // Independent mirror of RoomShellAssembler.TryComputeMultiRoomSeamRotation's
        // own condition (continuing.Count == 2, not an opposite pair) — deliberately
        // not calling that private method reflectively.
        private static bool IsLShapedSeam(Vector2Int cell, Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell)
        {
            var ownRooms = primaryRoomsByCell[cell];
            var continuing = new List<int>();
            for (int i = 0; i < FourWayDirs.Length; i++)
            {
                var neighbor = cell + FourWayDirs[i];
                if (primaryRoomsByCell.TryGetValue(neighbor, out var neighborRooms) && neighborRooms.Overlaps(ownRooms))
                    continuing.Add(i);
            }

            if (continuing.Count != 2) return false;
            return (continuing[0] + 2) % 4 != continuing[1]; // exclude opposite pairs (straight runs)
        }

        // Independent reconstruction of RoomShellAssembler.BuildShells's own
        // primaryRoomsByCell — deliberately not calling any private
        // RoomShellAssembler method reflectively, mirroring how
        // RoomShellAssemblerCornerTagTests recomputes cellOwner.
        private static Dictionary<Vector2Int, HashSet<string>> ComputePrimaryRoomsByCell(Layout layout)
        {
            var cellOwner = new Dictionary<Vector2Int, string>();
            foreach (var room in layout.Rooms)
            foreach (var cell in room.FootprintCells)
                cellOwner[cell] = room.RoomId;

            var wallCellRooms = new Dictionary<Vector2Int, HashSet<string>>();
            var directRoomsByCell = new Dictionary<Vector2Int, HashSet<string>>();
            foreach (var ownedCell in cellOwner.Keys)
            {
                var owningRoom = cellOwner[ownedCell];
                foreach (var dir in EightWayDirs)
                    AddRoomMembership(wallCellRooms, ownedCell + dir, owningRoom, cellOwner);
                foreach (var dir in FourWayDirs)
                    AddRoomMembership(directRoomsByCell, ownedCell + dir, owningRoom, cellOwner);
            }

            var primaryRoomsByCell = new Dictionary<Vector2Int, HashSet<string>>();
            foreach (var cell in wallCellRooms.Keys)
                primaryRoomsByCell[cell] = directRoomsByCell.TryGetValue(cell, out var direct) && direct.Count > 0
                    ? direct
                    : wallCellRooms[cell];

            return primaryRoomsByCell;
        }

        private static void AddRoomMembership(Dictionary<Vector2Int, HashSet<string>> membership, Vector2Int cell,
            string owningRoom, Dictionary<Vector2Int, string> cellOwner)
        {
            if (cellOwner.ContainsKey(cell)) return;

            if (!membership.TryGetValue(cell, out var rooms))
            {
                rooms = new HashSet<string>();
                membership[cell] = rooms;
            }

            rooms.Add(owningRoom);
        }
    }
}

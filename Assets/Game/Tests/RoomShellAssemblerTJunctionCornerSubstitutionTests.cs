using System.Collections.Generic;
using Core.Editor.Tools.PrefabBrush.Rules;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Regression for T-junction retirement (2026-09-23): WallTjunctionPrefab's
    // branch connector was measured ~0.32 units off the true cell-boundary
    // midpoint (an authoring flaw), vs. ~0.07 off for Corner — see
    // plans/tjunction-corner-research.md for the grilled tiebreak decision.
    // Every 3-continuing-direction wall cell (what used to match the
    // TJunction rule) now resolves to Corner instead, using a fixed
    // deterministic "clockwise-first" rule: the mandatory direction (the
    // one whose opposite is the single missing direction) pairs with
    // whichever opposite-pair member sits at (mandatory+1)%4 in the
    // N=0,E=1,S=2,W=3 clockwise indexing PrefabRuleResolver itself uses.
    //
    // Ground truth for rotation is the ruleset's own Corner rule, matched
    // via a synthetic 2-tag snapshot — not a hand-derived degree table
    // (this file's RoomShellAssembler.cs has gotten hand-derived rotation
    // conventions wrong twice before; see ResolveFallbackYRotation's own
    // comment).
    public class RoomShellAssemblerTJunctionCornerSubstitutionTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";
        private const string TJunctionPrefabName = "WallTjunctionPrefab";
        private const string CornerPrefabName = "WallCornerPrefab_Grid";

        private static readonly Vector2Int[] FourWayDirs =
        {
            new(0, 1), new(1, 0), new(0, -1), new(-1, 0) // N, E, S, W
        };

        private static readonly Vector2Int[] EightWayDirs =
        {
            new(0, 1), new(1, 1), new(1, 0), new(1, -1),
            new(0, -1), new(-1, -1), new(-1, 0), new(-1, 1)
        };

        [Test]
        public void ThreeContinuingDirectionCells_ResolveToCornerWithClockwiseFirstRotation_NeverTJunction()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            // Same seed/params as RoomShellAssemblerRunReconciliationTests —
            // known (plans/README.md, Item 3) to generate real
            // 3-continuing-direction T-junction cells, e.g. (9,4).
            var parameters = new GenerationParameters
            {
                Seed = 1919335568, RoomCount = 8, PuzzleRoomCount = 2, OpenSpaceCount = 1,
                MinEntryExitDistance = 3, LoopCount = 2, CorridorCount = 2, CorridorMinLength = 4,
                CorridorMaxLength = 8, IrregularRoomCount = 2, MinLeafSize = 3, SpareDoorCount = 0,
                IsolatedPocketCount = 0,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var primaryRoomsByCell = ComputePrimaryRoomsByCell(layout);
            var doorwayCells = ComputeDoorwayCells(layout);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            var tempParent = new GameObject("RoomShellAssemblerTJunctionCornerSubstitutionTests_TempParent");
            try
            {
                var cellByInstance = RoomShellAssembler.BuildShells(layout, shellConfig, tempParent.transform);

                int threeContinuingCellsChecked = 0;

                foreach (var kvp in cellByInstance)
                {
                    var instance = kvp.Key;
                    var cell = kvp.Value;
                    if (instance == null) continue;

                    var marker = instance.GetComponent<PrefabBrushObject>();
                    var prefabName = marker != null && marker.SourcePrefab != null ? marker.SourcePrefab.name : "?";

                    Assert.AreNotEqual(TJunctionPrefabName, prefabName,
                        $"cell {cell} resolved to \"{TJunctionPrefabName}\" — this prefab is retired and must " +
                        "never be placed.");

                    // A door always wins over the T-junction/Corner path in
                    // BuildShells (checked first in its per-cell loop), so a
                    // 3-continuing doorway cell correctly stays a Door.
                    if (doorwayCells.Contains(cell)) continue;

                    if (!TryGetThreeContinuingMandatoryAndKeptPair(cell, primaryRoomsByCell, out var mandatory, out var keptPair))
                        continue;

                    threeContinuingCellsChecked++;

                    var expectedSnapshot = new NeighborSnapshot(4);
                    expectedSnapshot.Tags[mandatory] = ruleSet.RuleTag;
                    expectedSnapshot.Tags[keptPair] = ruleSet.RuleTag;
                    var expected = PrefabRuleResolver.Resolve(ruleSet, expectedSnapshot);
                    Assert.IsTrue(expected.Matched,
                        $"cell {cell}: synthetic 2-tag snapshot (mandatory={mandatory}, keptPair={keptPair}) " +
                        "did not match any rule — has the ruleset's Corner rule changed shape?");

                    Assert.AreEqual(CornerPrefabName, prefabName,
                        $"cell {cell} has 3 continuing directions (was a T-junction match) but resolved to " +
                        $"\"{prefabName}\" instead of \"{CornerPrefabName}\".");

                    // Compose with the prefab's own baked-in local rotation
                    // exactly like RoomShellAssembler.PlacePiece does — the
                    // Corner prefab carries a non-zero baked rotation, so the
                    // raw resolver YRotation alone isn't the final transform.
                    var expectedFinalY = ComposedYRotation(expected.Prefab, expected.YRotation);
                    Assert.AreEqual(expectedFinalY, instance.transform.localRotation.eulerAngles.y, 0.01f,
                        $"cell {cell}: expected clockwise-first rotation {expectedFinalY}° (mandatory dir " +
                        $"index {mandatory}, kept pair index {keptPair}, raw resolver YRotation " +
                        $"{expected.YRotation}°) but got {instance.transform.localRotation.eulerAngles.y}°.");
                }

                Assert.Greater(threeContinuingCellsChecked, 0,
                    "No 3-continuing-direction cells were found for this seed/params — the test no longer " +
                    "exercises the case it was written to guard, has the layout drifted?");
            }
            finally
            {
                Object.DestroyImmediate(tempParent);
            }
        }

        // Independent mirror of RoomShellAssembler's own T-junction detection
        // — deliberately not calling any private RoomShellAssembler method
        // reflectively, matching this test file's siblings
        // (RoomShellAssemblerMultiRoomSeamTests, etc.).
        private static bool TryGetThreeContinuingMandatoryAndKeptPair(Vector2Int cell,
            Dictionary<Vector2Int, HashSet<string>> primaryRoomsByCell, out int mandatory, out int keptPair)
        {
            mandatory = -1;
            keptPair = -1;
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

            mandatory = (missing + 2) % 4; // opposite of the single missing direction
            keptPair = (mandatory + 1) % 4; // clockwise-first tiebreak (grilled, 2026-09-23)
            return true;
        }

        // Mirrors RoomShellAssembler.PlacePiece's own rotation composition
        // (extraRotation * bakedRotation) — a throwaway instantiate/destroy,
        // not a hand-read baked-rotation constant, so this stays correct if
        // the Corner prefab's own authored rotation ever changes.
        private static float ComposedYRotation(GameObject prefab, float yRotation)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            try
            {
                var bakedRotation = instance.transform.localRotation;
                var extraRotation = Quaternion.Euler(0f, yRotation, 0f);
                return (extraRotation * bakedRotation).eulerAngles.y;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static HashSet<Vector2Int> ComputeDoorwayCells(Layout layout)
        {
            var doorwayCells = new HashSet<Vector2Int>();
            foreach (var connection in layout.Connections)
            foreach (var doorCell in connection.DoorCells)
                doorwayCells.Add(doorCell);
            return doorwayCells;
        }

        // Independent reconstruction of RoomShellAssembler.BuildShells's own
        // primaryRoomsByCell — deliberately not calling any private
        // RoomShellAssembler method reflectively, mirroring how
        // RoomShellAssemblerMultiRoomSeamTests recomputes it.
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

using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    // Regression coverage for two bugs found live-baking the corridor fix
    // (SCARAB-MAP-01 follow-up, 2026-09-03):
    //
    // 1. Margin rooms (RoomRole.Margin — structural filler that closes a
    //    corridor's flank, see BspLayoutGeneratorCorridorTests) were briefly
    //    treated as real gameplay rooms: a RoomDefinition asset, a
    //    RoomController, a place in the connectivity graph. That's not what
    //    they're for — they exist purely so the wall-halo has cells to close
    //    against.
    // 2. Most exterior wall pieces ended up parented directly under the level
    //    root instead of their owning room's group. Root cause:
    //    ShellAlignmentPass deliberately shifts every exterior piece by
    //    close to half a cell toward its outward edge, and LevelBaker used to
    //    reverse-engineer a piece's origin cell from that already-shifted
    //    world position — which rounds to the wrong (void) cell for most
    //    exterior walls. Fixed by having RoomShellAssembler.BuildShells hand
    //    back the exact cell each instance was placed for, instead of
    //    recovering it from a mutable transform.
    //
    // Both need a real Bake() — a Layout-level check can't see either issue,
    // since both are about what LevelBaker does with the baked scene
    // hierarchy and asset set, not about Layout shape.
    public class LevelBakerHierarchyTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";

        // Dedicated to this test — deliberately NOT the handoff's repro seed
        // (-705775668), which is Michael's own live hand-editing reference
        // bake (Level_-705775668_Root / _CornerRotationTest.unity):
        // LevelBaker.Bake reuses/overwrites whatever asset already sits at
        // Generated/Level_<seed>/, so baking under his seed here would
        // clobber his reference data. Confirmed to produce margin rooms and
        // a corridor with the same "dense" parameters as the handoff repro.
        private const int TestSeed = 90210555;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            // A GUID, not GetHashCode(): NUnit reuses one fixture instance
            // across all [Test] methods, so the instance hash is identical
            // for every test and collides on the save path.
            _tempScenePath = $"Assets/_TempLevelBakerHierarchyTest_{System.Guid.NewGuid():N}.unity";

            // An untitled, never-dirtied scene (no path, not modified) is the
            // Editor's own idle default, not any real unsaved work — reuse it
            // directly rather than creating additively, which Unity refuses
            // ("Cannot create a new scene additively with an untitled scene
            // unsaved") when that's the only scene open. Anything else (a
            // real saved scene, or one with unsaved edits — e.g. the user's
            // own in-progress work) is left untouched, opening the temp scene
            // alongside it instead.
            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);
        }

        [TearDown]
        public void DestroyTempScene()
        {
            if (!_reusedOriginalScene)
            {
                EditorSceneManager.SetActiveScene(_originalActiveScene);
                EditorSceneManager.CloseScene(_tempScene, true);
            }

            AssetDatabase.DeleteAsset(_tempScenePath);
            AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}");
        }

        [Test]
        public void Bake_NeverTreatsMarginRoomsAsGameplayRooms()
        {
            var layout = BakeDenseRepro(out var levelDef, out var levelRoot);

            int marginCount = 0;
            foreach (var room in layout.Rooms)
                if (room.Role == RoomRole.Margin)
                    marginCount++;
            Assert.Greater(marginCount, 0, "Repro parameters should produce at least one margin room.");

            foreach (var roomDef in levelDef.rooms)
                Assert.IsFalse(roomDef.roomId.StartsWith("Margin_"),
                    $"{roomDef.roomId} is a margin room but got baked as a real RoomDefinition.");

            var controllers = levelRoot.GetComponentsInChildren<RoomController>(true);
            Assert.AreEqual(levelDef.rooms.Count, controllers.Length,
                "Every RoomController under the level root should correspond to exactly one real (non-margin) RoomDefinition.");

            foreach (var connection in layout.Connections)
            {
                Assert.IsFalse(connection.FromRoomId.StartsWith("Margin_"),
                    $"Connection references margin room {connection.FromRoomId} — margins must not enter the connectivity graph.");
                Assert.IsFalse(connection.ToRoomId.StartsWith("Margin_"),
                    $"Connection references margin room {connection.ToRoomId} — margins must not enter the connectivity graph.");
            }

            foreach (var room in layout.Rooms)
                if (room.Role == RoomRole.Margin)
                {
                    Assert.IsFalse(room.IsEntry, $"{room.RoomId} is margin filler but got picked as the entry room.");
                    Assert.IsFalse(room.IsExit, $"{room.RoomId} is margin filler but got picked as the exit room.");
                }
        }

        [Test]
        public void Bake_NeverLeavesWallPiecesUngroupedAtLevelRoot()
        {
            BakeDenseRepro(out _, out var levelRoot);

            for (int i = 0; i < levelRoot.transform.childCount; i++)
            {
                var child = levelRoot.transform.GetChild(i);
                var loosePiece = child.GetComponent<PrefabBrushObject>();
                Assert.IsNull(loosePiece,
                    $"{child.name} is a wall/door piece parented directly under the level root instead of a room group.");
            }
        }

        private static Layout BakeDenseRepro(out Game.Systems.LevelSystem.Definitions.LevelDefinition levelDef,
            out GameObject levelRoot)
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            var parameters = new GenerationParameters
            {
                Seed = TestSeed,
                RoomCount = 5,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                CorridorCount = 2,
                CorridorMinLength = 5,
                CorridorMaxLength = 10,
                IrregularRoomCount = 1,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = null, CellSize = 2f };
            levelDef = LevelBaker.Bake(layout, shellConfig, "HierarchyTestRepro");
            levelRoot = GameObject.Find($"Level_{layout.Seed}_Root");
            return layout;
        }
    }
}

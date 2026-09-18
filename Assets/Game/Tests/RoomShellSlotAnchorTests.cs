using System.Linq;
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
    // SCARAB-015 step 1: RoomShellAssembler must stamp a RoomShellSlotAnchor (Cell + Role) onto
    // every wall/door piece it places, since Cell isn't persisted anywhere today — the reconciler
    // (later steps) has nothing to diff against without it.
    public class RoomShellSlotAnchorTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from every other test class's seed (90210555-557 already used).
        private const int TestSeed = 90210558;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;
        private GameObject _parent;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempRoomShellSlotAnchorTest_{System.Guid.NewGuid():N}.unity";

            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);

            _parent = new GameObject("ShellParent");
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
        }

        [Test]
        public void BuildShells_StampsWallRoleAndMatchingCell_OnOrdinaryWallPieces()
        {
            var cellByInstance = BuildTestShells();

            var wallInstances = cellByInstance.Where(kv => kv.Key.GetComponent<DoorwayMarker>() == null).ToList();
            Assert.IsNotEmpty(wallInstances, "Repro parameters should produce at least one ordinary wall piece.");

            foreach (var (instance, cell) in wallInstances)
            {
                var anchor = instance.GetComponent<RoomShellSlotAnchor>();
                Assert.NotNull(anchor, $"{instance.name} has no RoomShellSlotAnchor.");
                Assert.AreEqual(RoomShellSlotRole.Wall, anchor.Role, $"{instance.name} should be Role.Wall.");
                Assert.AreEqual(cell, anchor.Cell, $"{instance.name}'s anchor.Cell must match the cell BuildShells resolved it for.");
            }
        }

        [Test]
        public void BuildShells_StampsDoorRole_OnDoorwayPieces()
        {
            var cellByInstance = BuildTestShells();

            var doorInstances = cellByInstance.Keys.Where(go => go.GetComponent<DoorwayMarker>() != null).ToList();
            Assert.IsNotEmpty(doorInstances, "Repro parameters should produce at least one doorway piece.");

            foreach (var instance in doorInstances)
            {
                var anchor = instance.GetComponent<RoomShellSlotAnchor>();
                Assert.NotNull(anchor, $"{instance.name} has no RoomShellSlotAnchor.");
                Assert.AreEqual(RoomShellSlotRole.Door, anchor.Role, $"{instance.name} should be Role.Door.");
            }
        }

        private System.Collections.Generic.Dictionary<GameObject, Vector2Int> BuildTestShells()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            var parameters = new GenerationParameters
            {
                Seed = TestSeed,
                RoomCount = 6,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                SpareDoorCount = 0,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            return RoomShellAssembler.BuildShells(layout, shellConfig, _parent.transform);
        }
    }
}

using System.Linq;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Definitions;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    // Regression coverage for SCARAB-014's LevelBaker.Bake() guard fix: a Spare
    // Door's LayoutConnection has ToRoomId == null (no destination until a
    // curator pairs it — see ADR 0001, docs/adr/0001-spare-doors-stay-in-
    // layout-connections.md), which the pre-fix Bake() crashed on via an
    // unguarded roomDefsById[connection.ToRoomId] lookup.
    public class LevelBakerSpareConnectionTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from every other test class's seed (90210555, 90210556) so
        // Generated/Level_<seed>/ folders never collide across test classes.
        private const int TestSeed = 90210557;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerSpareConnectionTest_{System.Guid.NewGuid():N}.unity";

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
        public void Bake_DoesNotThrow_WhenLayoutContainsAnUnresolvedSpareDoorConnection()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            var parameters = new GenerationParameters
            {
                Seed = TestSeed,
                RoomCount = 8,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                SpareDoorCount = 1,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var spareDoor = layout.Connections.Find(c => c.IsSpareDoor);
            Assert.NotNull(spareDoor, "Repro parameters should produce at least one Spare Door.");
            Assert.IsNull(spareDoor.ToRoomId, "A Spare Door has no destination until curated.");

            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            Assert.DoesNotThrow(() => LevelBaker.Bake(layout, shellConfig, "SpareConnectionTestRepro"),
                "Bake() must tolerate a Spare Door's unresolved ToRoomId instead of throwing.");

            var levelRoot = GameObject.Find($"Level_{layout.Seed}_Root");
            Assert.NotNull(levelRoot, "Bake() should still have produced the level root.");

            var fromRoomDef = FindRoomDefinition(levelRoot, spareDoor.FromRoomId);
            Assert.NotNull(fromRoomDef, $"{spareDoor.FromRoomId} should still have a RoomDefinition.");
            Assert.IsFalse(fromRoomDef.connections.Any(c => c.doorwayId == spareDoor.DoorwayId),
                "A Spare Door's unresolved connection must be omitted from RoomDefinition.connections, " +
                "not populated with a null/broken targetRoom.");
        }

        private static RoomDefinition FindRoomDefinition(GameObject levelRoot, string roomId)
        {
            foreach (var controller in levelRoot.GetComponentsInChildren<RoomController>(true))
            {
                var def = controller.Definition;
                if (def != null && def.roomId == roomId) return def;
            }

            return null;
        }
    }
}

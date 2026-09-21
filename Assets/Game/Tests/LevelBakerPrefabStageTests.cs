using System.Linq;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    // A real bug found live, 2026-09-21: baking while a level's root prefab is open in Prefab
    // Isolation Mode used to silently operate on a phantom root in whatever scene sat
    // underneath the stage, since EditorSceneManager.GetActiveScene() never resolves to a
    // Prefab Stage's own scene. Harmless before SCARAB-015 (just wasted, invisible objects);
    // catastrophic after it, since Capture on the phantom root always finds nothing live to
    // protect, and a previously-populated KnownSlots for the same seed reads every freshly
    // regenerated slot as "known but missing" — turning a routine re-bake into a mass delete
    // of content Capture never actually saw in the first place.
    public class LevelBakerPrefabStageTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from every other test class's seed (90210555-562 already used).
        private const int TestSeed = 90210563;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;
        private string _prefabPath;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerPrefabStageTest_{System.Guid.NewGuid():N}.unity";

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
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                StageUtility.GoToMainStage();

            var stalePhantom = GameObject.Find($"Level_{TestSeed}_Root");
            if (stalePhantom != null) Object.DestroyImmediate(stalePhantom);

            if (!string.IsNullOrEmpty(_prefabPath))
                AssetDatabase.DeleteAsset(_prefabPath);

            if (!_reusedOriginalScene)
            {
                EditorSceneManager.SetActiveScene(_originalActiveScene);
                EditorSceneManager.CloseScene(_tempScene, true);
            }

            AssetDatabase.DeleteAsset(_tempScenePath);
            AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}");
        }

        [Test]
        public void Bake_OperatesOnThePrefabStagesOwnRoot_NotAPhantomInTheSceneUnderneath()
        {
            _prefabPath = $"Assets/_TempLevelBakerPrefabStageTest_{TestSeed}_Root.prefab";
            var placeholder = new GameObject($"Level_{TestSeed}_Root");
            PrefabUtility.SaveAsPrefabAsset(placeholder, _prefabPath);
            Object.DestroyImmediate(placeholder);

            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            var layout = BspLayoutGenerator.Generate(new GenerationParameters
            {
                Seed = TestSeed,
                RoomCount = 6,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                SpareDoorCount = 0,
            });

            PrefabStageUtility.OpenPrefab(_prefabPath);
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.NotNull(stage, "Prefab Stage should now be open.");

            LevelBaker.Bake(layout, shellConfig, "PrefabStageTest");

            var stageRoot = stage.scene.GetRootGameObjects().FirstOrDefault(r => r.name == $"Level_{TestSeed}_Root");
            Assert.NotNull(stageRoot, "Bake() must populate the prefab actually open in the stage.");
            var wallCount = stageRoot.GetComponentsInChildren<PrefabBrushObject>(true)
                .Count(m => m.PlacementGroup == RoomShellAssembler.WallPlacementGroup);
            Assert.Greater(wallCount, 0, "The prefab stage's own root should have received the baked Room Shell.");

            var phantomInUnderlyingScene = _tempScene.GetRootGameObjects().Any(r => r.name == $"Level_{TestSeed}_Root");
            Assert.IsFalse(phantomInUnderlyingScene,
                "Bake() must not create a phantom root in whatever scene sat underneath the Prefab Stage.");
        }
    }
}

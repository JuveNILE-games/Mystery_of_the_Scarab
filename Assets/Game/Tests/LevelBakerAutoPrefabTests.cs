using System.Linq;
using Game.Systems.LevelGeneration.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Tests
{
    // Levels shouldn't need a scene to generate in at all (Michael, 2026-09-21): whatever scene
    // is active when you click Bake shouldn't matter. EnsureLevelPrefabStageOpen creates an
    // empty Level_{seed}_Root.prefab the first time a seed is baked, then opens it in Prefab
    // Stage isolation every time — LevelBaker.Bake()/BakeFloors()/BakeConnections() already
    // resolve correctly against whatever Prefab Stage is open (GetWorkingScene, the same fix
    // that made this whole workflow possible). EnsureInstanceInScene is the separate, optional
    // convenience: drop an instance into whatever scene was active before, so the level is also
    // visible there without requiring it.
    //
    // Uses the same temp-scene isolation every other LevelBaker test class does (not just
    // "whatever EditorSceneManager.GetActiveScene() happens to return"), specifically because an
    // earlier draft of EnsureInstanceInScene's tests added a real object to the ambient active
    // scene and left it dirty — the EditMode test runner's own scratch scene, in that case,
    // which then broke every other test class's own _reusedOriginalScene check for the rest of
    // that run (Unity refuses to create a scene additively while an unsaved untitled scene is
    // loaded). Isolating to an explicit temp scene avoids touching anything ambient at all.
    public class LevelBakerAutoPrefabTests
    {
        // Distinct from every other test class's seed (90210555-563 already used).
        private const int TestSeed = 90210564;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;
        private string _folder;
        private string _prefabPath;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerAutoPrefabTest_{System.Guid.NewGuid():N}.unity";

            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);

            _folder = $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}";
            _prefabPath = $"{_folder}/Level_{TestSeed}_Root.prefab";
        }

        [TearDown]
        public void DestroyTempScene()
        {
            if (PrefabStageUtility.GetCurrentPrefabStage() != null)
                StageUtility.GoToMainStage();

            if (!_reusedOriginalScene)
            {
                EditorSceneManager.SetActiveScene(_originalActiveScene);
                EditorSceneManager.CloseScene(_tempScene, true);
            }

            AssetDatabase.DeleteAsset(_tempScenePath);
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void EnsureLevelPrefabStageOpen_CreatesAnEmptyPrefabAndOpensIt_WhenNoneExistsYet()
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath),
                "Repro assumption: no prefab should exist yet for this seed.");

            var returnedPath = LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);

            Assert.AreEqual(_prefabPath, returnedPath);
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath),
                "A prefab asset must now exist on disk.");

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.NotNull(stage, "Should now be inside a Prefab Stage.");
            Assert.AreEqual(_prefabPath, stage.assetPath);
            Assert.AreEqual(1, stage.scene.rootCount);
            Assert.AreEqual($"Level_{TestSeed}_Root", stage.scene.GetRootGameObjects()[0].name);
            Assert.AreEqual(0, stage.scene.GetRootGameObjects()[0].transform.childCount,
                "A freshly created prefab should be empty, ready for Bake() to populate.");
        }

        [Test]
        public void EnsureLevelPrefabStageOpen_ReopensTheExistingPrefab_WithoutWipingItsContent()
        {
            LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);
            var firstStage = PrefabStageUtility.GetCurrentPrefabStage();
            var handAddedChild = new GameObject("HandAddedProof");
            handAddedChild.transform.SetParent(firstStage.scene.GetRootGameObjects()[0].transform, false);
            PrefabUtility.SaveAsPrefabAsset(firstStage.scene.GetRootGameObjects()[0], _prefabPath);
            StageUtility.GoToMainStage();

            LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.NotNull(stage);
            Assert.NotNull(stage.scene.GetRootGameObjects()[0].transform.Find("HandAddedProof"),
                "Reopening an already-created prefab must not recreate it empty.");
        }

        [Test]
        public void EnsureLevelPrefabStageOpen_IsANoOp_WhenAlreadyInThatExactStage()
        {
            LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);
            var stageBefore = PrefabStageUtility.GetCurrentPrefabStage();
            var uncommittedChild = new GameObject("UncommittedSinceOpening");
            uncommittedChild.transform.SetParent(stageBefore.scene.GetRootGameObjects()[0].transform, false);

            LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);

            var stageAfter = PrefabStageUtility.GetCurrentPrefabStage();
            Assert.AreSame(stageBefore, stageAfter, "Calling this while already in the right stage must not reopen it.");
            Assert.NotNull(stageAfter.scene.GetRootGameObjects()[0].transform.Find("UncommittedSinceOpening"),
                "An uncommitted change made since opening must survive a redundant call.");
        }

        [Test]
        public void EnsureInstanceInScene_AddsAnInstance_WhenTheSceneHasNoneYet()
        {
            LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);
            StageUtility.GoToMainStage();

            LevelBaker.EnsureInstanceInScene(_tempScene, TestSeed);

            var found = _tempScene.GetRootGameObjects().Count(r => r.name == $"Level_{TestSeed}_Root");
            Assert.AreEqual(1, found);
        }

        [Test]
        public void EnsureInstanceInScene_DoesNotDuplicate_WhenAnInstanceAlreadyExists()
        {
            LevelBaker.EnsureLevelPrefabStageOpen(TestSeed);
            StageUtility.GoToMainStage();

            LevelBaker.EnsureInstanceInScene(_tempScene, TestSeed);
            LevelBaker.EnsureInstanceInScene(_tempScene, TestSeed);

            var found = _tempScene.GetRootGameObjects().Count(r => r.name == $"Level_{TestSeed}_Root");
            Assert.AreEqual(1, found, "A second call must not add a duplicate instance.");
        }
    }
}

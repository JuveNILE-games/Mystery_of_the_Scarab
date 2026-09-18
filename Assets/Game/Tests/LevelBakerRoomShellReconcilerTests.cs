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
    // SCARAB-015 step 5: wires RoomShellReconciler.Capture/Replay into LevelBaker.Bake() for
    // real, against a real generated layout. The legacy-recovery scenario is the point of this
    // step: Level_1399816449 and Level_2125612262 were baked before RoomShellSlotAnchor existed,
    // so their objects carry no anchor and their folders have no RoomShellEditHistory asset yet —
    // this simulates exactly that by baking once, then stripping every anchor and deleting the
    // history asset before hand-editing and re-baking, the same starting state those two levels
    // will actually be in on their first post-upgrade bake.
    public class LevelBakerRoomShellReconcilerTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from every other test class's seed (90210555-559 already used).
        private const int TestSeed = 90210560;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;
        private PrefabRuleSet _ruleSet;
        private GameObject _doorPrefab;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerRoomShellReconcilerTest_{System.Guid.NewGuid():N}.unity";

            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);

            _ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(_ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            _doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(_doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");
        }

        [TearDown]
        public void DestroyTempScene()
        {
            // Both tests in this class bake the same seed. GameObject.Find searches every loaded
            // scene, not just the active one, so a level root left behind by scene reuse (the
            // same _reusedOriginalScene convention LevelBakerSpareConnectionTests uses) can get
            // found by the next test instead of the one it just baked. Destroy it explicitly
            // rather than relying on the scene it lived in going away.
            var staleRoot = GameObject.Find($"Level_{TestSeed}_Root");
            if (staleRoot != null) Object.DestroyImmediate(staleRoot);

            if (!_reusedOriginalScene)
            {
                EditorSceneManager.SetActiveScene(_originalActiveScene);
                EditorSceneManager.CloseScene(_tempScene, true);
            }

            AssetDatabase.DeleteAsset(_tempScenePath);
            AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}");
        }

        [Test]
        public void Bake_CreatesARoomShellEditHistoryAsset_AsPartOfAnOrdinaryBake()
        {
            var layout = GenerateTestLayout();
            var shellConfig = new ShellAssemblyConfig { RuleSet = _ruleSet, DoorPrefab = _doorPrefab, CellSize = 1f };

            Assert.DoesNotThrow(() => LevelBaker.Bake(layout, shellConfig, "ReconcilerWiringTest"));

            var history = AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(
                $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            Assert.NotNull(history, "Bake() must load-or-create the level's RoomShellEditHistory asset.");
            Assert.AreEqual(TestSeed, history.Seed);
            Assert.IsNotEmpty(history.KnownSlots, "The first bake should already know about every Wall/Door slot it just generated.");
        }

        [Test]
        public void Bake_RecoversAHandTweak_OnALevelThatPredatesThisFeature()
        {
            var layout = GenerateTestLayout();
            var shellConfig = new ShellAssemblyConfig { RuleSet = _ruleSet, DoorPrefab = _doorPrefab, CellSize = 1f };

            LevelBaker.Bake(layout, shellConfig, "LegacyRecoveryTest");

            var levelRoot = GameObject.Find($"Level_{layout.Seed}_Root");
            Assert.NotNull(levelRoot);

            var target = levelRoot.GetComponentsInChildren<PrefabBrushObject>(true)
                .First(m => m.PlacementGroup == RoomShellAssembler.WallPlacementGroup &&
                            m.GetComponent<DoorwayMarker>() == null);
            var targetCell = target.GetComponent<RoomShellSlotAnchor>().Cell;
            var originalPosition = target.transform.localPosition;

            // Simulate a level baked before RoomShellSlotAnchor/RoomShellEditHistory existed:
            // no anchors anywhere, and no history asset for this seed.
            foreach (var anchor in levelRoot.GetComponentsInChildren<RoomShellSlotAnchor>(true))
                Object.DestroyImmediate(anchor);
            AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            AssetDatabase.Refresh();

            // The hand edit itself, made on what is now, by construction, unanchored content.
            var tweakedPosition = originalPosition + new Vector3(0.4f, 0f, 0f);
            target.transform.localPosition = tweakedPosition;

            LevelBaker.Bake(layout, shellConfig, "LegacyRecoveryTest");

            var recovered = levelRoot.GetComponentsInChildren<RoomShellSlotAnchor>(true)
                .FirstOrDefault(a => a.Cell == targetCell && a.Role == RoomShellSlotRole.Wall);
            Assert.NotNull(recovered, "The regenerated piece at the tweaked cell must still exist and be anchored.");
            Assert.AreEqual(tweakedPosition, recovered.transform.localPosition,
                "A hand edit made before RoomShellSlotAnchor ever existed must still survive the first post-upgrade bake.");

            var history = AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(
                $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            Assert.NotNull(history, "Recovery must (re)create the history asset even though it didn't exist a moment ago.");
            Assert.IsTrue(history.Edits.Any(e => e.Kind == RoomShellEditKind.Tweak && e.Cell == targetCell),
                "The recovered tweak must be recorded so it survives every bake after this one too.");
        }

        private static Layout GenerateTestLayout()
        {
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
            return BspLayoutGenerator.Generate(parameters);
        }
    }
}

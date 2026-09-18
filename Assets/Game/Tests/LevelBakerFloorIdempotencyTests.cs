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
    // SCARAB-015 step 6: floor is deliberately kept out of the reconciler entirely (Michael,
    // 2026-09-18 — "fine to just ignore floors entirely, baking a floor is trivial if we notice
    // it doesn't line up"). BakeFloors() gets a same/different check instead of an anchor: a
    // room whose required cells haven't changed keeps its existing floor object, hand edits
    // included, untouched by inaction. The check reuses PrefabBrushObject.RuleTag (unused for a
    // floor marker) to hold a cheap fingerprint of the room's cell set — no new component, no
    // RoomShellEditHistory involvement, matching the "floors don't need any of this" call.
    public class LevelBakerFloorIdempotencyTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from every other test class's seed (90210555-560 already used).
        private const int TestSeed = 90210561;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;
        private ShellAssemblyConfig _shellConfig;
        private Layout _layout;
        private GameObject _levelRoot;

        [SetUp]
        public void CreateTempSceneAndBake()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerFloorIdempotencyTest_{System.Guid.NewGuid():N}.unity";

            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);

            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");
            _shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            _layout = BspLayoutGenerator.Generate(new GenerationParameters
            {
                Seed = TestSeed,
                RoomCount = 6,
                PuzzleRoomCount = 1,
                OpenSpaceCount = 1,
                MinEntryExitDistance = 1,
                LoopCount = 1,
                SpareDoorCount = 0,
            });

            LevelBaker.Bake(_layout, _shellConfig, "FloorIdempotencyTest");
            LevelBaker.BakeFloors(_layout, _shellConfig);

            var staleRoot = GameObject.Find($"Level_{TestSeed}_Root");
            Assert.NotNull(staleRoot, "Sanity check on the baked level root before each test's own assertions.");
            _levelRoot = staleRoot;
        }

        [TearDown]
        public void DestroyTempScene()
        {
            // Same cross-scene GameObject.Find pollution risk as LevelBakerRoomShellReconcilerTests.
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
        public void BakeFloors_LeavesAnUnchangedRoomsFloorObjectUntouched_OnASecondCall()
        {
            var floorMarker = AnyFloorMarker();
            var floorGo = floorMarker.gameObject;
            var originalInstanceId = floorGo.GetInstanceID();

            var handAddedDecoration = new GameObject("HandAddedRug");
            handAddedDecoration.transform.SetParent(floorGo.transform, false);

            LevelBaker.BakeFloors(_layout, _shellConfig);

            var stillThere = GameObject.Find($"Level_{TestSeed}_Root")
                .GetComponentsInChildren<PrefabBrushObject>(true)
                .FirstOrDefault(m => m.PlacementGroup == RoomShellAssembler.FloorPlacementGroup &&
                                      m.gameObject.GetInstanceID() == originalInstanceId);
            Assert.NotNull(stillThere, "An unchanged room's floor GameObject must not be destroyed and recreated.");
            Assert.NotNull(stillThere.transform.Find("HandAddedRug"),
                "A hand-added child under the floor must survive a bake that left this room's cells unchanged.");
        }

        [Test]
        public void BakeFloors_RebuildsOnlyTheRoomWhoseFingerprintNoLongerMatches()
        {
            var markers = AllFloorMarkers();
            Assert.GreaterOrEqual(markers.Count, 2, "Need at least two rooms with floors for this test to mean anything.");

            var tampered = markers[0];
            var untouched = markers[1];
            var tamperedName = tampered.name;
            var untouchedName = untouched.name;
            var tamperedInstanceId = tampered.gameObject.GetInstanceID();
            var untouchedInstanceId = untouched.gameObject.GetInstanceID();
            var untouchedFingerprintBefore = untouched.RuleTag;

            tampered.RuleTag = "stale-fingerprint-forcing-a-rebuild";

            LevelBaker.BakeFloors(_layout, _shellConfig);

            var freshMarkers = AllFloorMarkers();
            var stillUntouched = freshMarkers.Single(m => m.name == untouchedName);
            Assert.AreEqual(untouchedInstanceId, stillUntouched.gameObject.GetInstanceID(),
                "A room whose fingerprint still matched must not have been rebuilt.");
            Assert.AreEqual(untouchedFingerprintBefore, stillUntouched.RuleTag);

            var rebuilt = freshMarkers.SingleOrDefault(m => m.name == tamperedName);
            Assert.NotNull(rebuilt, "The tampered room must still have a floor after the rebuild.");
            Assert.AreNotEqual(tamperedInstanceId, rebuilt.gameObject.GetInstanceID(),
                "A room whose recorded fingerprint no longer matches must get a freshly rebuilt floor object.");
            Assert.AreNotEqual("stale-fingerprint-forcing-a-rebuild", rebuilt.RuleTag,
                "The rebuild must stamp the real, current fingerprint, not leave the stale one behind.");
        }

        private PrefabBrushObject AnyFloorMarker() => AllFloorMarkers().First();

        private System.Collections.Generic.List<PrefabBrushObject> AllFloorMarkers()
        {
            return _levelRoot.GetComponentsInChildren<PrefabBrushObject>(true)
                .Where(m => m.PlacementGroup == RoomShellAssembler.FloorPlacementGroup)
                .ToList();
        }
    }
}

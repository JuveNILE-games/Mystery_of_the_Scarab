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
    // Regression coverage for LevelBaker.BakeConnections (SCARAB-012): it must never touch
    // wall/floor geometry, and must be idempotent/convergent — re-running it with a smaller
    // curation list must remove triggers a prior run added, not just accumulate.
    public class LevelBakerConnectionsTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from LevelBakerHierarchyTests.TestSeed so the two test classes never
        // fight over the same Generated/Level_<seed> folder.
        private const int TestSeed = 90210556;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerConnectionsTest_{System.Guid.NewGuid():N}.unity";

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
        public void BakeConnections_PairsMarkersWithoutTouchingWallOrFloorGeometry()
        {
            var layout = BakeRepro(out var levelRoot);
            var (doorwayIdA, doorwayIdB) = TwoRealDoorwayIds(layout);

            var meshFilterCountBefore = levelRoot.GetComponentsInChildren<MeshFilter>(true).Length;
            var prefabBrushCountBefore = levelRoot.GetComponentsInChildren<PrefabBrushObject>(true).Length;

            var curation = ScriptableObject.CreateInstance<LevelConnectionCuration>();
            curation.Seed = layout.Seed;
            curation.Pairings.Add(new CuratedConnectionPairing
            {
                DoorwayIdA = doorwayIdA, DoorwayIdB = doorwayIdB, Kind = ConnectionKind.NonEuclideanHidden
            });
            var transitionDefaults = ScriptableObject.CreateInstance<NonEuclideanTransitionDefaults>();

            LevelBaker.BakeConnections(layout, curation, transitionDefaults);

            Assert.AreEqual(meshFilterCountBefore, levelRoot.GetComponentsInChildren<MeshFilter>(true).Length,
                "BakeConnections must never add/remove wall or floor geometry.");
            Assert.AreEqual(prefabBrushCountBefore, levelRoot.GetComponentsInChildren<PrefabBrushObject>(true).Length,
                "BakeConnections must never add/remove wall/door/floor pieces.");

            var markerA = FindMarker(levelRoot, doorwayIdA);
            var markerB = FindMarker(levelRoot, doorwayIdB);
            var triggerA = markerA.GetComponent<DoorwayTeleportTrigger>();
            var triggerB = markerB.GetComponent<DoorwayTeleportTrigger>();

            Assert.NotNull(triggerA, $"{doorwayIdA} should have gained a DoorwayTeleportTrigger.");
            Assert.NotNull(triggerB, $"{doorwayIdB} should have gained a DoorwayTeleportTrigger.");
            Assert.AreEqual(ConnectionKind.NonEuclideanHidden, triggerA.Kind);
            Assert.AreEqual(ConnectionKind.NonEuclideanHidden, triggerB.Kind);
            Assert.AreSame(triggerB, triggerA.PairedTrigger, "Pairing must be bidirectional.");
            Assert.AreSame(triggerA, triggerB.PairedTrigger, "Pairing must be bidirectional.");
            Assert.AreSame(transitionDefaults, triggerA.TransitionDefaults);
            Assert.AreSame(transitionDefaults, triggerB.TransitionDefaults);

            // Shrink to zero pairings and re-run — this must converge (remove the stale
            // triggers), not just leave them behind from the prior run.
            curation.Pairings.Clear();
            LevelBaker.BakeConnections(layout, curation, transitionDefaults);

            Assert.IsNull(markerA.GetComponent<DoorwayTeleportTrigger>(),
                "Re-running with an emptied curation list must remove a previously-added trigger.");
            Assert.IsNull(markerB.GetComponent<DoorwayTeleportTrigger>(),
                "Re-running with an emptied curation list must remove a previously-added trigger.");
        }

        private static GameObject FindMarker(GameObject root, string doorwayId)
        {
            var marker = root.GetComponentsInChildren<DoorwayMarker>(true)
                .First(m => m.DoorwayId == doorwayId);
            return marker.gameObject;
        }

        private static (string, string) TwoRealDoorwayIds(Layout layout)
        {
            Assert.GreaterOrEqual(layout.Connections.Count, 2,
                "Repro parameters should produce at least 2 connections to pick doorway ids from.");
            return (layout.Connections[0].DoorwayId, layout.Connections[1].DoorwayId);
        }

        private static Layout BakeRepro(out GameObject levelRoot)
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

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
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };
            LevelBaker.Bake(layout, shellConfig, "ConnectionsTestRepro");
            levelRoot = GameObject.Find($"Level_{layout.Seed}_Root");
            return layout;
        }
    }
}

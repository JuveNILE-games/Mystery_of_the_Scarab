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
    // Regression/behavior coverage for SCARAB-014's pocket reachability
    // validation: LevelBaker.BakeConnections must flag (via Debug.LogWarning,
    // never a scene mutation — see the "Curated Reachability Survey" design
    // artifact) any Isolated Pocket that isn't actually reachable from the main
    // graph through curation.Pairings, including multi-hop chains and closed
    // pocket-to-pocket loops that a naive "is every doorway paired" check would
    // get wrong.
    public class LevelBakerPocketReachabilityTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;

        [SetUp]
        public void CreateTempScene()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerPocketReachabilityTest_{System.Guid.NewGuid():N}.unity";

            _reusedOriginalScene = string.IsNullOrEmpty(_originalActiveScene.path) &&
                                    !_originalActiveScene.isDirty;

            _tempScene = _reusedOriginalScene
                ? _originalActiveScene
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

            EditorSceneManager.SaveScene(_tempScene, _tempScenePath);
            EditorSceneManager.SetActiveScene(_tempScene);
        }

        // Seeds below (with RoomCount=24) confirmed via empirical sweep,
        // 2026-09-05: every pocket's Entrance/Exit door actually found a free
        // wall cell to place on — a denser layout can occasionally leave one
        // unplaced (logged, not a bug on its own), which is a separate,
        // untested-here edge case, not what these tests are about.
        private int _bakedSeed;

        [TearDown]
        public void DestroyTempScene()
        {
            if (!_reusedOriginalScene)
            {
                EditorSceneManager.SetActiveScene(_originalActiveScene);
                EditorSceneManager.CloseScene(_tempScene, true);
            }

            AssetDatabase.DeleteAsset(_tempScenePath);
            if (_bakedSeed != 0)
                AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{_bakedSeed}");
        }

        [Test]
        public void SinglePocket_CuratedBackToMainGraph_LogsNoReachabilityWarning()
        {
            var layout = BakeRepro(90210800, isolatedPocketCount: 1);
            var pocket = layout.Pockets[0];
            var anchor = AnyOrdinaryDoorwayId(layout);

            var curation = MakeCuration(layout.Seed,
                (anchor, pocket.EntranceDoorwayId));

            var warnings = CaptureWarnings(() => LevelBaker.BakeConnections(layout, curation, MakeTransitionDefaults()));

            Assert.IsEmpty(PocketWarningsFor(warnings, pocket.PocketId),
                "A Pocket curated back to the main graph must not be flagged unreachable.");
        }

        [Test]
        public void ThreePocketChain_AllCuratedBackToMainGraph_LogsNoReachabilityWarnings()
        {
            var layout = BakeRepro(90210700, isolatedPocketCount: 3);
            var p0 = layout.Pockets[0];
            var p1 = layout.Pockets[1];
            var p2 = layout.Pockets[2];
            var anchor = AnyOrdinaryDoorwayId(layout);

            // main graph -> p0 -> p1 -> p2 (each pocket's Entrance paired to the
            // previous hop's door) — must propagate reachability multiple hops,
            // not just one.
            var curation = MakeCuration(layout.Seed,
                (anchor, p0.EntranceDoorwayId),
                (p0.ExitDoorwayId, p1.EntranceDoorwayId),
                (p1.ExitDoorwayId, p2.EntranceDoorwayId));

            var warnings = CaptureWarnings(() => LevelBaker.BakeConnections(layout, curation, MakeTransitionDefaults()));

            Assert.IsEmpty(PocketWarningsFor(warnings, p0.PocketId), "p0 is directly curated to the main graph.");
            Assert.IsEmpty(PocketWarningsFor(warnings, p1.PocketId), "p1 is reachable via p0, two hops away.");
            Assert.IsEmpty(PocketWarningsFor(warnings, p2.PocketId), "p2 is reachable via p0->p1, three hops away.");
        }

        [Test]
        public void TwoPocketsCuratedOnlyToEachOther_BothLogReachabilityWarnings()
        {
            var layout = BakeRepro(90210801, isolatedPocketCount: 2);
            var p0 = layout.Pockets[0];
            var p1 = layout.Pockets[1];

            // A closed loop, disconnected from the main graph entirely — the
            // case a naive "is every doorway present in some pairing" check
            // would miss (both doorways ARE present in a pairing here).
            var curation = MakeCuration(layout.Seed,
                (p0.ExitDoorwayId, p1.EntranceDoorwayId));

            var warnings = CaptureWarnings(() => LevelBaker.BakeConnections(layout, curation, MakeTransitionDefaults()));

            Assert.IsNotEmpty(PocketWarningsFor(warnings, p0.PocketId),
                "p0 is not reachable from the main graph — its only pairing is to an equally-unreached pocket.");
            Assert.IsNotEmpty(PocketWarningsFor(warnings, p1.PocketId),
                "p1 is not reachable from the main graph — its only pairing is to an equally-unreached pocket.");
        }

        private static List<string> PocketWarningsFor(List<string> warnings, string pocketId) =>
            warnings.FindAll(w => w.Contains(pocketId) && w.Contains("not reachable"));

        private static List<string> CaptureWarnings(System.Action action)
        {
            var warnings = new List<string>();

            void Handler(string logString, string stackTrace, LogType type)
            {
                if (type == LogType.Warning) warnings.Add(logString);
            }

            Application.logMessageReceived += Handler;
            try
            {
                action();
            }
            finally
            {
                Application.logMessageReceived -= Handler;
            }

            return warnings;
        }

        private static string AnyOrdinaryDoorwayId(Layout layout)
        {
            var ordinary = layout.Connections.Find(c => !c.IsSpareDoor && c.ToRoomId != null);
            Assert.NotNull(ordinary, "Repro parameters should produce at least one ordinary connection.");
            return ordinary.DoorwayId;
        }

        private static LevelConnectionCuration MakeCuration(int seed, params (string a, string b)[] pairings)
        {
            var curation = ScriptableObject.CreateInstance<LevelConnectionCuration>();
            curation.Seed = seed;
            foreach (var (a, b) in pairings)
                curation.Pairings.Add(new CuratedConnectionPairing
                    { DoorwayIdA = a, DoorwayIdB = b, Kind = ConnectionKind.NonEuclideanHidden });
            return curation;
        }

        private static NonEuclideanTransitionDefaults MakeTransitionDefaults() =>
            ScriptableObject.CreateInstance<NonEuclideanTransitionDefaults>();

        private Layout BakeRepro(int seed, int isolatedPocketCount)
        {
            _bakedSeed = seed;

            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");
            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            var parameters = new GenerationParameters
            {
                Seed = seed,
                RoomCount = 24,
                PuzzleRoomCount = 0,
                OpenSpaceCount = 0,
                CorridorCount = 0,
                IrregularRoomCount = 0,
                LoopCount = 0,
                MinEntryExitDistance = 1,
                IsolatedPocketCount = isolatedPocketCount,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            Assert.AreEqual(isolatedPocketCount, layout.Pockets.Count,
                $"Repro parameters for seed {seed} should produce {isolatedPocketCount} pocket(s).");

            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };
            LevelBaker.Bake(layout, shellConfig, "PocketReachabilityTestRepro");

            return layout;
        }
    }
}

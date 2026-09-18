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
    // SCARAB-015 step 7: the ticket's own closing checklist line, through the real Bake()
    // pipeline rather than RoomShellReconciler in isolation — tweak survives a rule-only
    // re-bake, delete survives, add survives (including a duplicated-prefab add), and an edit
    // orphaned by a topology change is dropped and reported, not kept dormant. Step 5's tests
    // cover the legacy (no anchor, no history asset) starting state; these all start from an
    // already-anchored, already-baked level — the ordinary case every bake after the first one
    // will be in.
    public class LevelBakerRoomShellReconcilerSteadyStateTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        // Distinct from every other test class's seed (90210555-561 already used).
        private const int TestSeed = 90210562;

        private Scene _tempScene;
        private string _tempScenePath;
        private Scene _originalActiveScene;
        private bool _reusedOriginalScene;
        private ShellAssemblyConfig _shellConfig;
        private Layout _layout;

        [SetUp]
        public void CreateTempSceneAndBake()
        {
            _originalActiveScene = EditorSceneManager.GetActiveScene();
            _tempScenePath = $"Assets/_TempLevelBakerRoomShellReconcilerSteadyStateTest_{System.Guid.NewGuid():N}.unity";

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

            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");
        }

        [TearDown]
        public void DestroyTempScene()
        {
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
        public void Tweak_SurvivesARuleOnlyReBake()
        {
            var levelRoot = GameObject.Find($"Level_{TestSeed}_Root");
            var target = AnyWallPiece(levelRoot);
            var targetCell = target.GetComponent<RoomShellSlotAnchor>().Cell;
            var tweakedPosition = target.transform.localPosition + new Vector3(0.3f, 0f, 0f);
            target.transform.localPosition = tweakedPosition;

            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");

            var afterFirstReBake = WallAnchorAt(levelRoot, targetCell);
            Assert.AreEqual(tweakedPosition, afterFirstReBake.transform.localPosition);

            // A second consecutive re-bake proves this is a durable edit, not a one-time carry-over.
            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");
            var afterSecondReBake = WallAnchorAt(levelRoot, targetCell);
            Assert.AreEqual(tweakedPosition, afterSecondReBake.transform.localPosition);
        }

        [Test]
        public void Delete_SurvivesARuleOnlyReBake()
        {
            var levelRoot = GameObject.Find($"Level_{TestSeed}_Root");
            var target = AnyWallPiece(levelRoot);
            var targetCell = target.GetComponent<RoomShellSlotAnchor>().Cell;
            Undo.DestroyObjectImmediate(target.gameObject);

            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");

            Assert.IsNull(WallAnchorAt(levelRoot, targetCell, allowMissing: true),
                "A hand-deleted piece must not be silently refilled by the next bake.");

            // Re-bake again: it must stay deleted, not just survive once.
            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");
            Assert.IsNull(WallAnchorAt(levelRoot, targetCell, allowMissing: true));

            var history = AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(
                $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            Assert.IsTrue(history.Edits.Any(e => e.Kind == RoomShellEditKind.Delete && e.Cell == targetCell));
        }

        [Test]
        public void DuplicatedPrefabAdd_SurvivesARuleOnlyReBake()
        {
            var levelRoot = GameObject.Find($"Level_{TestSeed}_Root");
            var original = AnyWallPiece(levelRoot);
            var originalCell = original.GetComponent<RoomShellSlotAnchor>().Cell;

            // Ctrl+D in the Editor copies every component, anchor included — the exact scenario
            // Michael described as how hand edits actually happen.
            var duplicate = Object.Instantiate(original.gameObject, original.transform.parent);
            duplicate.name = original.name + "_HandDuplicate";
            duplicate.transform.localPosition = original.transform.localPosition + new Vector3(0.35f, 0f, 0f);

            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");

            var history = AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(
                $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            var addEntries = history.Edits.Where(e => e.Kind == RoomShellEditKind.Add).ToList();
            Assert.AreEqual(1, addEntries.Count, "The duplicate must be recorded as exactly one Add entry.");

            var originalStillThere = WallAnchorAt(levelRoot, originalCell);
            Assert.NotNull(originalStillThere, "The original piece must keep its own slot, unaffected by the duplicate.");

            var addedInstances = levelRoot.GetComponentsInChildren<RoomShellSlotAnchor>(true)
                .Where(a => a.Role == RoomShellSlotRole.Added).ToList();
            Assert.AreEqual(1, addedInstances.Count, "Exactly one Added instance must exist in the scene after the bake.");

            // Re-bake again: the add must persist as itself, not get flagged as a second duplicate.
            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");
            var addEntriesAfterSecondBake = AssetDatabase
                .LoadAssetAtPath<RoomShellEditHistory>(
                    $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset")
                .Edits.Where(e => e.Kind == RoomShellEditKind.Add).ToList();
            Assert.AreEqual(1, addEntriesAfterSecondBake.Count,
                "A second re-bake must not duplicate the already-recorded add.");
        }

        [Test]
        public void OrphanedEdit_IsDroppedAndNotKeptDormant_WhenItsSlotNoLongerExists()
        {
            var levelRoot = GameObject.Find($"Level_{TestSeed}_Root");
            var target = AnyWallPiece(levelRoot);
            var anchor = target.GetComponent<RoomShellSlotAnchor>();

            // Simulates a topology-changing parameter change without depending on
            // BspLayoutGenerator's randomness to actually reshuffle this specific cell: an
            // impossible cell can never appear in a fresh regeneration, so it's guaranteed
            // orphaned exactly the way a real topology change would orphan a real one.
            anchor.Cell = new Vector2Int(9999, 9999);
            target.transform.localPosition += new Vector3(0.3f, 0f, 0f); // also a "tweak", to prove it's dropped, not just ignored

            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");

            var history = AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(
                $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            Assert.IsFalse(history.Edits.Any(e => e.Cell == new Vector2Int(9999, 9999)),
                "An edit whose slot no longer exists must be dropped, not carried forward indefinitely.");

            // And it must not resurface on a later bake either — "dropped" means gone, not
            // dormant until some future bake happens to revive it.
            LevelBaker.Bake(_layout, _shellConfig, "SteadyStateTest");
            var historyAfter = AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(
                $"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}/RoomShellEditHistory.asset");
            Assert.IsFalse(historyAfter.Edits.Any(e => e.Cell == new Vector2Int(9999, 9999)));
        }

        private static PrefabBrushObject AnyWallPiece(GameObject levelRoot)
        {
            return levelRoot.GetComponentsInChildren<PrefabBrushObject>(true)
                .First(m => m.PlacementGroup == RoomShellAssembler.WallPlacementGroup &&
                            m.GetComponent<DoorwayMarker>() == null);
        }

        private static RoomShellSlotAnchor WallAnchorAt(GameObject levelRoot, Vector2Int cell, bool allowMissing = false)
        {
            var found = levelRoot.GetComponentsInChildren<RoomShellSlotAnchor>(true)
                .FirstOrDefault(a => a.Cell == cell && a.Role == RoomShellSlotRole.Wall);
            if (!allowMissing) Assert.NotNull(found, $"Expected a Wall anchor at {cell}.");
            return found;
        }
    }
}

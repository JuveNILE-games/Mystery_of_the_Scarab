using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // SCARAB-015 step 2: RoomShellEditHistory is the level-scoped, persistent store of hand
    // edits, mirroring LevelConnectionCuration's per-level asset pattern
    // (Generated/Level_{seed}/RoomShellEditHistory.asset). Load-or-create must be idempotent —
    // the same asset instance, with prior Edits intact — since Bake() calls it on every bake,
    // not just the first.
    public class RoomShellEditHistoryTests
    {
        // Distinct from every other test class's seed (90210555-558 already used).
        private const int TestSeed = 90210559;
        private const string ExpectedPath = "Assets/Game/Systems/LevelSystem/Generated/Level_90210559/RoomShellEditHistory.asset";

        [TearDown]
        public void DeleteGeneratedFolder()
        {
            AssetDatabase.DeleteAsset($"Assets/Game/Systems/LevelSystem/Generated/Level_{TestSeed}");
        }

        [Test]
        public void LoadOrCreate_CreatesNewAssetWithSeedSetAndNoEdits_WhenNoneExists()
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(ExpectedPath),
                "Repro assumption: no asset should exist yet for this seed.");

            var history = LevelBaker.LoadOrCreateRoomShellEditHistory(TestSeed);

            Assert.NotNull(history);
            Assert.AreEqual(TestSeed, history.Seed);
            Assert.NotNull(history.Edits);
            Assert.IsEmpty(history.Edits);
            Assert.NotNull(AssetDatabase.LoadAssetAtPath<RoomShellEditHistory>(ExpectedPath),
                "LoadOrCreate should have written a real asset to the Generated/ folder.");
        }

        [Test]
        public void LoadOrCreate_ReturnsSameAssetAndPreservesExistingEdits_OnSecondCall()
        {
            var first = LevelBaker.LoadOrCreateRoomShellEditHistory(TestSeed);
            first.Edits.Add(new RoomShellSlotEdit
            {
                Cell = new Vector2Int(3, 4),
                Role = RoomShellSlotRole.Wall,
                Kind = RoomShellEditKind.Tweak,
                LocalPosition = new Vector3(3f, 0f, 4f),
                LocalRotation = Quaternion.identity,
                LocalScale = Vector3.one,
            });
            EditorUtility.SetDirty(first);
            AssetDatabase.SaveAssets();

            var second = LevelBaker.LoadOrCreateRoomShellEditHistory(TestSeed);

            Assert.AreEqual(AssetDatabase.GetAssetPath(first), AssetDatabase.GetAssetPath(second),
                "A second call for the same seed must return the same asset, not a fresh one.");
            Assert.AreEqual(1, second.Edits.Count,
                "An edit recorded by an earlier bake must survive a later LoadOrCreate call.");
            Assert.AreEqual(new Vector2Int(3, 4), second.Edits[0].Cell);
        }
    }
}

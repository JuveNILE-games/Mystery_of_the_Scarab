using System.Linq;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // SCARAB-015 step 4: RoomShellReconciler.Replay runs after Bake()'s wipe-and-regenerate.
    // It's the only thing that reads or writes RoomShellEditHistory. Everything here is built on
    // a hand-made "fresh" scene standing in for what regeneration just produced, plus a
    // pre-wipe snapshot standing in for what Capture recorded a moment earlier — no
    // BspLayoutGenerator, no wall-kit prefabs, matching Capture's own precedent.
    public class RoomShellReconcilerReplayTests
    {
        private const float CellSize = 1f;

        private GameObject _freshRoot;
        private RoomShellEditHistory _history;

        [SetUp]
        public void CreateFreshRootAndHistory()
        {
            _freshRoot = new GameObject("FreshRoot");
            _history = ScriptableObject.CreateInstance<RoomShellEditHistory>();
        }

        [TearDown]
        public void CleanUp()
        {
            Object.DestroyImmediate(_freshRoot);
            Object.DestroyImmediate(_history);
        }

        [Test]
        public void UnchangedSlot_IsLeftAlone_AndRecordsNoEdit()
        {
            var fresh = AddFreshWall(new Vector2Int(2, 3), new Vector3(2f, 0f, 3f));
            var preWipe = new[]
            {
                Identified(new Vector2Int(2, 3), RoomShellSlotRole.Wall, new Vector3(2f, 0f, 3f)),
            }.ToList();

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                preWipe, _history, CellSize);

            Assert.AreEqual(new Vector3(2f, 0f, 3f), fresh.transform.localPosition,
                "An unchanged slot's fresh transform must not be touched.");
            Assert.IsEmpty(_history.Edits, "Nothing changed, so nothing should be recorded.");
            Assert.AreEqual(0, report.TweaksApplied);
        }

        [Test]
        public void TweakedSlot_OverwritesTheFreshTransform_AndRecordsATweak()
        {
            AddFreshWall(new Vector2Int(2, 3), new Vector3(2f, 0f, 3f));
            var preWipe = new[]
            {
                Identified(new Vector2Int(2, 3), RoomShellSlotRole.Wall, new Vector3(2.6f, 0f, 3f)),
            }.ToList();

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                preWipe, _history, CellSize);

            var freshNow = FindFreshAt(new Vector2Int(2, 3));
            Assert.AreEqual(new Vector3(2.6f, 0f, 3f), freshNow.transform.localPosition,
                "A tweak must overwrite the fresh piece's transform with the pre-wipe one.");
            Assert.AreEqual(1, _history.Edits.Count(e => e.Kind == RoomShellEditKind.Tweak));
            Assert.AreEqual(1, report.TweaksApplied);
        }

        [Test]
        public void FreshSlotWithNoLiveMatch_IsLeftAlone_WhenItWasNeverKnownBefore()
        {
            AddFreshWall(new Vector2Int(9, 9), new Vector3(9f, 0f, 9f));
            // _history.KnownSlots stays empty: this cell has never been part of any previous bake.

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                new(), _history, CellSize);

            Assert.NotNull(FindFreshAt(new Vector2Int(9, 9)),
                "A genuinely new slot from a topology change must not be mistaken for a deletion.");
            Assert.IsEmpty(_history.Edits.Where(e => e.Kind == RoomShellEditKind.Delete));
            Assert.AreEqual(0, report.DeletesApplied);
        }

        [Test]
        public void FreshSlotWithNoLiveMatch_IsDeleted_WhenItWasKnownBefore()
        {
            AddFreshWall(new Vector2Int(9, 9), new Vector3(9f, 0f, 9f));
            _history.KnownSlots.Add(new RoomShellSlotKey { Cell = new Vector2Int(9, 9), Role = RoomShellSlotRole.Wall });

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                new(), _history, CellSize);

            Assert.IsNull(FindFreshAt(new Vector2Int(9, 9)),
                "A slot that existed last bake and has no live match now must stay deleted.");
            Assert.AreEqual(1, _history.Edits.Count(e => e.Kind == RoomShellEditKind.Delete));
            Assert.AreEqual(1, report.DeletesApplied);
        }

        [Test]
        public void LegacyUnanchoredObject_RecoversATweak_ByMatchingItsNearestFreshInstance()
        {
            AddFreshWall(new Vector2Int(4, 4), new Vector3(4f, 0f, 4f));
            var legacy = Unanchored(new Vector3(4.5f, 0f, 4f)); // close to (4,4), no anchor at all

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                new() { legacy }, _history, CellSize);

            var freshNow = FindFreshAt(new Vector2Int(4, 4));
            Assert.AreEqual(new Vector3(4.5f, 0f, 4f), freshNow.transform.localPosition,
                "A recovered legacy tweak must be applied just like an already-anchored one.");
            Assert.AreEqual(1, report.TweaksApplied);
        }

        [Test]
        public void LegacyUnanchoredObject_BecomesAnAdd_WhenNothingFreshIsNearby()
        {
            var sourcePrefab = new GameObject("SourcePrefabStandIn");
            var legacy = Unanchored(new Vector3(500f, 0f, 500f), sourcePrefab);

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                new() { legacy }, _history, CellSize);

            Assert.AreEqual(1, _history.Edits.Count(e => e.Kind == RoomShellEditKind.Add));
            Assert.AreEqual(1, report.AddsApplied);
            var addEntry = _history.Edits.Single(e => e.Kind == RoomShellEditKind.Add);
            Assert.AreEqual(sourcePrefab, addEntry.SourcePrefab);
            Assert.IsFalse(string.IsNullOrEmpty(addEntry.SlotId), "A fresh SlotId must be minted for a recovered legacy add.");

            Object.DestroyImmediate(sourcePrefab);
        }

        [Test]
        public void DuplicatedAnchor_SplitsIntoOneKeptSlotAndOneNewAdd()
        {
            AddFreshWall(new Vector2Int(2, 3), new Vector3(2f, 0f, 3f));
            var sourcePrefab = new GameObject("SourcePrefabStandIn");
            var original = Identified(new Vector2Int(2, 3), RoomShellSlotRole.Wall, new Vector3(2f, 0f, 3f));
            var duplicate = Identified(new Vector2Int(2, 3), RoomShellSlotRole.Wall, new Vector3(2.3f, 0f, 3f),
                sourcePrefab);

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                new() { original, duplicate }, _history, CellSize);

            Assert.AreEqual(1, _history.Edits.Count(e => e.Kind == RoomShellEditKind.Add),
                "Exactly one of the two colliding entries must be demoted to a new Add.");
            Assert.AreEqual(1, report.AddsApplied);

            Object.DestroyImmediate(sourcePrefab);
        }

        [Test]
        public void DuplicatedAddedAnchor_SplitsIntoOneKeptAddAndOneNewAdd()
        {
            var prefabA = new GameObject("PrefabAStandIn");
            _history.Edits.Add(new RoomShellSlotEdit
            {
                Cell = new Vector2Int(3, 3),
                Role = RoomShellSlotRole.Added,
                SlotId = "slot-orig",
                Kind = RoomShellEditKind.Add,
                LocalPosition = new Vector3(3f, 0f, 3f),
                LocalRotation = Quaternion.identity,
                LocalScale = Vector3.one,
                SourcePrefab = prefabA,
            });

            var original = IdentifiedAdded("slot-orig", new Vector3(3f, 0f, 3f), prefabA);
            var duplicate = IdentifiedAdded("slot-orig", new Vector3(3.5f, 0f, 3f), prefabA);

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                new() { original, duplicate }, _history, CellSize);

            var addEdits = _history.Edits.Where(e => e.Kind == RoomShellEditKind.Add).ToList();
            Assert.AreEqual(2, addEdits.Count);
            Assert.AreEqual(1, addEdits.Count(e => e.SlotId == "slot-orig"));
            Assert.AreEqual(1, addEdits.Count(e => e.SlotId != "slot-orig"),
                "The duplicate must be re-anchored under a new SlotId, not collide with the original.");
            Assert.AreEqual(2, report.AddsApplied);

            Object.DestroyImmediate(prefabA);
        }

        [Test]
        public void IdentifiedEntry_WithNoFreshCounterpart_IsOrphanedAndDropped()
        {
            // No fresh wall at (7,7) at all — a topology change removed that cell entirely.
            var preWipe = new[]
            {
                Identified(new Vector2Int(7, 7), RoomShellSlotRole.Wall, new Vector3(7f, 0f, 7f)),
            }.ToList();

            var report = RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup,
                preWipe, _history, CellSize);

            Assert.IsEmpty(_history.Edits, "An orphaned slot must not be carried forward as any kind of edit.");
            Assert.AreEqual(1, report.OrphansDropped.Count);
        }

        [Test]
        public void Replay_OverwritesKnownSlots_WithExactlyTheFreshSlotSet()
        {
            AddFreshWall(new Vector2Int(1, 1), new Vector3(1f, 0f, 1f));
            AddFreshWall(new Vector2Int(2, 2), new Vector3(2f, 0f, 2f));
            _history.KnownSlots.Add(new RoomShellSlotKey { Cell = new Vector2Int(99, 99), Role = RoomShellSlotRole.Wall });

            RoomShellReconciler.Replay(_freshRoot, RoomShellAssembler.WallPlacementGroup, new(), _history, CellSize);

            Assert.AreEqual(2, _history.KnownSlots.Count);
            Assert.IsTrue(_history.KnownSlots.Any(k => k.Cell == new Vector2Int(1, 1)));
            Assert.IsTrue(_history.KnownSlots.Any(k => k.Cell == new Vector2Int(2, 2)));
            Assert.IsFalse(_history.KnownSlots.Any(k => k.Cell == new Vector2Int(99, 99)),
                "A stale slot from a previous bake must not survive once it's no longer fresh.");
        }

        private GameObject AddFreshWall(Vector2Int cell, Vector3 localPosition)
        {
            var go = new GameObject($"FreshWall_{cell.x}_{cell.y}");
            go.transform.SetParent(_freshRoot.transform, false);
            go.transform.localPosition = localPosition;
            go.AddComponent<PrefabBrushObject>().PlacementGroup = RoomShellAssembler.WallPlacementGroup;
            var anchor = go.AddComponent<RoomShellSlotAnchor>();
            anchor.Cell = cell;
            anchor.Role = RoomShellSlotRole.Wall;
            return go;
        }

        private GameObject FindFreshAt(Vector2Int cell)
        {
            return _freshRoot.GetComponentsInChildren<RoomShellSlotAnchor>(true)
                .FirstOrDefault(a => a.Cell == cell)?.gameObject;
        }

        private static RoomShellPreWipeSlot Identified(Vector2Int cell, RoomShellSlotRole role, Vector3 localPosition,
            GameObject sourcePrefab = null)
        {
            return new RoomShellPreWipeSlot
            {
                InstanceName = "PreWipe",
                HasAnchor = true,
                Cell = cell,
                Role = role,
                LocalPosition = localPosition,
                LocalRotation = Quaternion.identity,
                LocalScale = Vector3.one,
                WorldPosition = localPosition,
                SourcePrefab = sourcePrefab,
            };
        }

        private static RoomShellPreWipeSlot IdentifiedAdded(string slotId, Vector3 localPosition, GameObject sourcePrefab)
        {
            return new RoomShellPreWipeSlot
            {
                InstanceName = "PreWipeAdded",
                HasAnchor = true,
                Role = RoomShellSlotRole.Added,
                SlotId = slotId,
                LocalPosition = localPosition,
                LocalRotation = Quaternion.identity,
                LocalScale = Vector3.one,
                WorldPosition = localPosition,
                SourcePrefab = sourcePrefab,
            };
        }

        private static RoomShellPreWipeSlot Unanchored(Vector3 localPosition, GameObject sourcePrefab = null)
        {
            return new RoomShellPreWipeSlot
            {
                InstanceName = "LegacyPreWipe",
                HasAnchor = false,
                LocalPosition = localPosition,
                LocalRotation = Quaternion.identity,
                LocalScale = Vector3.one,
                WorldPosition = localPosition,
                SourcePrefab = sourcePrefab,
            };
        }
    }
}

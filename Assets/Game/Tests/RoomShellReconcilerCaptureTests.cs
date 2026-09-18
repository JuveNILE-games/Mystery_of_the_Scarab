using System.Linq;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    // SCARAB-015 step 3: RoomShellReconciler.Capture is a dumb, mechanical pre-wipe scan — no
    // matching, no Edits/history I/O. It runs before Bake() wipes anything, so the fresh
    // regeneration it would need to match a legacy (unanchored) object against doesn't exist
    // yet; that matching is Replay's job (step 4), once fresh content exists to match against.
    // Deliberately pure data in, plain data out — no BspLayoutGenerator, no wall-kit prefabs, no
    // scene-bake ceremony, matching RoomFloorFloodFill's own precedent in this codebase.
    public class RoomShellReconcilerCaptureTests
    {
        private GameObject _root;

        [SetUp]
        public void CreateSceneRoot()
        {
            _root = new GameObject("ShellRoot");
        }

        [TearDown]
        public void DestroySceneRoot()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Capture_RecordsAnchoredWallAndDoorObjects_WithTheirCellRoleAndTransform()
        {
            var sourcePrefab = new GameObject("SourcePrefabStandIn");
            sourcePrefab.transform.SetParent(_root.transform, false);
            var wall = AddMarked(RoomShellAssembler.WallPlacementGroup, sourcePrefab);
            wall.transform.localPosition = new Vector3(2f, 0f, 3f);
            var wallAnchor = wall.AddComponent<RoomShellSlotAnchor>();
            wallAnchor.Cell = new Vector2Int(2, 3);
            wallAnchor.Role = RoomShellSlotRole.Wall;

            var door = AddMarked(RoomShellAssembler.WallPlacementGroup, sourcePrefab);
            door.transform.localPosition = new Vector3(5f, 0f, 1f);
            var doorAnchor = door.AddComponent<RoomShellSlotAnchor>();
            doorAnchor.Cell = new Vector2Int(5, 1);
            doorAnchor.Role = RoomShellSlotRole.Door;

            var slots = RoomShellReconciler.Capture(_root, RoomShellAssembler.WallPlacementGroup);

            Assert.AreEqual(2, slots.Count);

            var wallSlot = slots.Single(s => s.Role == RoomShellSlotRole.Wall);
            Assert.IsTrue(wallSlot.HasAnchor);
            Assert.AreEqual(new Vector2Int(2, 3), wallSlot.Cell);
            Assert.AreEqual(new Vector3(2f, 0f, 3f), wallSlot.LocalPosition);
            Assert.AreEqual(sourcePrefab, wallSlot.SourcePrefab);

            var doorSlot = slots.Single(s => s.Role == RoomShellSlotRole.Door);
            Assert.IsTrue(doorSlot.HasAnchor);
            Assert.AreEqual(new Vector2Int(5, 1), doorSlot.Cell);
        }

        [Test]
        public void Capture_RecordsAnUnanchoredObject_WithHasAnchorFalse()
        {
            var legacy = AddMarked(RoomShellAssembler.WallPlacementGroup, null);
            legacy.transform.localPosition = new Vector3(9f, 0f, 9f);

            var slots = RoomShellReconciler.Capture(_root, RoomShellAssembler.WallPlacementGroup);

            Assert.AreEqual(1, slots.Count);
            Assert.IsFalse(slots[0].HasAnchor,
                "A legacy object with no RoomShellSlotAnchor must still be captured, just unidentified.");
            Assert.AreEqual(new Vector3(9f, 0f, 9f), slots[0].LocalPosition);
        }

        [Test]
        public void Capture_RecordsAnAddedSlot_WithItsSlotId()
        {
            var added = AddMarked(RoomShellAssembler.WallPlacementGroup, null);
            var anchor = added.AddComponent<RoomShellSlotAnchor>();
            anchor.Role = RoomShellSlotRole.Added;
            anchor.SlotId = "slot-abc123";

            var slots = RoomShellReconciler.Capture(_root, RoomShellAssembler.WallPlacementGroup);

            Assert.AreEqual(1, slots.Count);
            Assert.IsTrue(slots[0].HasAnchor);
            Assert.AreEqual(RoomShellSlotRole.Added, slots[0].Role);
            Assert.AreEqual("slot-abc123", slots[0].SlotId);
        }

        [Test]
        public void Capture_IgnoresObjectsInADifferentPlacementGroup()
        {
            AddMarked(RoomShellAssembler.FloorPlacementGroup, null);

            var slots = RoomShellReconciler.Capture(_root, RoomShellAssembler.WallPlacementGroup);

            Assert.IsEmpty(slots, "A floor-tagged object must not show up in a WallPlacementGroup capture.");
        }

        private GameObject AddMarked(string placementGroup, GameObject sourcePrefab)
        {
            var go = new GameObject("Marked");
            go.transform.SetParent(_root.transform, false);
            var marker = go.AddComponent<PrefabBrushObject>();
            marker.PlacementGroup = placementGroup;
            marker.SourcePrefab = sourcePrefab;
            return go;
        }
    }
}

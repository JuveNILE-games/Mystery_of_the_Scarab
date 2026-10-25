using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Regression for the door-mesh-overlap bug (found 2026-09-22 while
    // live-inspecting seed 555555 in the Editor): Door.prefab's mesh is
    // always a true 3-cell-wide module, but RoomShellAssembler only excludes
    // whatever cells are in connection.DoorCells from the normal per-cell
    // wall-placement loop. When BspLayoutGenerator.WidenDoorway can't widen a
    // doorway (the topology on one or both perpendicular sides doesn't
    // support a clean 1-cell border to grow into — confirmed correct,
    // by-design behavior, not a bug in its own right), DoorCells falls back
    // to a single cell, but the door's mesh still physically spans 3 cells —
    // so its immediate neighbor cells get their own independently-placed
    // WallStraightPrefab, which then overlaps the door.
    //
    // Confirmed live on seed 555555: Door at (13,9) has combined world
    // bounds X=[11.5,14.5], while WallStraightPrefab instances independently
    // placed at (12,9) and (14,9) sit at X=[11.5,12.5] and X=[13.5,14.5]
    // respectively — both physically inside the door's mesh footprint.
    public class RoomShellAssemblerDoorOverlapTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        [Test]
        public void NoWallOrDoorInstance_OverlapsAnyDoorInstance()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            // Exact repro parameters (2026-09-22) — the seed/params this bug
            // was ground-truthed against live in the Unity Editor.
            var parameters = new GenerationParameters
            {
                Seed = 555555, RoomCount = 8, PuzzleRoomCount = 2, OpenSpaceCount = 1,
                MinEntryExitDistance = 3, LoopCount = 2, CorridorCount = 2, CorridorMinLength = 4,
                CorridorMaxLength = 8, IrregularRoomCount = 2, MinLeafSize = 3, SpareDoorCount = 0,
                IsolatedPocketCount = 0,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            var tempParent = new GameObject("RoomShellAssemblerDoorOverlapTests_TempParent");
            try
            {
                var cellByInstance = RoomShellAssembler.BuildShells(layout, shellConfig, tempParent.transform);

                var doors = new List<GameObject>();
                var others = new List<GameObject>();
                foreach (var kvp in cellByInstance)
                {
                    if (kvp.Key == null) continue;
                    var anchor = kvp.Key.GetComponent<RoomShellSlotAnchor>();
                    if (anchor != null && anchor.Role == RoomShellSlotRole.Door) doors.Add(kvp.Key);
                    else others.Add(kvp.Key);
                }

                Assert.Greater(doors.Count, 0,
                    "No doors were placed for this seed/params — the test no longer exercises the case it was written to guard, has the layout drifted?");

                int overlapsChecked = 0;
                foreach (var door in doors)
                {
                    var doorBounds = CombinedBounds(door);
                    foreach (var other in others)
                    {
                        var otherBounds = CombinedBounds(other);
                        overlapsChecked++;

                        float overlapX = Mathf.Min(doorBounds.max.x, otherBounds.max.x) - Mathf.Max(doorBounds.min.x, otherBounds.min.x);
                        float overlapZ = Mathf.Min(doorBounds.max.z, otherBounds.max.z) - Mathf.Max(doorBounds.min.z, otherBounds.min.z);

                        // Genuine volumetric overlap on both axes, not merely touching at a shared edge.
                        Assert.IsFalse(overlapX > 0.02f && overlapZ > 0.02f,
                            $"Door at {cellByInstance[door]} (bounds X=[{doorBounds.min.x:F3},{doorBounds.max.x:F3}] " +
                            $"Z=[{doorBounds.min.z:F3},{doorBounds.max.z:F3}]) overlaps instance at {cellByInstance[other]} " +
                            $"(bounds X=[{otherBounds.min.x:F3},{otherBounds.max.x:F3}] Z=[{otherBounds.min.z:F3},{otherBounds.max.z:F3}]) " +
                            $"by X={overlapX:F3} Z={overlapZ:F3}.");
                    }
                }

                Assert.Greater(overlapsChecked, 0,
                    "No door/other-instance pairs were checked — the test no longer exercises the case it was written to guard.");
            }
            finally
            {
                Object.DestroyImmediate(tempParent);
            }
        }

        // Independent mirror of RoomShellAssembler.WorldMeshBounds/ReadSceneWallGeometry's
        // own bounds math — deliberately not calling those private methods reflectively.
        private static Bounds CombinedBounds(GameObject go)
        {
            Bounds combined = default;
            bool any = false;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = WorldMeshBounds(filter);
                if (!any) { combined = b; any = true; }
                else combined.Encapsulate(b);
            }

            return combined;
        }

        private static Bounds WorldMeshBounds(MeshFilter filter)
        {
            var localBounds = filter.sharedMesh.bounds;
            var t = filter.transform;
            var result = new Bounds(t.TransformPoint(localBounds.min), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) != 0 ? localBounds.max.x : localBounds.min.x,
                    (i & 2) != 0 ? localBounds.max.y : localBounds.min.y,
                    (i & 4) != 0 ? localBounds.max.z : localBounds.min.z);
                result.Encapsulate(t.TransformPoint(corner));
            }

            return result;
        }
    }
}

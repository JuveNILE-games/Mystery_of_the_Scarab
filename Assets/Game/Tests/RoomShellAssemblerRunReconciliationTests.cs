using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelGeneration.Editor;
using Game.Systems.LevelSystem.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests
{
    // Regression for the same-run outward-shift reconciliation fix (Item 2,
    // found 2026-09-22 during a fresh-eyes pass over plans/README.md's open
    // wall-alignment findings, superseding the deleted
    // RoomShellAssemblerDoorAlignmentTests.cs, whose premise — every door
    // must match every same-rotation wall neighbor — turned out wrong: a
    // door between two genuinely different rooms has no common flush side
    // and is correctly left centered).
    //
    // RoomShellAssembler.DetermineOutwardDir decides per-cell in isolation,
    // so a divider cell (genuine 2-room-opposite, deliberately centered)
    // sitting next to a single-room cell (deliberately flush) in the same
    // physical wall row produces a visible step exactly at the boundary.
    // Every door is a divider cell by construction (a doorway is exactly the
    // shared boundary of 2 rooms), so this hit doors constantly. Confirmed
    // live across multiple real wall runs on seed 1919335568 (both plain
    // wall-to-wall and door-adjacent cases) before this fix.
    //
    // Fixed by RoomShellAssembler.ReconcileRunOutwardDirs: walks each
    // maximal same-orientation, run-axis-adjacent chain of Straight-shaped
    // wall/door instances in cell order, and fills a null-verdict cell only
    // when sandwiched between (or trailing) an anchor whose verdict is
    // known — for an interior gap between two anchors, only when they
    // agree. Cells whose nearby anchors genuinely disagree (a door between
    // two different rooms with no common flush side, e.g. (9,19) below) are
    // left exactly as DetermineOutwardDir decided — correct, not a bug. See
    // plans/README.md's "Item 4" finding for the separate, deliberately
    // unaddressed class of seam (the room switches sides mid-run, so even
    // the *direction* to push disagrees, not just whether to push).
    public class RoomShellAssemblerRunReconciliationTests
    {
        private const string RuleSetPath = "Assets/Game/PrefabBrush/WallPrefabRuleSet.asset";
        private const string DoorPrefabPath = "Assets/Game/World/Prefabs/Room/Door.prefab";

        [Test]
        public void SameRunCells_ShareAConsistentThicknessAxisFace_AcrossDividersAndDoors()
        {
            var ruleSet = AssetDatabase.LoadAssetAtPath<PrefabRuleSet>(RuleSetPath);
            Assert.NotNull(ruleSet, $"Could not load {RuleSetPath} — is the path still correct?");

            var doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            Assert.NotNull(doorPrefab, $"Could not load {DoorPrefabPath} — is the path still correct?");

            // Exact repro parameters (2026-09-22), matching the seed this
            // fix was ground-truthed against live in the Unity Editor —
            // same seed as the deleted door-alignment test and Item 1.
            var parameters = new GenerationParameters
            {
                Seed = 1919335568, RoomCount = 8, PuzzleRoomCount = 2, OpenSpaceCount = 1,
                MinEntryExitDistance = 3, LoopCount = 2, CorridorCount = 2, CorridorMinLength = 4,
                CorridorMaxLength = 8, IrregularRoomCount = 2, MinLeafSize = 3, SpareDoorCount = 0,
                IsolatedPocketCount = 0,
            };

            var layout = BspLayoutGenerator.Generate(parameters);
            var shellConfig = new ShellAssemblyConfig { RuleSet = ruleSet, DoorPrefab = doorPrefab, CellSize = 1f };

            var tempParent = new GameObject("RoomShellAssemblerRunReconciliationTests_TempParent");
            try
            {
                var cellByInstance = RoomShellAssembler.BuildShells(layout, shellConfig, tempParent.transform);
                var instanceByCell = new Dictionary<Vector2Int, GameObject>();
                foreach (var kvp in cellByInstance)
                    if (kvp.Key != null)
                        instanceByCell[kvp.Value] = kvp.Key;

                // Known-verified runs (2026-09-22, live-inspected in the Editor via
                // selected-object dumps): every cell in each group sits on one
                // physically-contiguous wall run whose flanking single-room anchors
                // agree, so after reconciliation they must all share the same
                // thickness-axis bounds-center — including the divider/door cells
                // in the middle that DetermineOutwardDir alone leaves centered.

                // (9,4) T-junction correctly bounds this run on the west (not a
                // participant); (10,4)/(11,4) divider, (13,4) door. (12,4)/(14,4)
                // are deliberately absent from this list — RoomShellAssembler's
                // door-overlap cleanup (found 2026-09-22 alongside this fix)
                // removes them, since door (13,4)'s real 3-cell mesh already
                // physically covers what they used to redundantly occupy. The
                // reconciliation pass still runs before that cleanup, so it
                // correctly used their now-removed anchors to fill (10,4)/(11,4)
                // and the door before they were destroyed.
                AssertRunSharesThicknessCenter(instanceByCell, thicknessIsZ: true,
                    new Vector2Int(10, 4), new Vector2Int(11, 4), new Vector2Int(13, 4));

                // (17,12)/(17,17) anchors, (17,13)-(17,16) all divider cells between them.
                AssertRunSharesThicknessCenter(instanceByCell, thicknessIsZ: false,
                    new Vector2Int(17, 12), new Vector2Int(17, 13), new Vector2Int(17, 14),
                    new Vector2Int(17, 15), new Vector2Int(17, 16), new Vector2Int(17, 17));

                // (10,18)-(14,18) is a leading divider run with NO anchor on its own
                // west side at all — only reachable via extending (15,18)/(16,18)/
                // (17,18)'s anchor backward.
                AssertRunSharesThicknessCenter(instanceByCell, thicknessIsZ: true,
                    new Vector2Int(10, 18), new Vector2Int(11, 18), new Vector2Int(12, 18),
                    new Vector2Int(13, 18), new Vector2Int(14, 18), new Vector2Int(15, 18),
                    new Vector2Int(16, 18), new Vector2Int(17, 18));

                // Regression guard against over-eager reconciliation (e.g. "always
                // extend even on disagreement"): the door at (9,19) sits between
                // Room_001 and Room_004 with no common flush side — must stay
                // centered (self-centered thickness-axis center = grid X + 0.5*
                // CellSize, matching the untouched, un-shifted baseline every
                // piece gets before any outward push is applied), not pulled
                // flush toward either flanking room. Checked directly against the
                // door's own bounds rather than a specific neighbor cell, since
                // RoomShellAssembler's door-overlap cleanup (found 2026-09-22
                // alongside this fix) can legitimately remove whichever immediate
                // neighbor cell used to sit there.
                Assert.IsTrue(instanceByCell.ContainsKey(new Vector2Int(9, 19)), "cell (9,19) has no placed door — has the layout drifted?");
                var doorCenter = ThicknessCenter(instanceByCell[new Vector2Int(9, 19)], thicknessIsZ: false);
                Assert.AreEqual(9.0f, doorCenter, 0.01f,
                    $"Door at (9,19) has thickness-axis center {doorCenter:F4}, expected the self-centered " +
                    $"baseline 9.0 (grid X=9, no outward shift applied) — Room_001/Room_004 disagree on which " +
                    $"side to push, so the door must stay centered, not get pulled flush toward one arbitrary side.");
            }
            finally
            {
                Object.DestroyImmediate(tempParent);
            }
        }

        private static void AssertRunSharesThicknessCenter(Dictionary<Vector2Int, GameObject> instanceByCell,
            bool thicknessIsZ, params Vector2Int[] cells)
        {
            float? reference = null;
            Vector2Int referenceCell = default;
            foreach (var cell in cells)
            {
                Assert.IsTrue(instanceByCell.ContainsKey(cell), $"cell {cell} has no placed instance — has the layout drifted?");
                var center = ThicknessCenter(instanceByCell[cell], thicknessIsZ);
                if (reference == null)
                {
                    reference = center;
                    referenceCell = cell;
                }
                else
                {
                    Assert.AreEqual(reference.Value, center, 0.01f,
                        $"cell {cell} thickness-axis center {center:F4} doesn't match run reference cell " +
                        $"{referenceCell} ({reference.Value:F4}) — the run isn't one continuous flush face.");
                }
            }
        }

        private static float ThicknessCenter(GameObject go, bool thicknessIsZ)
        {
            var b = CombinedBounds(go);
            return thicknessIsZ ? (b.min.z + b.max.z) * 0.5f : (b.min.x + b.max.x) * 0.5f;
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

using System;
using System.Collections.Generic;
using System.Linq;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelSystem.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // SCARAB-015: reconciles hand-edited Room Shell content across Bake()'s unconditional
    // wipe-and-regenerate. Two entry points, run around the existing wipe/regenerate — Capture
    // before it, Replay (step 4) after — sharing a plain, scene-agnostic snapshot in between.
    // Nothing here knows about Layout, BspLayoutGenerator, or the wall kit, which is what makes
    // it testable without any of them (RoomFloorFloodFill set this precedent in this codebase).
    public static class RoomShellReconciler
    {
        // A dumb, mechanical pre-wipe scan — no matching, no RoomShellEditHistory I/O. Matching
        // an unanchored (legacy) slot against a freshly regenerated one needs the fresh scene,
        // which doesn't exist yet at this point in Bake(); that's Replay's job (step 4), once
        // fresh content exists to match against. Capture only has to survive long enough for
        // Replay to consume it moments later, so it holds no live GameObject reference past the
        // object's own imminent destruction — only what Replay needs to act on it afterward.
        public static List<RoomShellPreWipeSlot> Capture(GameObject levelRoot, string placementGroup)
        {
            var slots = new List<RoomShellPreWipeSlot>();

            foreach (var marker in levelRoot.GetComponentsInChildren<PrefabBrushObject>(true))
            {
                if (marker.PlacementGroup != placementGroup) continue;

                var anchor = marker.GetComponent<RoomShellSlotAnchor>();
                var t = marker.transform;

                slots.Add(new RoomShellPreWipeSlot
                {
                    InstanceName = marker.name,
                    HasAnchor = anchor != null,
                    Cell = anchor != null ? anchor.Cell : default,
                    Role = anchor != null ? anchor.Role : default,
                    SlotId = anchor != null ? anchor.SlotId : null,
                    LocalPosition = t.localPosition,
                    LocalRotation = t.localRotation,
                    LocalScale = t.localScale,
                    WorldPosition = t.position,
                    Parent = t.parent,
                    SourcePrefab = marker.SourcePrefab,
                });
            }

            return slots;
        }

        // Everything that actually decides tweak/delete/add lives here, run after Bake()'s wipe
        // and regenerate — the only point where both the pre-wipe snapshot and the freshly
        // regenerated scene exist at once. The only thing that reads or writes
        // RoomShellEditHistory. Position/rotation drift under these thresholds counts as
        // floating-point jitter, not a real edit (Michael, 2026-09-18) — deliberately not exact
        // equality.
        private const float PositionEpsilon = 0.001f; // 1mm
        private const float RotationEpsilonDegrees = 0.05f;

        public static RoomShellReplayReport Replay(GameObject levelRoot, string placementGroup,
            List<RoomShellPreWipeSlot> preWipeSlots, RoomShellEditHistory history, float cellSize)
        {
            var report = new RoomShellReplayReport();
            var newEdits = new List<RoomShellSlotEdit>();

            var freshByKey = BuildFreshSlotMap(levelRoot, placementGroup);
            var claimedKeys = new HashSet<(Vector2Int, RoomShellSlotRole)>();

            var identifiedWallDoor = preWipeSlots.Where(s => s.HasAnchor && s.Role != RoomShellSlotRole.Added);
            var identifiedAdded = preWipeSlots.Where(s => s.HasAnchor && s.Role == RoomShellSlotRole.Added);
            var unanchored = preWipeSlots.Where(s => !s.HasAnchor).ToList();

            // Direct matches, and the duplicate-collision split for anything sharing a
            // Cell/Role that a Ctrl+D on an already-anchored piece produces.
            foreach (var group in identifiedWallDoor.GroupBy(s => (s.Cell, s.Role)))
            {
                var key = group.Key;
                if (!freshByKey.TryGetValue(key, out var freshGo))
                {
                    report.OrphansDropped.Add($"{group.First().InstanceName} ({key.Item1}, {key.Item2})");
                    continue;
                }

                var winner = PickClosest(group, freshGo.transform.localPosition);
                ApplyToFreshSlot(freshGo, winner, key.Item1, key.Item2, newEdits, report);
                claimedKeys.Add(key);

                foreach (var loser in group.Where(s => s != winner))
                    AddAsNewSlot(loser, levelRoot, cellSize, newEdits, report);
            }

            // Legacy fallback: an unanchored object matched to its nearest still-available fresh
            // slot, greedy by ascending distance, so a duplicate-of-a-duplicate resolves the same
            // way a duplicate-of-an-anchored-piece does above. Half a cell is close enough to be
            // "the same piece, just nudged" rather than a coincidence.
            float maxMatchDistance = cellSize * 0.5f;
            var candidates = new List<(RoomShellPreWipeSlot Slot, Vector2Int Cell, RoomShellSlotRole Role, float Distance)>();
            foreach (var slot in unanchored)
            foreach (var kv in freshByKey)
            {
                if (claimedKeys.Contains(kv.Key)) continue;
                var distance = Vector3.Distance(slot.WorldPosition, kv.Value.transform.position);
                if (distance <= maxMatchDistance)
                    candidates.Add((slot, kv.Key.Item1, kv.Key.Item2, distance));
            }

            var matchedSlots = new HashSet<RoomShellPreWipeSlot>();
            foreach (var candidate in candidates.OrderBy(c => c.Distance))
            {
                if (matchedSlots.Contains(candidate.Slot) || claimedKeys.Contains((candidate.Cell, candidate.Role)))
                    continue;

                matchedSlots.Add(candidate.Slot);
                claimedKeys.Add((candidate.Cell, candidate.Role));
                var freshGo = freshByKey[(candidate.Cell, candidate.Role)];
                ApplyToFreshSlot(freshGo, candidate.Slot, candidate.Cell, candidate.Role, newEdits, report);
            }

            foreach (var slot in unanchored.Where(s => !matchedSlots.Contains(s)))
                AddAsNewSlot(slot, levelRoot, cellSize, newEdits, report);

            // Already-added slots have no fresh counterpart at all — regeneration never produces
            // them — so they're always re-instantiated fresh, keeping their SlotId unless a
            // duplicate is claiming the same one.
            foreach (var group in identifiedAdded.GroupBy(s => s.SlotId))
            {
                var groupList = group.ToList();
                var expected = history.Edits.FirstOrDefault(e => e.Kind == RoomShellEditKind.Add && e.SlotId == group.Key);
                var winner = groupList.Count == 1
                    ? groupList[0]
                    : PickClosest(groupList, expected?.LocalPosition ?? groupList[0].LocalPosition);

                ReinstantiateAdd(winner, winner.Cell, winner.SlotId, newEdits, report);

                foreach (var loser in groupList.Where(s => s != winner))
                    AddAsNewSlot(loser, levelRoot, cellSize, newEdits, report);
            }

            // Whatever's left in freshByKey and unclaimed either stands as freshly generated
            // content (never seen before, per KnownSlots) or was deleted by hand and stays gone.
            foreach (var kv in freshByKey)
            {
                if (claimedKeys.Contains(kv.Key)) continue;

                bool wasKnown = history.KnownSlots.Any(k => k.Cell == kv.Key.Item1 && k.Role == kv.Key.Item2);
                if (!wasKnown) continue; // genuinely new from this bake's generation — leave it

                newEdits.Add(new RoomShellSlotEdit { Cell = kv.Key.Item1, Role = kv.Key.Item2, Kind = RoomShellEditKind.Delete });
                Undo.DestroyObjectImmediate(kv.Value);
                report.DeletesApplied++;
            }

            history.Edits = newEdits;
            history.KnownSlots = freshByKey.Keys
                .Select(k => new RoomShellSlotKey { Cell = k.Item1, Role = k.Item2 })
                .ToList();

            return report;
        }

        private static Dictionary<(Vector2Int, RoomShellSlotRole), GameObject> BuildFreshSlotMap(
            GameObject levelRoot, string placementGroup)
        {
            var freshByKey = new Dictionary<(Vector2Int, RoomShellSlotRole), GameObject>();
            foreach (var anchor in levelRoot.GetComponentsInChildren<RoomShellSlotAnchor>(true))
            {
                if (anchor.Role == RoomShellSlotRole.Added) continue; // never produced by regeneration
                var marker = anchor.GetComponent<PrefabBrushObject>();
                if (marker == null || marker.PlacementGroup != placementGroup) continue;
                freshByKey[(anchor.Cell, anchor.Role)] = anchor.gameObject;
            }

            return freshByKey;
        }

        // "Closest to the slot's expected transform keeps the slot; sibling index breaks ties"
        // (Michael, 2026-09-18) — sibling index isn't available here since the pre-wipe objects
        // are already destroyed by the time Replay runs, so stable capture order (the order
        // Capture's own scan encountered them in) stands in for it on an exact-distance tie.
        private static RoomShellPreWipeSlot PickClosest(IEnumerable<RoomShellPreWipeSlot> candidates,
            Vector3 expectedLocalPosition)
        {
            RoomShellPreWipeSlot best = null;
            float bestDistance = float.MaxValue;
            foreach (var candidate in candidates)
            {
                var distance = Vector3.Distance(candidate.LocalPosition, expectedLocalPosition);
                if (best == null || distance < bestDistance)
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static void ApplyToFreshSlot(GameObject freshGo, RoomShellPreWipeSlot preWipe, Vector2Int cell,
            RoomShellSlotRole role, List<RoomShellSlotEdit> newEdits, RoomShellReplayReport report)
        {
            var t = freshGo.transform;
            bool unchanged =
                Vector3.Distance(t.localPosition, preWipe.LocalPosition) <= PositionEpsilon &&
                Quaternion.Angle(t.localRotation, preWipe.LocalRotation) <= RotationEpsilonDegrees &&
                Vector3.Distance(t.localScale, preWipe.LocalScale) <= PositionEpsilon;

            if (unchanged) return;

            t.localPosition = preWipe.LocalPosition;
            t.localRotation = preWipe.LocalRotation;
            t.localScale = preWipe.LocalScale;

            newEdits.Add(new RoomShellSlotEdit
            {
                Cell = cell,
                Role = role,
                Kind = RoomShellEditKind.Tweak,
                LocalPosition = preWipe.LocalPosition,
                LocalRotation = preWipe.LocalRotation,
                LocalScale = preWipe.LocalScale,
            });
            report.TweaksApplied++;
        }

        // A hand-added piece that couldn't be matched to any fresh slot at all — no cell was ever
        // resolved for it by the rule system, so it's anchored to the nearest cell to its own
        // position plus whatever offset that leaves, exactly as the ADR describes.
        private static void AddAsNewSlot(RoomShellPreWipeSlot slot, GameObject levelRoot, float cellSize,
            List<RoomShellSlotEdit> newEdits, RoomShellReplayReport report)
        {
            if (slot.SourcePrefab == null)
            {
                report.OrphansDropped.Add($"{slot.InstanceName} (no SourcePrefab to recover an add from)");
                return;
            }

            var nearestCell = new Vector2Int(
                Mathf.RoundToInt(slot.LocalPosition.x / cellSize),
                Mathf.RoundToInt(slot.LocalPosition.z / cellSize));
            var slotId = Guid.NewGuid().ToString("N");

            ReinstantiateAdd(slot, nearestCell, slotId, newEdits, report, levelRoot);
        }

        private static void ReinstantiateAdd(RoomShellPreWipeSlot slot, Vector2Int cell, string slotId,
            List<RoomShellSlotEdit> newEdits, RoomShellReplayReport report, GameObject fallbackParent = null)
        {
            var parent = slot.Parent != null ? slot.Parent : fallbackParent?.transform;
            var instance = PrefabUtility.IsPartOfPrefabAsset(slot.SourcePrefab)
                ? (GameObject)PrefabUtility.InstantiatePrefab(slot.SourcePrefab)
                : UnityEngine.Object.Instantiate(slot.SourcePrefab);

            Undo.RegisterCreatedObjectUndo(instance, "Replay Room Shell Add");
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = slot.LocalPosition;
            instance.transform.localRotation = slot.LocalRotation;
            instance.transform.localScale = slot.LocalScale;

            var marker = instance.GetComponent<PrefabBrushObject>() ?? instance.AddComponent<PrefabBrushObject>();
            marker.SourcePrefab = slot.SourcePrefab;
            marker.PlacementGroup = RoomShellAssembler.WallPlacementGroup;
            marker.PlacementTime = DateTime.UtcNow.ToString("o");

            var anchor = instance.GetComponent<RoomShellSlotAnchor>() ?? instance.AddComponent<RoomShellSlotAnchor>();
            anchor.Cell = cell;
            anchor.Role = RoomShellSlotRole.Added;
            anchor.SlotId = slotId;

            newEdits.Add(new RoomShellSlotEdit
            {
                Cell = cell,
                Role = RoomShellSlotRole.Added,
                SlotId = slotId,
                Kind = RoomShellEditKind.Add,
                LocalPosition = slot.LocalPosition,
                LocalRotation = slot.LocalRotation,
                LocalScale = slot.LocalScale,
                SourcePrefab = slot.SourcePrefab,
            });
            report.AddsApplied++;
        }
    }

    public class RoomShellReplayReport
    {
        public int TweaksApplied;
        public int DeletesApplied;
        public int AddsApplied;
        public List<string> OrphansDropped = new();
    }

    // Plain data, not a live scene reference — the object this was captured from is destroyed by
    // Bake()'s wipe before Replay runs. InstanceName is diagnostics only (log messages), never an
    // identity key.
    public class RoomShellPreWipeSlot
    {
        public string InstanceName;

        public bool HasAnchor;
        public Vector2Int Cell;
        public RoomShellSlotRole Role;
        public string SlotId;

        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;

        // World position is for Replay's legacy nearest-match distance check only — comparing
        // local positions across two objects that might not share a parent isn't meaningful.
        // Parent survives the wipe (only the marked object itself is destroyed by
        // ClearGeneratedShellChildren), so it's safe to hold onto and reuse when Replay
        // re-instantiates a hand-added piece.
        public Vector3 WorldPosition;
        public Transform Parent;

        public GameObject SourcePrefab;
    }
}

using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using Game.Systems.LevelSystem.Runtime;
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
                    SourcePrefab = marker.SourcePrefab,
                });
            }

            return slots;
        }
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

        public GameObject SourcePrefab;
    }
}

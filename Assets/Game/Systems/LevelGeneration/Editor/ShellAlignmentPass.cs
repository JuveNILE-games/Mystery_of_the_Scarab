using System.Collections.Generic;
using Core.Tools.PrefabBrush;
using UnityEngine;

namespace Game.Systems.LevelGeneration.Editor
{
    // Post-process alignment (SCARAB-010 follow-up): each wall-kit prefab was
    // authored with its own pivot-to-footprint convention — WallCornerPrefab_Grid
    // is pivot-centered on its full 1x1 cell, but WallStraightPrefab's thin slab
    // only fills half its cell along its thickness axis. Centering every piece on
    // its own footprint fixes wall-to-wall and wall-to-door alignment, but where a
    // wall borders a real room on exactly one side (an actual exterior boundary,
    // not an ambiguous room-vs-room seam), the correct look is flush against the
    // *outward* edge, not centered — confirmed by hand-calibrating 8 exterior
    // wall/corner pairs live in the Editor (all reproduced exactly by this rule).
    // `outwardDirByInstance` carries that one topology fact in from
    // RoomShellAssembler (which already has cellOwner); everything else here is
    // still pure prefab geometry, no other room/neighbor knowledge involved.
    public static class ShellAlignmentPass
    {
        public static void Align(List<GameObject> instances, float cellSize,
            Dictionary<GameObject, Vector2Int> outwardDirByInstance = null)
        {
            // Scoped to one Align() call (one bake) rather than a static field:
            // a static cache silently went stale across an entire Editor session
            // whenever a prefab's source geometry changed after its bounds were
            // first measured (e.g. re-authoring WallTjunctionPrefab's children),
            // since editing a prefab asset doesn't trigger the domain reload that
            // would otherwise reset it — every later bake kept placing that prefab
            // using the old, wrong footprint with no visible cause.
            var footprintBoundsCache = new Dictionary<GameObject, Bounds>();

            foreach (var instance in instances)
            {
                if (instance == null) continue;

                var marker = instance.GetComponent<PrefabBrushObject>();
                if (marker == null || marker.SourcePrefab == null) continue;

                var localBounds = GetFootprintBoundsXZ(marker.SourcePrefab, footprintBoundsCache);
                var rotation = instance.transform.localRotation;
                var rotatedOffset = rotation * new Vector3(localBounds.center.x, 0f, localBounds.center.z);

                var desired = Vector3.zero;
                if (outwardDirByInstance != null && outwardDirByInstance.TryGetValue(instance, out var outward) &&
                    outward != Vector2Int.zero)
                {
                    var worldExtents = WorldExtentsXZ(rotation, localBounds.extents);
                    float cellHalfExtent = 0.5f * cellSize;

                    if (outward.x != 0)
                        desired.x = outward.x * (cellHalfExtent - worldExtents.x);
                    else
                        desired.z = outward.y * (cellHalfExtent - worldExtents.z);
                }

                instance.transform.localPosition += desired - rotatedOffset;
            }
        }

        // World-space half-extents of a local-space extents vector, valid for
        // rotations that are multiples of 90° around Y — true for every piece in
        // this grid-aligned kit. A 90°/270° rotation swaps which local axis (X or
        // Z) becomes the world X/Z half-extent; 0°/180° leaves them as-is.
        private static Vector3 WorldExtentsXZ(Quaternion rotation, Vector3 localExtents)
        {
            var right = rotation * Vector3.right;
            var forward = rotation * Vector3.forward;
            float worldX = Mathf.Abs(right.x) >= 0.5f ? localExtents.x : localExtents.z;
            float worldZ = Mathf.Abs(forward.z) >= 0.5f ? localExtents.z : localExtents.x;
            return new Vector3(worldX, localExtents.y, worldZ);
        }

        // The prefab's combined mesh footprint (center + extents), in the prefab
        // root's own local space. Cached per source prefab since the same handful
        // of prefabs get placed dozens of times per Level.
        private static Bounds GetFootprintBoundsXZ(GameObject prefab, Dictionary<GameObject, Bounds> cache)
        {
            if (cache.TryGetValue(prefab, out var cached))
                return cached;

            var filters = prefab.GetComponentsInChildren<MeshFilter>();
            Bounds combined = default;
            bool any = false;

            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null) continue;

                var localBounds = TransformBounds(filter.transform, prefab.transform, mesh.bounds);
                if (!any)
                {
                    combined = localBounds;
                    any = true;
                }
                else
                {
                    combined.Encapsulate(localBounds);
                }
            }

            cache[prefab] = combined;
            return combined;
        }

        // Transforms an axis-aligned Bounds from `from`'s local space into `to`'s
        // local space via all 8 corners — safe for whatever rotation/scale a
        // prefab's child hierarchy carries, unlike a naive center-only transform.
        private static Bounds TransformBounds(Transform from, Transform to, Bounds bounds)
        {
            var result = new Bounds(to.InverseTransformPoint(from.TransformPoint(bounds.min)), Vector3.zero);
            for (int i = 1; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) != 0 ? bounds.max.x : bounds.min.x,
                    (i & 2) != 0 ? bounds.max.y : bounds.min.y,
                    (i & 4) != 0 ? bounds.max.z : bounds.min.z);
                result.Encapsulate(to.InverseTransformPoint(from.TransformPoint(corner)));
            }

            return result;
        }
    }
}

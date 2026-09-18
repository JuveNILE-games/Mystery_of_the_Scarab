using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime
{
    // Per-level hand-edit memory (SCARAB-015): the durable record of every tweak, delete, and
    // add a curator made directly to a baked Room Shell, keyed by RoomShellSlotAnchor's Cell +
    // Role (never RoomId — see ADR 0003 for why that's unstable). Same shape of problem as
    // LevelConnectionCuration (level-scoped, persistent, keyed data), same convention: one asset
    // per generated level (Generated/Level_{seed}/RoomShellEditHistory.asset).
    //
    // Only edited slots get an entry — an unedited slot is simply absent, and letting
    // regeneration replace it with an identical fresh copy is harmless. There is deliberately no
    // separate "last known baseline" snapshot here: RoomShellReconciler diffs a slot's pre-wipe
    // transform against that same slot's freshly regenerated transform, which regeneration
    // already recomputes for free, so persisting a second copy of it would never have earned its
    // keep.
    [CreateAssetMenu(fileName = "RoomShellEditHistory", menuName = "Scarab/Room Shell Edit History")]
    public class RoomShellEditHistory : ScriptableObject
    {
        // Checked against the regenerated Layout.Seed before applying, same guard
        // LevelConnectionCuration.Seed already uses — catches applying the wrong level's history
        // against the wrong bake.
        public int Seed;
        public List<RoomShellSlotEdit> Edits = new();
    }

    public enum RoomShellEditKind
    {
        Tweak,
        Delete,
        Add,
    }

    [Serializable]
    public class RoomShellSlotEdit
    {
        public Vector2Int Cell;
        public RoomShellSlotRole Role;

        // Only meaningful when Role == Added, matching RoomShellSlotAnchor.SlotId.
        public string SlotId;

        public RoomShellEditKind Kind;

        // Unused for a Delete entry — there's nothing to reposition, only a slot to suppress.
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;

        // Only meaningful for an Add entry — the prefab Replay re-instantiates at LocalPosition.
        public string PrefabGuid;
    }
}

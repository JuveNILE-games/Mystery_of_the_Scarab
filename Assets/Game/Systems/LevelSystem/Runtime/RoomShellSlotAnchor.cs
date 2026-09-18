// RoomShellSlotAnchor.cs
// SCARAB-015: persists the grid cell + role RoomShellAssembler resolved a Room Shell piece for,
// so a later re-bake can tell "this is the same piece as last time" without RoomId (a generation-
// order index that reshuffles under a parameter change, see ADR 0003) or reverse-engineering a
// cell from a post-ShellAlignmentPass-shifted transform (already known to round wrong).
// Editor-only bookkeeping, same reasoning as Core's PrefabBrushObject: compiles to nothing in a
// player build even though it ships on real, playable Room Shell geometry.

#if UNITY_EDITOR
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime
{
    public enum RoomShellSlotRole
    {
        Wall,
        Door,
        Added,
    }

    [AddComponentMenu("")]
    public class RoomShellSlotAnchor : MonoBehaviour
    {
        [HideInInspector] public Vector2Int Cell;
        [HideInInspector] public RoomShellSlotRole Role;

        // Only meaningful when Role == Added, since two hand-added pieces can share a nearest
        // cell and Cell alone can't tell them apart.
        [HideInInspector] public string SlotId;
    }
}
#endif

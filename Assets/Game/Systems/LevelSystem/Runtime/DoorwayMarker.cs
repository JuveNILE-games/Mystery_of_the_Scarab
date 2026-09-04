using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime
{
    // The physical marker a Connection's doorway id resolves to in the scene.
    // Minimal by design (SCARAB-006) — expected to evolve once SCARAB-009's
    // non-Euclidean realization mechanism has real requirements.
    public class DoorwayMarker : MonoBehaviour
    {
        public string DoorwayId;
    }
}

using Game.Systems.LevelSystem.Definitions;
using UnityEngine;

namespace Game.Systems.LevelSystem.Runtime{
    /// <summary>
    /// Base room controller. Handles visibility/activation of the room root.
    /// PuzzleRoomController extends this.
    /// </summary>
    public class RoomController : MonoBehaviour
    {
        [SerializeField] protected RoomDefinition _definition;

        public RoomDefinition Definition => _definition;
        public bool IsActive { get; private set; }

        public virtual void EnterRoom()
        {
            IsActive = true;
            gameObject.SetActive(true);
        }

        public virtual void ExitRoom()
        {
            IsActive = false;
            // Optionally deactivate (or keep active for open-world rooms):
            // gameObject.SetActive(false);
        }
    }
}
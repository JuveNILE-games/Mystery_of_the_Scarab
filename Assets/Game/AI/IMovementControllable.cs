using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Interface for any state machine or movement component that can be driven by external AI input.
    /// </summary>
    public interface IMovementControllable
    {
        void OnMove(Vector2 direction);
    }
}

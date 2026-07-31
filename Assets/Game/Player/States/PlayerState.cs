using Core.Systems.StateMachine;
using SpriteAnimations;
using UnityEngine;

namespace Game.Player.States
{
    /// <summary>
    /// Base state for the player state machine.
    /// Provides common functionality and access to player components.
    ///
    /// Movement direction is sourced from Owner.WorldMoveInput — world-space XZ that was
    /// already projected from camera-relative input by PlayerInputInitializer (human) or
    /// supplied directly by AIMovementBridge (AI). No Camera.main access happens here.
    /// </summary>
    public abstract class PlayerState : BaseState<PlayerStateMachine, PlayerState>
    {
        // Common player references (cached for performance)
        protected CharacterController Controller => Owner != null ? Owner.Controller : null;
        protected SpriteAnimator Animator => Owner != null ? Owner.Animator : null;
        protected Transform Transform => Owner != null ? Owner.transform : null;

        protected bool IsGrounded => Owner != null && Owner.IsGrounded;

        protected PlayerState(string name = null) : base(name) { }

        /// <summary>Returns true when there is meaningful movement input this frame.</summary>
        protected bool HasInput()
        {
            return Owner != null && Owner.WorldMoveInput.sqrMagnitude > 0.01f;
        }

        /// <summary>
        /// Returns the world-space XZ movement direction for this frame.
        /// Already in world space — no camera projection needed.
        /// </summary>
        protected Vector3 GetMoveDirection()
        {
            if (Owner == null) return Vector3.zero;
            // WorldMoveInput.y is always 0 (enforced by PlayerStateMachine.OnMoveWorldSpace).
            // Normalize defensively; it should already be unit length from the input boundary.
            Vector3 dir = Owner.WorldMoveInput;
            return dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.zero;
        }
    }
}

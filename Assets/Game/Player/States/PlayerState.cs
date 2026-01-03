using Core.Systems.StateMachine;
using SpriteAnimations;
using UnityEngine;

namespace Game.Player.States
{
    /// <summary>
    /// Base state for the player state machine.
    /// Provides common functionality and access to player components.
    /// </summary>
    public abstract class PlayerState : BaseState<PlayerStateMachine, PlayerState>
    {

        // Common player references (cached for performance)
        protected CharacterController Controller => Owner?.Controller;
        protected SpriteAnimator Animator => Owner?.Animator;
        protected Transform Transform => Owner?.transform;
        
        // Common player data
        protected Vector3 Velocity => Controller != null ? Controller.velocity : Vector3.zero;
        protected bool IsGrounded => Owner != null && Owner.IsGrounded;
        
        protected PlayerState(string name = null) : base(name) { }
        
        /// <summary>
        /// Helper to check if player input is active
        /// </summary>
        protected bool HasInput()
        {
            return Owner != null && Owner.MoveInput.sqrMagnitude > 0.01f;
        }
        
        /// <summary>
        /// Helper to get normalized move direction
        /// </summary>
        protected Vector3 GetMoveDirection()
        {
            if (Owner == null) return Vector3.zero;
            
            Vector3 forward = Camera.main != null ? Camera.main.transform.forward : Transform.forward;
            Vector3 right = Camera.main != null ? Camera.main.transform.right : Transform.right;
            
            forward.y = 0;
            right.y = 0;
            forward.Normalize();
            right.Normalize();
            
            return (forward * Owner.MoveInput.y + right * Owner.MoveInput.x).normalized;
        }
    }
}
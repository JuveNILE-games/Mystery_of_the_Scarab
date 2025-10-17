using Core.Systems.StateMachine.Core;
using UnityEngine;

namespace Game.Player.States
{
    [CreateAssetMenu(fileName = "GroundedState", menuName = "State Machine/States/Grounded State")]
    public class GroundedState : PlayerState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Debug.Log("Player is now grounded");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Check if player is no longer grounded
            if (StateComponent != null && !StateComponent.IsGrounded())
            {
                // Transition to airborne state
                // This would be handled by transitions in the actual implementation
            }
        }
    }
}
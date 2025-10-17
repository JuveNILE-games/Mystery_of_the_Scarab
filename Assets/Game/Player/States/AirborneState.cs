using Core.Systems.StateMachine.Core;
using UnityEngine;

namespace Game.Player.States
{
    [CreateAssetMenu(fileName = "AirborneState", menuName = "State Machine/States/Airborne State")]
    public class AirborneState : PlayerState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            Debug.Log("Player is now airborne");
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Check if player has landed
            if (StateComponent != null && StateComponent.IsGrounded())
            {
                // Transition to grounded state handled by conditions
            }
        }
    }
}
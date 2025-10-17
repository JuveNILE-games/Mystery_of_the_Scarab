using Core.Systems.StateMachine.Core;
using UnityEngine;

namespace Game.Player.States
{
    [CreateAssetMenu(fileName = "AbilityState", menuName = "State Machine/States/Ability State")]
    public class AbilityState : PlayerState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("Ability");
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Handle ability-specific logic
            if (StateComponent != null)
            {
                // This would be handled by specific ability states
            }
        }
    }
}
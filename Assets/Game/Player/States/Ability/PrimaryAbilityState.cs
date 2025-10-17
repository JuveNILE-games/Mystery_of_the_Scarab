using UnityEngine;

namespace Game.Player.States.Ability
{
    [CreateAssetMenu(fileName = "PrimaryAbilityState", menuName = "State Machine/States/Primary Ability State")]
    public class PrimaryAbilityState : AbilityState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("PrimaryAbility");
                StateComponent.UsePrimaryAbility();
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Handle primary ability logic
        }
    }
}
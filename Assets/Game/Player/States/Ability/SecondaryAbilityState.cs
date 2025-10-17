using UnityEngine;

namespace Game.Player.States.Ability
{
    [CreateAssetMenu(fileName = "SecondaryAbilityState", menuName = "State Machine/States/Secondary Ability State")]
    public class SecondaryAbilityState : AbilityState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("SecondaryAbility");
                StateComponent.UseSecondaryAbility();
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Handle secondary ability logic
        }
    }
}
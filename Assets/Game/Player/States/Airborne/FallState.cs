using UnityEngine;

namespace Game.Player.States.Airborne
{
    [CreateAssetMenu(fileName = "FallState", menuName = "State Machine/States/Fall State")]
    public class FallState : PlayerState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("Fall");
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Transition handled when player lands (grounded check)
        }
    }
}
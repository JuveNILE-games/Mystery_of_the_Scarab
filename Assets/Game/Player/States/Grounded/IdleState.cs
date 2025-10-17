using UnityEngine;

namespace Game.Player.States.Grounded
{
    [CreateAssetMenu(fileName = "IdleState", menuName = "State Machine/States/Idle State")]
    public class IdleState : PlayerState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("Idle");
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            if (StateComponent != null)
            {
                Vector2 movement = StateComponent.GetMovementDirection();
                if (movement.magnitude > 0.1f)
                {
                    // Transition handled by conditions/transitions
                }
            }
        }
    }
}
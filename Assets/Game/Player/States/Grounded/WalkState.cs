using UnityEngine;

namespace Game.Player.States.Grounded
{
    [CreateAssetMenu(fileName = "WalkState", menuName = "State Machine/States/Walk State")]
    public class WalkState : PlayerState
    {
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("Walk");
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
                    StateComponent.SetMovement(movement);
                }
                else
                {
                    // Transition back to idle handled by conditions
                }
            }
        }
    }
}
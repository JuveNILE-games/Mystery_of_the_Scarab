using UnityEngine;

namespace Game.Player.States.Grounded
{
    [CreateAssetMenu(fileName = "SprintState", menuName = "State Machine/States/Sprint State")]
    public class SprintState : PlayerState
    {
        [SerializeField] private float sprintMultiplier = 1.5f;
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("Sprint");
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            if (StateComponent != null)
            {
                Vector2 movement = StateComponent.GetMovementDirection();
                if (movement.magnitude > 0.1f && StateComponent.IsSprintPressed())
                {
                    StateComponent.SetMovement(movement * sprintMultiplier);
                }
                else
                {
                    // Transition back to walk or idle handled by conditions
                }
            }
        }
    }
}
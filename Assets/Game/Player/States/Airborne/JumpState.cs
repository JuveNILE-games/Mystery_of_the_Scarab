using UnityEngine;

namespace Game.Player.States.Airborne
{
    [CreateAssetMenu(fileName = "JumpState", menuName = "State Machine/States/Jump State")]
    public class JumpState : PlayerState
    {
        [SerializeField] private float jumpForce = 10f;
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (StateComponent != null)
            {
                StateComponent.PlayAnimation("Jump");
                // Apply jump force
                StateComponent.SetVelocity(new Vector2(
                    StateComponent.GetMovementDirection().x * 5f, // Maintain horizontal momentum
                    jumpForce
                ));
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Transition to fall state when upward velocity becomes negative
            if (StateComponent != null)
            {
                // This would be handled by a condition checking vertical velocity
            }
        }
    }
}
using UnityEngine;
using SpriteAnimations;

namespace Game.Player.States.Grounded{
    public class IdleState : PlayerState
    {
        public IdleState() : base("Idle") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (Animator != null)
            {
                var windrose = Animator.Play<WindroseAnimator>("Idle");
                if (Owner != null) windrose?.SetDirection(Owner.LastMoveDirection);
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            // Apply simple gravity/stick force to ensure CharacterController updates isGrounded
            // and we don't float off slopes.
            if (Controller != null)
            {
                Controller.Move(Vector3.down * 4f * Time.deltaTime);
            }
        }
    }
}
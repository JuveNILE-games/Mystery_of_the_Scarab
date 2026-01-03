using UnityEngine;

using SpriteAnimations;

namespace Game.Player.States.Grounded{
    public class SprintState : PlayerState
    {
        private WindroseAnimator _windroseAnimator;
        
        public SprintState() : base("Sprint") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (Animator != null)
            {
                _windroseAnimator = Animator.Play<WindroseAnimator>("Sprint");
                if (Owner != null) _windroseAnimator?.SetDirection(Owner.LastMoveDirection);
            }
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            Vector3 moveDir = GetMoveDirection();
            if (Controller != null)
            {
                // Update animation direction
                if (Owner != null && _windroseAnimator != null && Owner.MoveInput.sqrMagnitude > 0.01f)
                {
                    _windroseAnimator.SetDirection(Owner.GetCardinalDirection(Owner.MoveInput));
                }

                float speed = Owner.Data.Value != null ? Owner.Data.Value.SprintSpeed : 6f;
                Vector3 moveVelocity = moveDir * speed;
                
                // Stick to ground
                moveVelocity.y = -2f;

                Controller.Move(moveVelocity * Time.deltaTime);
                
                if (moveDir != Vector3.zero)
                {
                    // Rotate towards movement
                    Transform.forward = Vector3.Slerp(Transform.forward, moveDir, Time.deltaTime * 15f);
                }
            }
                
        }
    }
}
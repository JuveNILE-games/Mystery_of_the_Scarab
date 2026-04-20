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
                // Update animation direction from world XZ — works for both human and AI input.
                if (Owner != null && _windroseAnimator != null && Owner.WorldMoveInput.sqrMagnitude > 0.01f)
                {
                    _windroseAnimator.SetDirection(Owner.GetCardinalDirection(
                        new Vector2(Owner.WorldMoveInput.x, Owner.WorldMoveInput.z)));
                }

                float speed = Owner.Data.Value != null ? Owner.Data.Value.SprintSpeed : 6f;
                Vector3 moveVelocity = moveDir * speed;
                
                // Stick to ground — raised to match WalkState (-5f). The shallower -2f caused
                // airborne flickers on dynamic NavMesh surface height variation.
                moveVelocity.y = -5f;

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
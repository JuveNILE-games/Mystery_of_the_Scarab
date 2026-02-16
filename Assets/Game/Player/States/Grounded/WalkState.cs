using UnityEngine;

using SpriteAnimations;

namespace Game.Player.States.Grounded{
    public class WalkState : PlayerState
    {
        private WindroseAnimator _windroseAnimator;
        
        public WalkState() : base("Walk") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (Animator != null)
            {
                _windroseAnimator = Animator.Play<WindroseAnimator>("Walk");
                // Set initial direction to last known direction so we don't snap to default (usually South/East)
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

                float speed = Owner.Data.Value != null ? Owner.Data.Value.WalkSpeed : 3f;
                //Debug.Log($"[WalkState] MoveDir: {moveDir}, Speed: {speed}, Input: {Owner.MoveInput}");
                Vector3 moveVelocity = moveDir * speed;
                
                // Preserve vertical velocity (gravity handling is typically in a separate system or Airborne state, 
                // but for grounded movement we might want to stick to ground or just move horizontally)
                // For a simple walk, we move horizontally. Gravity is applied if we are not grounded or simply by the CC.
                // However, Custom gravity logic is needed.
                // Let's ensure we move.
                
                // NOTE: We need to handle gravity even when walking to stick to slopes? 
                // A simple way is to apply a small downward force if grounded.
                moveVelocity.y = -2f; // simple stick-to-ground force

                Controller.Move(moveVelocity * Time.deltaTime);

                if (moveDir != Vector3.zero)
                {
                    // Rotate towards movement
                    Transform.forward = Vector3.Slerp(Transform.forward, moveDir, Time.deltaTime * 10f);
                }
            }
                
        }
    }
}
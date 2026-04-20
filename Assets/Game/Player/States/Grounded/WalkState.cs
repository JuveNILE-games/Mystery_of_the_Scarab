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
                // Update animation direction from world XZ — works for both human input
                // (already projected to world space by PlayerInputInitializer) and AI input
                // (NavMesh desiredVelocity, inherently world-space).
                if (Owner != null && _windroseAnimator != null && Owner.WorldMoveInput.sqrMagnitude > 0.01f)
                {
                    _windroseAnimator.SetDirection(Owner.GetCardinalDirection(
                        new Vector2(Owner.WorldMoveInput.x, Owner.WorldMoveInput.z)));
                }

                float speed = Owner.Data.Value != null ? Owner.Data.Value.WalkSpeed : 3f;
                Vector3 moveVelocity = moveDir * speed;

                // Stick-to-ground force. -2f was too weak for a dynamic NavMesh surface that
                // has slight height variation between bakes — the companion flickered into
                // AirborneState for 1-2 frames at a time, switching from Walk (direct velocity)
                // to Air Control (acceleration-based) and killing its momentum. -5f keeps the
                // CharacterController firmly pressed against the surface across all slope angles
                // within the NavMesh's bake tolerance.
                moveVelocity.y = -5f;

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
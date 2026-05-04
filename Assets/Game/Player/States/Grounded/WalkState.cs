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
            if (Owner != null)
            {
                // Update animation direction from world XZ — works for both human input
                // (already projected to world space by PlayerInputInitializer) and AI input
                // (NavMesh desiredVelocity, inherently world-space).
                if (_windroseAnimator != null && Owner.WorldMoveInput.sqrMagnitude > 0.01f)
                {
                    _windroseAnimator.SetDirection(Owner.GetCardinalDirection(
                        new Vector2(Owner.WorldMoveInput.x, Owner.WorldMoveInput.z)));
                }

                if (moveDir != Vector3.zero)
                {
                    // Rotate towards movement
                    Transform.forward = Vector3.Slerp(Transform.forward, moveDir, Time.deltaTime * 10f);
                }
            }
                
        }
    }
}

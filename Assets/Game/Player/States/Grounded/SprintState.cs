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
            if (Owner != null)
            {
                Owner.SpeedMultiplier = Owner.Data.Value != null ? Owner.Data.Value.SprintMultiplier : 2f;
            }
            if (Animator != null)
            {
                _windroseAnimator = Animator.Play<WindroseAnimator>("Sprint");
                if (Owner != null) _windroseAnimator?.SetDirection(Owner.LastMoveDirection);
            }
        }

        public override void OnExit()
        {
            if (Owner != null)
            {
                Owner.SpeedMultiplier = 1f;
            }
            base.OnExit();
        }
        
        public override void OnUpdate()
        {
            base.OnUpdate();
            
            Vector3 moveDir = GetMoveDirection();
            if (Owner != null)
            {
                // Update animation direction from world XZ — works for both human and AI input.
                if (_windroseAnimator != null && Owner.WorldMoveInput.sqrMagnitude > 0.01f)
                {
                    _windroseAnimator.SetDirection(Owner.GetCardinalDirection(
                        new Vector2(Owner.WorldMoveInput.x, Owner.WorldMoveInput.z)));
                }
                
                if (moveDir != Vector3.zero)
                {
                    // Rotate towards movement
                    Transform.forward = Vector3.Slerp(Transform.forward, moveDir, Time.deltaTime * 15f);
                }
            }
                
        }
    }
}

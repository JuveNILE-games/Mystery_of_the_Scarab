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
        }
    }
}

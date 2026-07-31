using UnityEngine;

namespace Game.Player.States.Grounded{
    public class JumpState : PlayerState
    {
        
        public JumpState() : base("Jump") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            if (Owner != null)
            {
                Owner.ConsumeJumpEligibility();
                Owner.VerticalVelocity = Owner.Data.Value != null ? Owner.Data.Value.JumpForce : 5f;
            }
            if (Animator != null) Animator.Play("Jump");
        }
    }
}

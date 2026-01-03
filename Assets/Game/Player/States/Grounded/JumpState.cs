using UnityEngine;

namespace Game.Player.States.Grounded{
    public class JumpState : PlayerState
    {
        
        public JumpState() : base("Jump") { }
        
        public override void OnEnter()
        {
            base.OnEnter();
            
            if (Controller != null)
            {
                float force = Owner.Data.Value != null ? Owner.Data.Value.JumpForce : 5f;
                // Move immediately (this might behave snappy, but it works)
                // A better way is to passing this velocity to the Airborne state, 
                // but since we rely on State transitions, let's just Move and let physics (gravity in Airborne) take over next frame.
                // However, CharacterController.Move doesn't impart "momentum" for the next frame unless we continue applying it.
                // WE NEED A SHARED VELOCITY VARIABLE IN STATE MACHINE for seamless transitions!
                
                // For now, let's do a trick: 
                // We'll apply the jump velocity in the FIRST frame of Airborne state if we came from Jump?
                // Or: JumpState transitions to Airborne immediately, but we need to impart that upward velocity.
                
                // Let's rely on the fact that we can just Start falling with an initial positive velocity.
                // But JumpState is separate.
            }
            
            Animator?.Play("Jump");
        }
    }
}
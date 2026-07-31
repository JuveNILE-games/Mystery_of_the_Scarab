using UnityEngine;

namespace Game.Player.States.Ability{
    public class SecondaryAbilityState : PlayerState{
        private float abilityDuration = 0.8f;
        private float startTime;

        public SecondaryAbilityState() : base("SecondaryAbility"){
        }

        public override void OnEnter(){
            base.OnEnter();
            startTime = Time.time;

            if (Animator != null) Animator.Play("SecondaryAbility");
            Debug.Log("[PlayerSM] Executing Secondary Ability");

            // Execute ability logic here
            ExecuteSecondaryAbility();
        }

        public override void OnUpdate(){
            base.OnUpdate();

            // Mark as finished after duration
            if (Time.time - startTime >= abilityDuration)
            {
                if (Owner != null) Owner.MarkSecondaryAbilityFinished();
            }
            else
            {
                if (Owner != null)
                {
                    Owner.AdditionalVelocity = Vector3.up * 10f;
                }
            }
        }

        private void ExecuteSecondaryAbility(){
            // Example: Apply upward force
            if (Controller != null)
            {
                // Applying upward burst
                 Debug.Log("Upward Burst!");
            }
        }
    }
}

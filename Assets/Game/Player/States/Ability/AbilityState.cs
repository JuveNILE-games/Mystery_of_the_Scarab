using Core.Systems.StateMachine;
using UnityEngine;

namespace Game.Player.States.Ability{
    /// <summary>
    /// Hierarchical state for abilities.
    /// Contains: PrimaryAbility, SecondaryAbility
    /// </summary>
    public class AbilityState : HierarchicalState<PlayerStateMachine, PlayerState>{
        private PrimaryAbilityState primaryAbility;
        private SecondaryAbilityState secondaryAbility;

        public AbilityState() : base("Ability"){
        }

        protected override void Initialize(){
            primaryAbility = AddChildState(new PrimaryAbilityState());
            secondaryAbility = AddChildState(new SecondaryAbilityState());

            SetInitialChildState(primaryAbility);

            SetupTransitions();
        }

        private void SetupTransitions(){
            // Primary ability transitions
            primaryAbility
                .When(() => Owner.IsPrimaryAbilityFinished(), secondaryAbility, priority: 10);

            // Secondary ability transitions  
            secondaryAbility
                .When(() => Owner.IsSecondaryAbilityFinished(), primaryAbility, priority: 10);
        }

        public override void OnEnter(){
            base.OnEnter();
            Debug.Log("[PlayerSM] Entered Ability state");
        }

        public override void OnExit(){
            Debug.Log("[PlayerSM] Exited Ability state");
            base.OnExit();
        }
    }
}
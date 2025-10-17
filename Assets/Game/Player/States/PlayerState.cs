using Core.Systems.StateMachine.Components;
using Core.Systems.StateMachine.Core;
using UnityEngine;

namespace Game.Player.States
{
    [CreateAssetMenu(fileName = "PlayerState", menuName = "State Machine/States/Player State")]
    public class PlayerState : State
    {
        protected IStateMachineComponent StateComponent => StateMachine.Context as IStateMachineComponent;
        
        public override void OnEnter()
        {
            base.OnEnter();
            Debug.Log($"Entering Player State: {name}");
        }
        
        public override void OnExit()
        {
            base.OnExit();
            Debug.Log($"Exiting Player State: {name}");
        }
    }
}
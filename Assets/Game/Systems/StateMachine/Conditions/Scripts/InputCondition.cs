using Core.Systems.InputManagement;
using Game.Player;
using loophouse.ScriptableStates;
using Obvious.Soap;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Game.Systems.StateMachine.Conditions.Scripts{
    [CreateAssetMenu(menuName = "Scriptable State Machine/Conditions/InputCondition", fileName = "new InputCondition")]
    public class InputCondition : ScriptableCondition
    {
        [SerializeField] private string actionName;
        public BoolVariable isTrue;
        

        public override bool Verify(StateComponent statesComponent)
        {
            // Get the PlayerInputInitializer from the StateComponent
            PlayerInputInitializer playerInputInitializer = statesComponent.GetComponent<PlayerInputInitializer>();
            if (playerInputInitializer == null)
            {
                Debug.LogError("PlayerInputInitializer not found on the StateComponent.");
                return false;
            }
            // Get the InputReader from the PlayerInputInitializer
            InputReader inputReader = playerInputInitializer.PlayerInputReader;
            if (inputReader == null)
            {
                Debug.LogError("InputReader not found on the PlayerInputInitializer.");
                return false;
            }
            // Get the action from the InputReader
            InputAction action = inputReader.Actions.FindAction(actionName);
            if (action == null)
            {
                Debug.LogError($"Action '{actionName}' not found in InputReader.");
                return false;
            }
            
            // Check if the action was preformed this frame
            ButtonControl button = action.activeControl as ButtonControl;
            if (button == null)
            {
                return false;
            }

            if (button.isPressed)
            {
                Debug.Log($"Action '{actionName}' is pressed");
            }
            isTrue.Value = button is { isPressed: true };
            return isTrue.Value;
        }
    }
}
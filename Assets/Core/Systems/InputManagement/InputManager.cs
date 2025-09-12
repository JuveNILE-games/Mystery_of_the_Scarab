using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Systems.InputManagement{
    public class InputManager : InputAutoSubscriber{
        public Vector2 movementDirection { get; private set; } = Vector2.zero;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        private void Awake(){
            inputReader.Initialize();
        }

        [InputAction("Move", InputActionPhase.Started)]
        private void OnMove(InputAction.CallbackContext ctx){
            Debug.Log($"Move action triggered {ctx.phase}");
            movementDirection = ctx.ReadValue<Vector2>();
        }

        [InputAction("Move")]
        private void OnMovePerformed(InputAction.CallbackContext ctx){
            Debug.Log($"Move action triggered {ctx.phase}");
        }

        [InputAction("Move", InputActionPhase.Canceled)]
        private void OnMoveCancelled(InputAction.CallbackContext ctx){
            Debug.Log($"Move action triggered {ctx.phase}");
        }

        [InputAction("Sprint", InputActionPhase.Started)]
        private void OnSprint(InputAction.CallbackContext ctx){
            Debug.Log($"Sprint action triggered {ctx.phase}");
        }

        [InputAction("Sprint")]
        private void OnSprintPerformed(InputAction.CallbackContext ctx){
            Debug.Log($"Sprint action triggered {ctx.phase}");
        }

        [InputAction("Sprint", InputActionPhase.Canceled)]
        private void OnSprintCancelled(InputAction.CallbackContext ctx){
            Debug.Log($"Sprint action triggered {ctx.phase}");
        }

        [InputAction("Jump", InputActionPhase.Started)]
        private void OnJump(InputAction.CallbackContext ctx){
            Debug.Log($"Sprint action triggered {ctx.phase}");
        }

        [InputAction("Jump")]
        private void OnJumpPerformed(InputAction.CallbackContext ctx){
            Debug.Log($"Sprint action triggered {ctx.phase}");
        }

        [InputAction("Jump", InputActionPhase.Canceled)]
        private void OnJumpCancelled(InputAction.CallbackContext ctx){
            Debug.Log($"Sprint action triggered {ctx.phase}");
        }

        [InputAction("PrimaryAbility", InputActionPhase.Started)]
        private void OnPrimaryAbility(InputAction.CallbackContext ctx){
            Debug.Log($"Primary Ability: {ctx.phase}");
        }

        [InputAction("PrimaryAbility")]
        private void OnPrimaryAbilityPerformed(InputAction.CallbackContext ctx){
            Debug.Log($"Primary Ability: {ctx.phase}");
        }

        [InputAction("PrimaryAbility", InputActionPhase.Canceled)]
        private void OnPrimaryAbilityCancelled(InputAction.CallbackContext ctx){
            Debug.Log($"Primary Ability: {ctx.phase}");
        }
        [InputAction("SecondaryAbility", InputActionPhase.Started)]
        private void OnSecondaryAbility(InputAction.CallbackContext ctx){
            Debug.Log($"Secondary Ability: {ctx.phase}");
        }
        [InputAction("SecondaryAbility")]
        private void OnSecondaryAbilityPerformed(InputAction.CallbackContext ctx){
            Debug.Log($"Secondary Ability: {ctx.phase}");
        }
        [InputAction("SecondaryAbility", InputActionPhase.Canceled)]
        private void OnSecondaryAbilityCancelled(InputAction.CallbackContext ctx){
            Debug.Log($"Secondary Ability: {ctx.phase}");
        }
    }
}
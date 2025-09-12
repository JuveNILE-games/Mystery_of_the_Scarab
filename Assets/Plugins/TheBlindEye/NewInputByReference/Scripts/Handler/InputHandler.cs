using UnityEngine;
using UnityEngine.InputSystem;

namespace NewInputByReference.Handler
{
    [RequireComponent(typeof(PlayerInput))]
    public class InputHandler : MonoBehaviour
    {
        [SerializeField] private BindingsImages bindingsImages;

        private void Awake()
        {
            NewInput.SetPlayerInput(GetComponent<PlayerInput>());
            NewInput.SetBindingImages(bindingsImages);
        }
        
        // This script can be expanded, like so:
        // public bool Interact => NewInput.GetButtonDown("Interact");
        // public Vector2 Movement => NewInput.GetVector2("Movement");
        // ...
    }
}
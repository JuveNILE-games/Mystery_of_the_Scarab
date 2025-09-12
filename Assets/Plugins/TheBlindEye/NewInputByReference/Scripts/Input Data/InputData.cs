using UnityEngine;
using UnityEngine.InputSystem;
using TheBlindEye.Utility.NewInputByReference;

namespace NewInputByReference 
{
    public abstract class InputData : ScriptableObject
    {
        [SerializeField] private InputActionReference inputAction;

        public InputAction InputAction { get; private set; }

        protected string ControlType => inputAction.action?.expectedControlType; 
        private bool IsNull => inputAction == null;

        protected virtual void OnValidate()
        {
            if (IsNull)
                return;

            new Error06(inputAction.action.name).Trow();
            SetInputAction(null);   
        }

        private void OnEnable() => InputAction = IsNull ? null : inputAction.action;

        public void SetInputAction(InputActionReference newInputAction) => inputAction = newInputAction;
    }
}

using UnityEngine.InputSystem;

namespace Core.Systems.InputManagement{
    [System.AttributeUsage(System.AttributeTargets.Method)]
    public class InputActionAttribute : System.Attribute {
        public string ActionName { get; }
        public InputActionPhase Phase { get; }

        public InputActionAttribute(string actionName, InputActionPhase phase = InputActionPhase.Performed) {
            ActionName = actionName;
            Phase = phase;
        }
    }
}
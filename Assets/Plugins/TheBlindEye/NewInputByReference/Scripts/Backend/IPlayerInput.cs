using UnityEngine.InputSystem;

namespace NewInputByReference.BackEnd
{
    internal interface IPlayerInput
    {
        public string Name { get; }

        public InputAction GetAction(string actionName);
        
        public void SwitchActionMap(string actionMap);
        public void TriggerActionMap(string actionMap, bool enable);

        public void RemoveAllBindingOverrides();
        public string SaveBindingOverridesAsJson();
        public void LoadBindingOverridesFromJson(string rebinds);
    }
}
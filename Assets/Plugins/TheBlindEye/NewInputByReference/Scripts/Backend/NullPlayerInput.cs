using UnityEngine.InputSystem;
using TheBlindEye.Utility.NewInputByReference;

namespace NewInputByReference.BackEnd
{
    internal class NullPlayerInput : IPlayerInput
    {
        public string Name => null;

        public InputAction GetAction(string actionName)
        {
            new Error07().Trow();
            return null;
        }

        public void SwitchActionMap(string actionMap) => new Error07().Trow();
        public void TriggerActionMap(string actionMap, bool enable) => new Error07().Trow();
        
        public void RemoveAllBindingOverrides() => new Error07().Trow();

        public string SaveBindingOverridesAsJson()
        {
            new Error07().Trow();
            return null;
        }
        
        public void LoadBindingOverridesFromJson(string rebinds) => new Error07().Trow();
    }
}
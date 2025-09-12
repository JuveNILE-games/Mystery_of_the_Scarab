using UnityEngine.InputSystem;

namespace NewInputByReference.BackEnd
{
    internal class DefinedPlayerInput : IPlayerInput
    {
        private readonly PlayerInput _playerInput;
        
        public string Name => _playerInput.name;
        
        public DefinedPlayerInput(PlayerInput playerInput) => _playerInput = playerInput;
        
        public InputAction GetAction(string actionName) => _playerInput.actions[actionName];
   
        public void SwitchActionMap(string actionMap) => _playerInput.SwitchCurrentActionMap(actionMap);

        public void TriggerActionMap(string actionMap, bool enable)
        {
            var foundActionMap = _playerInput.actions.FindActionMap(actionMap);
            
            if (enable) 
                foundActionMap.Enable();
            else 
                foundActionMap.Disable();
        }

        public void RemoveAllBindingOverrides() => _playerInput.actions.RemoveAllBindingOverrides();

        public string SaveBindingOverridesAsJson() => _playerInput.actions.SaveBindingOverridesAsJson();
        public void LoadBindingOverridesFromJson(string rebinds) => _playerInput.actions.LoadBindingOverridesFromJson(rebinds);
    }
}
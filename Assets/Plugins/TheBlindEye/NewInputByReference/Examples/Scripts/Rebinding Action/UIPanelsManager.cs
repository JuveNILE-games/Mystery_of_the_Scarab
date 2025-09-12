using System;
using UnityEngine;

namespace NewInputByReference.Examples
{
    public class UIPanelsManager : MonoBehaviour
    {
        public static event Action<bool> OnMenuUpdate;
        
        [Header("Settings")]
        [SerializeField] private string menuActionName;
        [SerializeField] private string playerActionMapName;
        [SerializeField] private string menuActionMapName;
        
        [Header("References")]
        [SerializeField] private GameObject gamePanel;
        [SerializeField] private GameObject menuPanel;

        private bool _isOpen;
        private void Update()
        {
            // In this case, I would personally call the function thru an event. (same for changing the action map)
            // E.g:
            // InputHandler.cs:
            // public bool Pause => NewInput.GetButtonDown("Pause Menu");
            //
            // PlayerManager.cs:
            // public static event Action OnPressMenu; (to be more detach I have a separate static class named UIEvents)
            // private InputHandler _inputHandler;
            // private void Awake() => _inputHandler = GetComponent<InputHandler>();
            // private void Update() => if(_inputHandler.Pause) OnPressMenu?.Invoke();
            //
            // UIPanelsManager.cs:
            // private OnEnable() => PlayerManager.OnPressMenu += TriggerMenu;
            // private OnDisable() => PlayerManager.OnPressMenu -= TriggerMenu;
            if (!UIRebindingButton.InRebinding && NewInput.GetButtonDown(menuActionName))
                TriggerMenu();
        }

        private void TriggerMenu()
        {
            _isOpen = !_isOpen;

            OnMenuUpdate?.Invoke(_isOpen);
            NewInput.SwitchActionMap(_isOpen ? menuActionMapName : playerActionMapName);

            gamePanel.SetActive(!_isOpen);
            menuPanel.SetActive(_isOpen);
        }
    }
}
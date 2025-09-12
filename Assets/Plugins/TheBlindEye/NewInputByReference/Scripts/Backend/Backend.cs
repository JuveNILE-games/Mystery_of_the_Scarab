using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using NewInputByReference.Bindings;
using TheBlindEye.Utility.NewInputByReference;

namespace NewInputByReference.BackEnd
{
    internal static class Backend
    {
        private enum ControlType
        {
            Button = 44, 
            Axis = 50, 
            Vector2 = -36, 
            Vector3 = -35
        }

        private const string REBINDS_DIRECTORY = "NewInputByReference.Rebinds"; 
        private const string DEFAULT_CANCEL_PATH = "/Keyboard/escape";
        private const string DEFAULT_CANCEL_BINDING = "<Keyboard>/escape";
        private const string DEFAULT_NULL_BINDING = "NULL";

        private static IPlayerInput _playerInput = new NullPlayerInput();
        private static BindingsImages _bindingsImages;
        private static InputActionRebindingExtensions.RebindingOperation _rebindingOperation;

        private static string RebindsDirectory => REBINDS_DIRECTORY + '.' + _playerInput.Name;
        private static string DefaultCancelBinding
        {
            get
            {
                #if INPUT_SYSTEM_VERSION_140
                    return DEFAULT_CANCEL_BINDING;
                #else
                    return DEFAULT_NULL_BINDING;
                #endif
            }
        }

        #region Utility Functions
        
        public static void SetPlayerInput(PlayerInput newPlayerInput)
        {
            if (newPlayerInput == null)
            {
                _playerInput = new NullPlayerInput();
                return;
            }

            _playerInput = new DefinedPlayerInput(newPlayerInput);
            LoadRebinds();
        }

        public static void SetBindingsImages(BindingsImages newBindingsImages) => _bindingsImages = newBindingsImages;
        
        public static InputAction GetAction(string actionName) => _playerInput.GetAction(actionName);

        private static ControlType GetCastedControlType(InputAction inputAction)
        {
            string controlType = inputAction.expectedControlType;
            return (ControlType)controlType[controlType.Length - 1] - controlType[0];
        }
        
        #endregion

        #region Action Map Functions
        
        public static void SwitchActionMap(string actionMap)
        {
            _playerInput.SwitchActionMap(actionMap);
        }
        
        public static void EnableActionMap(string actionMap)
        {
            _playerInput.TriggerActionMap(actionMap, true);
        }
        
        public static void DisableActionMap(string actionMap)
        {
            _playerInput.TriggerActionMap(actionMap, false);
        }
        
        #endregion
        
        #region Rebinding Action Functions
        
        public static void StartRebinding(InputAction inputAction, int bindingIndex, bool allCompositeBindings, 
            Action onComplete, string[] cancelBindings, string[] excludedBindings)
        {
            if (!CheckBindingIndex())
                return;
            
            #if !INPUT_SYSTEM_VERSION_140
                var initialPath = inputAction.bindings[bindingIndex].effectivePath;
            #endif

            string cancelPath = cancelBindings[0] ?? DefaultCancelBinding;
            
            SetPathsBindings(excludedBindings);
            SetPathsBindings(cancelBindings);

            bool inputActionEnabled = inputAction.enabled;
            inputAction.Disable();

            _rebindingOperation = inputAction.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding(excludedBindings[0])
                .WithControlsExcluding(excludedBindings[1])
                .WithCancelingThrough(cancelBindings[0])
                .WithCancelingThrough(cancelBindings[1])
                .WithCancelingThrough(cancelPath)
                
                .OnMatchWaitForAnother(0.1f)
                .OnGeneratePath(CheckRebinding)
                .OnCancel(operation => RebindingCancel())
                .OnComplete(operation => RebindingComplete())
                .Start();

            if(inputActionEnabled)
                inputAction.Enable();

            void RebindingComplete()
            {
                _rebindingOperation.Dispose();
                _rebindingOperation = null;
                
                if (!allCompositeBindings)
                {
                    onComplete?.Invoke();
                    SaveRebinds();
                    return;
                }

                int nextBindingIndex = bindingIndex + 1;
                if(nextBindingIndex < inputAction.bindings.Count && inputAction.bindings[nextBindingIndex].isPartOfComposite)
                {
                    StartRebinding(inputAction, nextBindingIndex, true, onComplete, cancelBindings, excludedBindings);
                    return;
                }

                onComplete?.Invoke();
                SaveRebinds();
            }
            
            void RebindingCancel()
            {
                _rebindingOperation.Dispose();
                _rebindingOperation = null;
                
                onComplete?.Invoke();
            }

            string CheckRebinding(InputControl inputControl)
            {
                #if INPUT_SYSTEM_VERSION_140
                    return null;
                #else
                    string currentPath = inputControl.path;
                    return currentPath == DEFAULT_CANCEL_PATH ? initialPath : null;
                #endif
            }

            void SetPathsBindings(IList<string> pathsList)
            {
                for (int i = 0; i < pathsList.Count; i++)
                {
                    if (pathsList[i] != null) 
                        continue;
                    
                    pathsList[i] = DEFAULT_NULL_BINDING;
                }
            }

            bool CheckBindingIndex()
            {
                if (bindingIndex < inputAction.bindings.Count)
                    return true;

                new Error05(inputAction.name, bindingIndex, inputAction.bindings.Count).Trow();
                return false;
            }
        }

        public static void Rebind(InputAction inputAction, string bindingPath, int bindingIndex)
        {
            if (CheckForNull(inputAction))
                return;
            
            inputAction.ApplyBindingOverride(bindingIndex, bindingPath);
            SaveRebinds();
        }

        public static (string, int) GetBindingName(InputAction inputAction, int bindingIndex, bool allCompositeBindings, NameOptions option)
        {
            if (CheckForNull(inputAction))
                return (null, 0);

            var castedOption = (InputControlPath.HumanReadableStringOptions)option;
            int receivedCharIndex = 0;
            
            string receivedBindingName = GetConstructedBindingName();
            return (receivedBindingName, receivedCharIndex);

            string GetConstructedBindingName()
            {
                int lastCompositeIndex = allCompositeBindings ? GetLastCompositeIndex() : bindingIndex;
                var bindingName = new StringBuilder();
                
                for (int i = bindingIndex; i <= lastCompositeIndex; i++)
                {
                    string binding = InputControlPath.ToHumanReadableString(
                        inputAction.bindings[i].effectivePath, castedOption);
                    
                    if (castedOption == InputControlPath.HumanReadableStringOptions.UseShortNames)
                        binding = GetTransformedUseShortName(binding);
                    
                    bindingName.Append(binding);
                
                    if(i != lastCompositeIndex)
                        bindingName.Append('/');
                }
                
                return bindingName.ToString();
            }
            
            int GetLastCompositeIndex()
            {
                var castedControlType = GetCastedControlType(inputAction);
                return castedControlType switch
                {
                    ControlType.Axis => bindingIndex + 1,
                    ControlType.Vector2 => bindingIndex + 3,
                    ControlType.Vector3 => bindingIndex + 5,
                    _ => bindingIndex
                };
            }

            string GetTransformedUseShortName(string bindingName)
            {
                int length = bindingName.Length;
            
                int charIndex = 8;
                switch (bindingName[length - charIndex])
                {
                    case 'e':
                        charIndex += 3;
                        break;
                   
                    case 'G':
                        charIndex += 2;
                        break;
                }

                receivedCharIndex = charIndex;
                return bindingName.Substring(0, length - charIndex);
            }
        }
        
        public static Sprite GetBindingSprite(InputAction inputAction, int bindingIndex)
        {
            if (_bindingsImages == null)
            {
                new Error04().Trow();
                return null;
            }

            (string bindingName, int charIndex) = GetBindingName(inputAction, bindingIndex, false,
                NameOptions.UseShortNames);

            return string.IsNullOrEmpty(bindingName) ? null : _bindingsImages.GetBindingSprite(bindingName, (KeyType)charIndex);
        }
        
        public static void ResetRebinds()
        {
            #if INPUT_SYSTEM_VERSION_111
                _playerInput.RemoveAllBindingOverrides();
                PlayerPrefs.SetString(RebindsDirectory, string.Empty);
            #else
                new Error03().Trow();
            #endif
        }

        public static void ResetRebind(InputAction inputAction, int bindingIndex)
        {
            if (CheckForNull(inputAction))
                return;
            
            if (bindingIndex == 0) 
                inputAction.RemoveAllBindingOverrides(); 
            else 
                inputAction.RemoveBindingOverride(bindingIndex);
            
            SaveRebinds();
        }
        
        private static void SaveRebinds()
        {
            #if INPUT_SYSTEM_VERSION_111
                string rebinds = _playerInput.SaveBindingOverridesAsJson();
                
                PlayerPrefs.SetString(RebindsDirectory, rebinds);
            #else
                new Error03().Trow();
            #endif
        }

        private static void LoadRebinds()
        {
            #if INPUT_SYSTEM_VERSION_111
                string rebinds = PlayerPrefs.GetString(RebindsDirectory, string.Empty);

                if (string.IsNullOrEmpty(rebinds))
                    return;
            
                _playerInput.LoadBindingOverridesFromJson(rebinds);
            #else
                new Error03().Trow();
            #endif
        }
        
        #endregion
        
        #region Input Action Functions
        
        public static bool GetButtonDown(InputAction inputAction)
        {
            if (!CheckInputAction(inputAction, ControlType.Button))
                return false;
            
            bool buttonClicked = inputAction.triggered && inputAction.ReadValue<float>() > 0;
            return buttonClicked;
        }

        public static bool GetButton(InputAction inputAction)
        {
            return CheckInputAction(inputAction, ControlType.Button);
        }

        public static bool GetButtonUp(InputAction inputAction)
        {
            if (CheckForNull(inputAction) || !CheckControlType(inputAction, ControlType.Button))
                return false;

            bool buttonReleased = inputAction.triggered && inputAction.ReadValue<float>() <= 0;
            return buttonReleased; 
        }
        
        public static float GetAxis(InputAction inputAction)
        {
            return CheckInputAction(inputAction, ControlType.Axis) ? inputAction.ReadValue<float>() : 0f; 
        }
        
        public static Vector2 GetVector2(InputAction inputAction)
        {
            return CheckInputAction(inputAction, ControlType.Vector2) ? inputAction.ReadValue<Vector2>() : Vector2.zero; 
        }

        public static Vector3 GetVector3(InputAction inputAction)
        {
            return CheckInputAction(inputAction, ControlType.Vector3) ? inputAction.ReadValue<Vector3>() : Vector3.zero; 
        }
        
        #endregion

        #region Check Functions

        private static bool CheckInputAction(InputAction inputAction, ControlType controlType)
        {
            return !CheckForNull(inputAction) && CheckInputActionPhase(inputAction) && CheckControlType(inputAction, controlType);
        } 
        
        public static bool CheckForNull(InputAction inputAction)
        {
            if (inputAction != null)
                return false;

            new Error01().Trow();
            return true;
        }
        
        private static bool CheckInputActionPhase(InputAction inputAction)
        {
            var inputActionPhase = inputAction.phase;
            return inputActionPhase != InputActionPhase.Disabled && inputActionPhase != InputActionPhase.Waiting;
        }
        
        // Bug Solved: CheckControlType returned only false for Vector2 and Vector3 (found by martyr)
        private static bool CheckControlType(InputAction inputAction, ControlType expectedControlType)
        {
            var castedControlType = GetCastedControlType(inputAction);
            if (castedControlType == expectedControlType)
                return true;

            new Error02(inputAction.name, inputAction.expectedControlType,
                expectedControlType.ToString(), inputAction.actionMap.name).Trow();
            return false;
        }

        #endregion
        
    }
}
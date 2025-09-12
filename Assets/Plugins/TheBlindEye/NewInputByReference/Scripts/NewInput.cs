using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using NewInputByReference.BackEnd;

namespace NewInputByReference
{
    public static class NewInput
    {  
        public static void SetPlayerInput(PlayerInput newPlayerInput) => Backend.SetPlayerInput(newPlayerInput);
        public static void SetBindingImages(BindingsImages newBindingsImages) => Backend.SetBindingsImages(newBindingsImages);

        #region Action Map Functions
        
        /// <summary>
        /// Change the current Action Map to the one indicated by "actionMap".
        /// </summary>
        /// <param name="actionMap">Can be found in the Input Action Asset, under the "Action Maps" column.</param>
        public static void SwitchActionMap(string actionMap)
        {
            Backend.SwitchActionMap(actionMap);
        }
        
        /// <summary>
        /// Enable the Action Map indicated by "actionMap". Other Action Maps will not be disabled if they are currently active.
        /// </summary>
        /// <param name="actionMap">Can be found in the Input Action Asset, under the "Action Maps" column.</param>
        public static void EnableActionMap(string actionMap)
        {
            Backend.EnableActionMap(actionMap);
        }
        
        /// <summary>
        /// Disable the Action Map indicated by "actionMap". Other Action Maps will not be disabled if they are currently active.
        /// </summary>
        /// <param name="actionMap">Can be found in the Input Action Asset, under the "Action Maps" column.</param>
        public static void DisableActionMap(string actionMap)
        {
            Backend.DisableActionMap(actionMap);
        }
        
        #endregion
        
        #region Rebinding Action Functions

        /// <summary>
        /// Start the rebinding process for the input action indicated by "inputData", by waiting for a key input from the player. 
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference.<br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "inputData", binding that will be rebounded. <br /><br /> 
        /// If "isComposite" is true, "bindingIndex" becomes the starting index of the composite. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered) <br /><br /></param>
        /// <param name="allCompositeBindings">Can be true only if the input action indicated by "inputData" is of Action Type = Value and Control Type = Axis/Vector 2/Vector 3. <br /><br />
        /// If it's true, all the input actions from the same composite, as the input action indicated by "inputData", will start the rebinding process one after another.
        /// After all of them have been rebounded, the function will exit. (check Documentation -> "6.What are some tips?", to understand what is a composite) <br /><br /></param>
        /// <param name="onComplete">Delegate that invokes when the rebinding process is completed. <br /><br /></param>
        /// <param name="cancelInputData">Input action indicated by "cancelInputData" that upon pressing makes the function exit, and stops listening to the player's input. (Default Action Path = &lt;Keyboard&gt;/escape) (cancelInputData.InputAction.bindings.Count &lt;= 2)<br /><br /></param>
        /// <param name="excludedInputData">Input action indicated by "excludedInputData" that upon press will not rebind(nor exit the function) the input action indicated by "inputData". (excludedInputData.InputAction.bindings.Count &lt;= 2)</param>
        /// <remarks>If you'd like it constructed using the builder pattern or to have an overload with "actionName" instead of "inputData", send me an email at: blindeyethe@gmail.com. <br /><br />
        /// Builder Pattern example:
        /// <code>
        /// NewInput.StartRebinding(inputData)
        ///       .WithBindingIndex(0)
        ///       .WithComposite(isComposite)
        ///       .WithCancel(cancelInputData)
        ///       .WithExcluded(excludedInputData)
        ///       .OnComplete(OnComplete)
        ///       .Start();
        /// </code>
        ///  (".With" functions and ".OnComplete" are optional)
        /// </remarks>
        public static void StartRebinding(InputData inputData, int bindingIndex = 0, bool allCompositeBindings = false, 
            Action onComplete = null, InputData cancelInputData = null, InputData excludedInputData = null)
        {
            var inputAction = inputData.InputAction;
            if (Backend.CheckForNull(inputAction))
                return;
            
            string[] cancelPaths = {null, null};
            ConvertInputDataToPath(cancelInputData, cancelPaths);

            string[] excludedPaths = {null, null};
            ConvertInputDataToPath(excludedInputData, excludedPaths);
            
            Backend.StartRebinding(inputAction, bindingIndex, allCompositeBindings, onComplete, cancelPaths, excludedPaths);

            void ConvertInputDataToPath(InputData passedInputData, IList<string> pathsList)
            {
                if (!passedInputData)
                    return;

                var receivedInputAction = passedInputData.InputAction;
                if (Backend.CheckForNull(receivedInputAction))
                    return;

                int index = 0;
                var bindings = receivedInputAction.bindings;
                foreach (var binding in bindings)
                {
                    pathsList[index++] = binding.path;
                    
                    if (index == 2)
                        break;
                }
            }
        }
        
        public static void StartRebinding(InputAction inputAction, int bindingIndex = 0, bool allCompositeBindings = false, 
            Action onComplete = null, InputData cancelInputData = null, InputData excludedInputData = null)
        {
            
            if (Backend.CheckForNull(inputAction))
                return;
            
            string[] cancelPaths = {null, null};
            ConvertInputDataToPath(cancelInputData, cancelPaths);

            string[] excludedPaths = {null, null};
            ConvertInputDataToPath(excludedInputData, excludedPaths);
            
            Backend.StartRebinding(inputAction, bindingIndex, allCompositeBindings, onComplete, cancelPaths, excludedPaths);

            void ConvertInputDataToPath(InputData passedInputData, IList<string> pathsList)
            {
                if (!passedInputData)
                    return;

                var receivedInputAction = passedInputData.InputAction;
                if (Backend.CheckForNull(receivedInputAction))
                    return;

                int index = 0;
                var bindings = receivedInputAction.bindings;
                foreach (var binding in bindings)
                {
                    pathsList[index++] = binding.path;
                    
                    if (index == 2)
                        break;
                }
            }
        }


        /// <summary>
        /// Rebind the input action indicated by "inputData".
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference. <br /><br /></param>
        /// <param name="bindingPath">Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Path. (e.g. &lt;Keyboard&gt;/e, &lt;Gamepad&gt;/leftStick) <br /><br />
        /// Remark: To avoid GC, the passed variable should be cached into the class. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "inputData", binding that will be rebounded. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered) </param>
        public static void Rebind(InputData inputData, string bindingPath, int bindingIndex = 0)
        {
            var inputAction = inputData.InputAction;
            Backend.Rebind(inputAction, bindingPath, bindingIndex);
        }
        
        /// <summary>
        /// Rebind the input action indicated by "actionName".
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. <br /><br /></param>
        /// <param name="bindingPath">Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Path. (e.g. &lt;Keyboard&gt;/e, &lt;Gamepad&gt;/leftStick) <br /><br />
        /// Remark: To avoid GC, the passed variable should be cached into the class. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "actionName", binding that will be rebounded. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)</param>
        public static void Rebind(string actionName, string bindingPath, int bindingIndex = 0)
        {
            var inputAction = Backend.GetAction(actionName);
            Backend.Rebind(inputAction, bindingPath, bindingIndex);
        }

        /// <summary>
        /// Get the name of the binding indicated by "inputData". (e.g. W, E, Up Arrow, Tab, etc.)
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference.<br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "inputData", binding that will provide the name. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)<br /><br /></param>
        /// <param name="allCompositeBindings">Can be true only if the input action indicated by "inputData" is of Action Type = Value and Control Type = Axis/Vector 2/Vector 3. <br /><br />
        /// If it's true, all the input actions from the same composite as the input action, indicated by "inputData", will return their bindings' names separated by "/" (e.g. W/S/A/D, S/W, A/D), 
        /// and "bindingIndex" will become the starting index of the composite
        /// (check Documentation -> "6.What are some tips?", to understand what is a composite and its starting index) <br /><br /></param>
        /// <param name="nameOption">Options for customizing the behavior of the returned string.</param>
        public static string GetBindingName(InputData inputData, int bindingIndex = 0, 
            bool allCompositeBindings = false, NameOptions nameOption = NameOptions.OmitDevice)
        {
            var inputAction = inputData.InputAction;
            return Backend.CheckForNull(inputAction) ? "NULL" : 
                Backend.GetBindingName(inputAction, bindingIndex, allCompositeBindings, nameOption).Item1;
        }

        /// <summary>
        /// Get the name of the binding indicated by "actionName". (e.g. W, E, Up Arrow, Tab, etc.)
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "actionName", binding that will provide the name. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)<br /><br /></param>
        /// <param name="allCompositeBindings">Can be true only if the input action indicated by "actionName" is of Action Type = Value and Control Type = Axis/Vector 2/Vector 3. <br /><br />
        /// If it's true, all the input actions from the same composite as the input action, indicated by "actionName", will return their bindings' names separated by "/" (e.g. W/S/A/D, S/W, A/D), 
        /// and "bindingIndex" will become the starting index of the composite
        /// (check Documentation -> "6.What are some tips?", to understand what is a composite and its starting index) <br /><br /></param>
        /// <param name="nameOption">Options for customizing the behavior of the returned string.</param>
        public static string GetBindingName(string actionName, int bindingIndex = 0, 
            bool allCompositeBindings = false, NameOptions nameOption = NameOptions.OmitDevice)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetBindingName(inputAction, bindingIndex, allCompositeBindings, nameOption).Item1;
        }

        /// <summary>
        /// Get the sprite of the binding indicated by "inputData". A BindingsImages Scriptable Object needs to be
        /// assigned using NewInput.SetBindingsImages function, or in the bindingsImages field of the InputHandler.
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "inputData", binding that will provide the sprite. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)</param>
        public static Sprite GetBindingSprite(InputData inputData, int bindingIndex = 0)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetBindingSprite(inputAction, bindingIndex);
        }
        
        /// <summary>
        /// Get the sprite of the binding indicated by "actionName". A BindingsImages Scriptable Object needs to be
        /// assigned using NewInput.SetBindingsImages function, or in the bindingsImages field of the InputHandler.
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "actionName", binding that will provide the sprite. <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)</param>
        public static Sprite GetBindingSprite(string actionName, int bindingIndex = 0)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetBindingSprite(inputAction, bindingIndex);
        }

        /// <summary>
        /// Reset all rebinds of the current Input Action Asset, attached to the Player Input Component, to the default value.
        /// </summary>
        public static void ResetRebinds()
        {
            Backend.ResetRebinds();
        }

        /// <summary>
        /// Reset the rebinding of the input action indicated by "inputData".
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "inputData", binding that will be reset. <br /><br />
        /// If "bindingIndex" = 0, the function will reset all rebinds of the input action, indicated by "inputData". <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)</param>
        public static void ResetRebind(InputData inputData, int bindingIndex = 0)
        {
            var inputAction = inputData.InputAction;
            Backend.ResetRebind(inputAction, bindingIndex);
        }
        
        /// <summary>
        /// Reset the rebinding of the input action indicated by "actionName".
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. <br /><br /></param>
        /// <param name="bindingIndex">Index of the input action, indicated by "actionName", binding that will be reset. <br /><br />
        /// If "bindingIndex" = 0, the function will reset all rebinds of the input action, indicated by "actionName". <br /><br />
        /// Can be determined in the Input Action Asset, under the "Actions" column -> (any) Action -> Left DropDown Arrow. (check Documentation -> "6.What are some tips?", to understand how they are numbered)</param>
        public static void ResetRebind(string actionName, int bindingIndex = 0)
        {
            var inputAction = Backend.GetAction(actionName);
            Backend.ResetRebind(inputAction, bindingIndex);
        }
        
        #endregion

        #region Input Data Functions
        
        /// <summary>
        /// Returns true if the user pressed the virtual Button indicated by "inputData" during the current frame.
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference/Button Input Data.</param>
        public static bool GetButtonDown(InputData inputData)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetButtonDown(inputAction);
        }

        /// <summary>
        /// Returns true if the user had pressed and did not release the virtual Button indicated by "inputData" during the current frame.
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference/Button Input Data.</param>
        public static bool GetButton(InputData inputData)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetButton(inputAction);
        }

        /// <summary>
        /// Returns true if the user released the virtual Button indicated by "inputData" during the current frame.
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference/Button Input Data.</param>
        public static bool GetButtonUp(InputData inputData)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetButtonUp(inputAction);
        }
        
        /// <summary>
        /// Returns the value of the virtual Axis indicated by "inputData".
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference/Axis Input Data.</param>
        public static float GetAxis(InputData inputData)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetAxis(inputAction);
        }
        
        /// <summary>
        /// Returns the value of the virtual Vector2 indicated by "inputData".
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference/Vector2 Input Data.</param>
        public static Vector2 GetVector2(InputData inputData)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetVector2(inputAction);
        }

        /// <summary>
        /// Returns the value of the virtual Vector3 indicated by "inputData".
        /// </summary>
        /// <param name="inputData">Can be generated using Generate Input Data Window, or created from Create/New Input By Reference/Vector3 Input Data.</param>
        public static Vector3 GetVector3(InputData inputData)
        {
            var inputAction = inputData.InputAction;
            return Backend.GetVector3(inputAction);
        }
        
        #endregion

        #region Action Name Functions

        /// <summary>
        /// Returns true if the user pressed the virtual Button indicated by "actionName" during the current frame.
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. (Action Type = Button)</param>
        public static bool GetButtonDown(string actionName)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetButtonDown(inputAction);
        }

        /// <summary>
        /// Returns true if the user had pressed and did not release the virtual Button indicated by "actionName" during the current frame.
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. (Action Type = Button)</param>
        public static bool GetButton(string actionName)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetButton(inputAction);
        }
        
        /// <summary>
        /// Returns true if the user released the virtual Button indicated by "actionName" during the current frame.
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. (Action Type = Button)</param>
        public static bool GetButtonUp(string actionName)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetButtonUp(inputAction);
        }
        
        /// <summary>
        /// Returns the value of the virtual Axis indicated by "actionName".
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. (Action Type = Value, Control Type = Axis)</param>
        public static float GetAxis(string actionName)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetAxis(inputAction);
        }

        /// <summary>
        /// Returns the value of the virtual Vector2 indicated by "actionName".
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. (Action Type = Value, Control Type = Vector 2)</param>
        public static Vector2 GetVector2(string actionName)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetVector2(inputAction);
        }
        
        /// <summary>
        /// Returns the value of the virtual Vector3 indicated by "actionName".
        /// </summary>
        /// <param name="actionName">Can be found in the Input Action Asset, under the "Actions" column. (Action Type = Value, Control Type = Vector 3)</param>
        public static Vector3 GetVector3(string actionName)
        {
            var inputAction = Backend.GetAction(actionName);
            return Backend.GetVector3(inputAction);
        }
        
        #endregion
        
    }
}
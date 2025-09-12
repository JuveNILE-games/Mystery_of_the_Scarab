using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Core.Systems.InputManagement{
    public abstract class InputAutoSubscriber : MonoBehaviour 
    {
         public InputReader inputReader;

        protected virtual void OnEnable()
        {
            if (inputReader == null)
            {
                Debug.LogError("InputReader not assigned!", this);
                return;
            }

            // Auto-discover methods with [InputAction] attributes
            foreach (var method in GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                var attributes = method.GetCustomAttributes<InputActionAttribute>(true);
                foreach (var attr in attributes)
                {
                    SubscribeMethod(method, attr);
                }
            }
        }

        private void SubscribeMethod(MethodInfo method, InputActionAttribute attr)
        {
            // Validate method signature
            if (method.GetParameters().Length != 1 || 
                method.GetParameters()[0].ParameterType != typeof(InputAction.CallbackContext))
            {
                Debug.LogError($"Invalid method signature for {method.Name}!", this);
                return;
            }

            // Create delegate
            var handler = (InputReader.InputEvent)Delegate.CreateDelegate(
                typeof(InputReader.InputEvent), 
                this, 
                method
            );

            // Subscribe based on phase
            switch (attr.Phase)
            {
                case InputActionPhase.Started:
                    inputReader.SubscribeStarted(attr.ActionName, handler);
                    break;
                case InputActionPhase.Performed:
                    inputReader.SubscribePerformed(attr.ActionName, handler);
                    break;
                case InputActionPhase.Canceled:
                    inputReader.SubscribeCanceled(attr.ActionName, handler);
                    break;
            }
        }

        protected virtual void OnDisable()
        {
            // Automatically unsubscribe from all subscribed actions from _inputReader
            inputReader.UnsubscribeAll();
        }
    }
}
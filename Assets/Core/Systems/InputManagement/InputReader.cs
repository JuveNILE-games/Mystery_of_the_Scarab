using System.Collections.Generic;
using AYellowpaper.SerializedCollections;
using Core.Utility.Attributes;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Core.Systems.InputManagement{
    [CreateAssetMenu(fileName = "Input Reader", menuName = "Core/Input/Input Reader")]
    public class InputReader : ScriptableObject{
        [SerializeField] private InputActionAsset actions;
        [SerializeField, ReadOnly] private SerializedDictionary<string, InputAction> _actionCache = new();

        // Event delegates
        public delegate void InputEvent(InputAction.CallbackContext context);

        // Phase-specific subscriptions
        private Dictionary<string, InputEvent> _startedSubscriptions = new();
        private readonly Dictionary<string, InputEvent> _performedSubscriptions = new();
        private readonly Dictionary<string, InputEvent> _canceledSubscriptions = new();

        //Getters & Setters
        public InputActionAsset Actions
        {
            get => actions;
            set => actions = value;
        }
        public void Initialize(){
           //Get the object that is calling this method
            actions.Enable();
            CacheActions();
        }

        private void CacheActions(){
            _actionCache.Clear();
            foreach (InputAction action in actions)
            {
                _actionCache[action.name] = action;

                // Wire up phase events to internal handlers
                action.started += (ctx) => TriggerEvent(action.name, ctx, InputActionPhase.Started);
                action.performed += (ctx) => TriggerEvent(action.name, ctx, InputActionPhase.Performed);
                action.canceled += (ctx) => TriggerEvent(action.name, ctx, InputActionPhase.Canceled);
            }
        }

        private InputAction GetAction(string actionName){
            if (_actionCache.TryGetValue(actionName, out InputAction action))
            {
                return action;
            }

            Debug.LogError($"Action '{actionName}' not found in InputActionAsset!");
            return null;
        }

        // === Subscription Methods ===
        public void SubscribeStarted(string actionName, InputEvent callback){
            if (GetAction(actionName) != null)
            {
                if (_startedSubscriptions.TryGetValue(actionName, out var existing))
                    _startedSubscriptions[actionName] = existing + callback;
                else
                    _startedSubscriptions[actionName] = callback;
            }
        }

        public void SubscribePerformed(string actionName, InputEvent callback){
            if (GetAction(actionName) != null)
            {
                if (_performedSubscriptions.TryGetValue(actionName, out var existing))
                    _performedSubscriptions[actionName] = existing + callback;
                else
                    _performedSubscriptions[actionName] = callback;
            }
        }

        public void SubscribeCanceled(string actionName, InputEvent callback){
            if (GetAction(actionName) != null)
            {
                if (_canceledSubscriptions.TryGetValue(actionName, out var existing))
                    _canceledSubscriptions[actionName] = existing + callback;
                else
                    _canceledSubscriptions[actionName] = callback;
            }
        }

        // === Event Triggering ===
        private void TriggerEvent(string actionName, InputAction.CallbackContext ctx, InputActionPhase phase){
            switch (phase)
            {
                case InputActionPhase.Started:
                    if (_startedSubscriptions.TryGetValue(actionName, out var started))
                        started.Invoke(ctx);
                    break;
                case InputActionPhase.Performed:
                    if (_performedSubscriptions.TryGetValue(actionName, out var performed))
                        performed.Invoke(ctx);
                    break;
                case InputActionPhase.Canceled:
                    if (_canceledSubscriptions.TryGetValue(actionName, out var canceled))
                        canceled.Invoke(ctx);
                    break;
            }

        }

        public void UnsubscribeAll(){
            foreach (var action in _actionCache.Values)
            {
                action.started -= ctx => TriggerEvent(action.name, ctx, InputActionPhase.Started);
                action.performed -= ctx => TriggerEvent(action.name, ctx, InputActionPhase.Performed);
                action.canceled -= ctx => TriggerEvent(action.name, ctx, InputActionPhase.Canceled);
            }

            _startedSubscriptions.Clear();
            _performedSubscriptions.Clear();
            _canceledSubscriptions.Clear();
        }
    }
}
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using Game.Events;
using Core.Utility.Attributes;
using Core;
using Core.Utility;
using Core.Systems.AgentNavigation;
using Core.Systems.Services;
using Core.Systems.InputManagement;

namespace Game
{
    /// <summary>
    /// Manages character switching in single player mode.
    /// Toggles input control and AI behavior for registered characters.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public class ControlSwitcher : MonoBehaviour, IControlSwitcher
    {
        [SerializeField] private ScriptableEventControlChanged onControlChanged;

        [Inject] private IControllableRegistry _registry;
        [Inject] private IGameStateManager _gameState;
        [Inject] private InputReader _inputReader;
        [Inject] private INavMeshSurfaceService _navMeshService;

        private int _currentIndex = 0;

        private void Start()
        {
            if (_gameState == null || _gameState.CurrentState != GameState.SinglePlayer)
            {
                enabled = false;
                return;
            }

            // Subscribe here, after [Inject] has populated _inputReader.
            if (_inputReader != null)
            {
                _inputReader.SubscribeStarted("SwitchCharacter", OnSwitchCharacter);
            }
            else
            {
                Debug.LogError("[ControlSwitcher] _inputReader is null after injection — SwitchCharacter will not work!", this);
            }

            // Self-register as IControlSwitcher
            ServiceLocator.Global.Register<IControlSwitcher>(this);
            
            InitializeSwitching();
        }

        // NOTE: Subscription happens in Start(), not OnEnable(), because [Inject] fields
        // (_inputReader, _gameState, etc.) are populated by the service locator during the
        // Start phase. OnEnable fires before injection is complete, so _inputReader is null
        // at that point and the SubscribeStarted call would silently do nothing.
        private void OnDisable()
        {
            if (_inputReader != null)
            {
                _inputReader.UnsubscribeStarted("SwitchCharacter", OnSwitchCharacter);
            }
        }

        private void OnSwitchCharacter(InputAction.CallbackContext context)
        {
            var all = _registry.GetAll();
            if (all.Count > 1)
            {
                int nextIndex = (_currentIndex + 1) % all.Count;
                SwitchTo(nextIndex);
            }
        }

        private void InitializeSwitching()
        {
            var all = _registry.GetAll();
            if (all.Count == 0) return;
            
            for (int i = 0; i < all.Count; i++)
            {
                ApplyState(all[i], i == _currentIndex);
            }
            
            // Ensure reactive systems (Camera, AI) know who to follow from the start
            if (SceneCamera.Instance != null)
            {
                SceneCamera.Instance.TrackingTarget.Value = all[_currentIndex].GetTransform();
            }

            // Initialize the dynamic NavMesh surface around the AI companion (inactive player)
            if (_navMeshService != null)
            {
                for (int j = 0; j < all.Count; j++)
                {
                    if (j != _currentIndex)
                    {
                        _navMeshService.InitializeSurface(all[j].GetTransform());
                        break;
                    }
                }
            }
        }

        public void SwitchTo(int newIndex)
        {
            var all = _registry.GetAll();
            if (newIndex < 0 || newIndex >= all.Count || newIndex == _currentIndex) return;

            // 1. Publish the new target to the global reactive system (Camera and AI will react automatically)
            if (SceneCamera.Instance != null)
            {
                SceneCamera.Instance.TrackingTarget.Value = all[newIndex].GetTransform();
            }

            // 2. Re-anchor the NavMesh surface on the AI companion (the previously active player)
            if (_navMeshService != null)
            {
                _navMeshService.InitializeSurface(all[_currentIndex].GetTransform());
            }

            // 3. Then apply control states
            ApplyState(all[_currentIndex], false);
            ApplyState(all[newIndex], true);
            
            _currentIndex = newIndex;
            BroadcastControlChanged();
        }

        private void ApplyState(IControllable c, bool isControlled)
        {
            if (isControlled) c.OnControlGained();
            else c.OnControlLost();
        }

        private void BroadcastControlChanged()
        {
            var all = _registry.GetAll();
            if (all.Count == 0) return;
            
            var tf = all[_currentIndex].GetTransform();
            
            if (onControlChanged != null)
            {
                onControlChanged.Raise(new ControlChanged { newIndex = _currentIndex, newTransform = tf });
            }
        }
    }
}

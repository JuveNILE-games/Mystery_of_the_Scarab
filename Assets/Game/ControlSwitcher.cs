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
using NetCore.Interfaces;

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
        [Inject] private ISessionService _session;
        [Inject] private InputReader _inputReader;
        [Inject] private INavMeshSurfaceService _navMeshService;

        // Tracks the actual controlled object, not a list index — an unregister before the
        // tracked slot would otherwise silently hand control to whoever occupies it next.
        private IControllable _controlled;
        private System.IDisposable _controllablesSubscription;

        private void Start()
        {
            if (_session == null || _session.Mode.Value != SessionMode.Solo)
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

            // Reactive rather than a one-shot check: controllables can register after this Start()
            // runs, and ReconcileControlState is idempotent so re-running it as membership grows
            // always converges correctly.
            _controllablesSubscription = _registry.Controllables.Bind(ReconcileControlState);
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

            _controllablesSubscription?.Dispose();
            _controllablesSubscription = null;
        }

        private void OnSwitchCharacter(InputAction.CallbackContext context)
        {
            var all = _registry.GetAll();
            if (all.Count > 1)
            {
                int currentIndex = _controlled != null ? IndexOf(all, _controlled) : -1;
                int nextIndex = (currentIndex + 1) % all.Count;
                SwitchTo(nextIndex);
            }
        }

        private void ReconcileControlState(IReadOnlyList<IControllable> all)
        {
            if (all.Count == 0)
            {
                _controlled = null;
                return;
            }

            if (_controlled == null || IndexOf(all, _controlled) < 0)
            {
                _controlled = all[0];
            }

            for (int i = 0; i < all.Count; i++)
            {
                ApplyState(all[i], all[i] == _controlled);
            }

            // Ensure reactive systems (Camera, AI) know who to follow
            if (SceneCamera.Instance != null)
            {
                SceneCamera.Instance.TrackingTarget.Value = _controlled.GetTransform();
            }

            // Initialize the dynamic NavMesh surface around the AI companion (inactive player)
            if (_navMeshService != null)
            {
                for (int j = 0; j < all.Count; j++)
                {
                    if (all[j] != _controlled)
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
            if (newIndex < 0 || newIndex >= all.Count) return;

            var target = all[newIndex];
            if (target == _controlled) return;

            // 1. Publish the new target to the global reactive system (Camera and AI will react automatically)
            if (SceneCamera.Instance != null)
            {
                SceneCamera.Instance.TrackingTarget.Value = target.GetTransform();
            }

            // 2. Re-anchor the NavMesh surface on the AI companion (the previously active player)
            if (_navMeshService != null && _controlled != null)
            {
                _navMeshService.InitializeSurface(_controlled.GetTransform());
            }

            // 3. Then apply control states
            if (_controlled != null) ApplyState(_controlled, false);
            ApplyState(target, true);

            _controlled = target;
            BroadcastControlChanged();
        }

        private void ApplyState(IControllable c, bool isControlled)
        {
            if (isControlled) c.OnControlGained();
            else c.OnControlLost();
        }

        private void BroadcastControlChanged()
        {
            if (_controlled == null) return;

            if (onControlChanged != null)
            {
                int index = IndexOf(_registry.GetAll(), _controlled);
                onControlChanged.Raise(new ControlChanged { newIndex = index, newTransform = _controlled.GetTransform() });
            }
        }

        private static int IndexOf(IReadOnlyList<IControllable> list, IControllable item)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == item) return i;
            }
            return -1;
        }
    }
}

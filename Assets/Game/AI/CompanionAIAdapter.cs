using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;
using Core.Utility.Attributes;
using Core.Systems.AgentNavigation;
using Core.Utility;
using System;

namespace Game.AI
{
    /// <summary>
    /// Adapter that allows the core game systems to control a companion character
    /// that is driven by the Unity Behavior graph system.
    /// Implements the standard IAIController interface.
    /// </summary>
    [RequireComponent(typeof(BehaviorGraphAgent))]
    public class CompanionAIAdapter : MonoBehaviour, IAIController
    {
        [Header("Components")]
        [SerializeField] private NavMeshAgent _navAgent;
        [SerializeField] private BehaviorGraphAgent _behaviorAgent;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerAbilities _abilities;
        [SerializeField] private AIMovementBridge _movementBridge;

        /// <summary>
        /// Optional — only registered in SinglePlayer mode.
        /// When present, the adapter waits for the NavMesh to be ready before enabling movement.
        /// </summary>
        [Inject] private INavMeshSurfaceService _navMeshService;

        /// <summary>Whether ControlSwitcher has requested AI to be enabled.</summary>
        private bool _aiRequested;
        private IDisposable _targetSubscription;

        private void Awake()
        {
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_behaviorAgent == null) _behaviorAgent = GetComponent<BehaviorGraphAgent>();
            if (_interactor == null) _interactor = GetComponent<PlayerInteractor>();
            if (_abilities == null) _abilities = GetComponent<PlayerAbilities>();
            if (_movementBridge == null) _movementBridge = GetComponent<AIMovementBridge>();
        }

        private void OnEnable()
        {
            // Subscribe to the global tracking target reactively
            if (SceneCamera.Instance != null)
            {
                _targetSubscription = SceneCamera.Instance.TrackingTarget.Bind(OnTrackingTargetChanged);
            }
        }

        private void OnDisable()
        {
            _targetSubscription?.Dispose();
        }

        private void Start()
        {
            if (_navMeshService != null)
            {
                if (_navMeshService.IsReady) OnNavMeshReady();
                else _navMeshService.OnNavMeshReady += OnNavMeshReady;

                _navMeshService.OnNavMeshDestroyed += OnNavMeshLost;
            }
        }

        private void OnTrackingTargetChanged(Transform newTarget)
        {
            if (_behaviorAgent != null)
            {
                _behaviorAgent.SetVariableValue("Player", newTarget);
            }
        }

        public void EnableAI(bool enabled)
        {
            _aiRequested = enabled;
            
            if (!enabled)
            {
                // Disable AI
                if (_behaviorAgent != null) _behaviorAgent.enabled = false;
                
                if (_navAgent != null)
                {
                    if (_navAgent.enabled && _navAgent.isOnNavMesh) _navAgent.isStopped = true;
                    _navAgent.enabled = false;
                    _navAgent.updatePosition = true; // Restore defaults
                    _navAgent.updateRotation = true;
                }
                
                if (_movementBridge != null) _movementBridge.SetAiControlled(false);
                return;
            }

            // Enable AI
            if (_movementBridge != null) _movementBridge.SetAiControlled(true);

            if (_navMeshService != null)
            {
                // Wait for NavMesh service
                if (_behaviorAgent != null) _behaviorAgent.enabled = false;
                if (_navAgent != null) _navAgent.enabled = false;

                if (_navMeshService.IsReady) OnNavMeshReady();
                else _navMeshService.OnNavMeshReady += OnNavMeshReady;
            }
            else
            {
                // Fallback (e.g. static NavMesh)
                SnapAndEnable();
            }
        }

        private void OnNavMeshReady()
        {
            if (_navMeshService != null) _navMeshService.OnNavMeshReady -= OnNavMeshReady;

            if (_aiRequested)
            {
                SnapAndEnable();
            }
        }

        private void SnapAndEnable()
        {
            if (_navAgent == null) return;

            // Snap agent to the NavMesh
            if (NavMesh.SamplePosition(transform.position, out var hit, 3.0f, NavMesh.AllAreas))
            {
                _navAgent.enabled = true;
                _navAgent.Warp(hit.position);
                _navAgent.isStopped = false;
                
                // Once agent is snapped, handle blackboard and enable behavior
                SyncLocalBlackboard();
                if (_behaviorAgent != null) _behaviorAgent.enabled = true;
                
                Debug.Log($"[CompanionAIAdapter] NavMesh ready — Agent snapped and enabled on '{gameObject.name}' at {hit.position}.", this);
            }
            else
            {
                // Fallback: Enable anyway if surface is ready
                _navAgent.enabled = true;
                SyncLocalBlackboard();
                if (_behaviorAgent != null) _behaviorAgent.enabled = true;
                
                Debug.LogWarning($"[CompanionAIAdapter] NavMesh built, but could not find a valid point near {gameObject.name}. Attempting raw enable.", this);
            }
        }

        private void SyncLocalBlackboard()
        {
            if (_behaviorAgent == null) return;

            // Ensure the behavior script is restarted to pick up new values
            _behaviorAgent.End();

            // Push current components into blackboard
            SetVariableSafe("Self", gameObject);
            SetVariableSafe("Agent", _navAgent);
            SetVariableSafe("Interactor", _interactor);
            SetVariableSafe("Abilities", _abilities);
            
            // Note: The "Player" variable is now handled reactively by OnTrackingTargetChanged
            if (SceneCamera.Instance != null && SceneCamera.Instance.TrackingTarget.Value != null)
            {
                SetVariableSafe("Player", SceneCamera.Instance.TrackingTarget.Value);
            }
            
            _behaviorAgent.Start();
            
            Debug.Log($"[CompanionAIAdapter] Blackboard synced for '{gameObject.name}'.", this);
        }

        private void SetVariableSafe(string varName, object value)
        {
            if (value == null) return;
            _behaviorAgent.SetVariableValue(varName, value);
        }

        public void UpdateBlackboardPlayer(Transform playerTransform)
        {
            // Deprecated: Now using reactive TrackingTarget from SceneCamera
        }

        private void OnNavMeshLost()
        {
            if (_navAgent != null && _navAgent.enabled)
            {
                if (_navAgent.isOnNavMesh) _navAgent.isStopped = true;
                _navAgent.enabled = false;
            }

            if (_navMeshService != null) _navMeshService.OnNavMeshReady += OnNavMeshReady;
        }

        private void OnDestroy()
        {
            if (_navMeshService != null)
            {
                _navMeshService.OnNavMeshReady -= OnNavMeshReady;
                _navMeshService.OnNavMeshDestroyed -= OnNavMeshLost;
            }
            _targetSubscription?.Dispose();
        }
    }
}

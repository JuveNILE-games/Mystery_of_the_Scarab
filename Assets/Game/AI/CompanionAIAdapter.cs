using UnityEngine;
using UnityEngine.AI;
using Unity.Behavior;
using Core.Utility.Attributes;
using Core.Systems.AgentNavigation;
using Core.Utility;
using System;
using Core;
using Core.Systems.Logging;
using Game.Systems.LevelSystem.Runtime;

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
        [SerializeField] private CompanionPuzzleObserver _puzzleObserver;

        [Inject] private INavMeshSurfaceService _navMeshService;
        [Inject] private ILoggerService _logger;
        [Inject] private IGameStateManager _gameState;

        private bool _aiRequested;
        private bool _isFirstEnable = true;
        private IDisposable _targetSubscription;

        private void Awake()
        {
            if (_navAgent == null) _navAgent = GetComponent<NavMeshAgent>();
            if (_behaviorAgent == null) _behaviorAgent = GetComponent<BehaviorGraphAgent>();
            if (_interactor == null) _interactor = GetComponent<PlayerInteractor>();
            if (_abilities == null) _abilities = GetComponent<PlayerAbilities>();
            if (_movementBridge == null) _movementBridge = GetComponent<AIMovementBridge>();
            if (_puzzleObserver == null) _puzzleObserver = GetComponent<CompanionPuzzleObserver>();
        }

        private void OnEnable()
        {
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
            if (_gameState != null && _gameState.CurrentState != GameState.SinglePlayer)
            {
                DisableAI();
                enabled = false;
                return;
            }

            SubscribeToNavMeshReady();
            if (_navMeshService != null)
            {
                _navMeshService.OnNavMeshDestroyed += OnNavMeshLost;
            }

            // Subscribe to room changes
            var levelController = FindFirstObjectByType<LevelController>();
            if (levelController != null)
            {
                levelController.OnRoomChanged += OnRoomChanged;
            }
        }

        private void OnTrackingTargetChanged(Transform newTarget)
        {
            if (_behaviorAgent != null)
            {
                _behaviorAgent.SetVariableValue("Player", newTarget);
            }
        }

        public void EnableAI(bool enable)
        {
            _aiRequested = enable;
            if (enable)
            {
                if (!_isFirstEnable)
                {
                    OnControlLost();
                }
                _isFirstEnable = false;
                SubscribeToNavMeshReady();
            }
            else
            {
                _isFirstEnable = false; // Next enable should trigger deference if switched from player
                DisableAI();
            }
        }

        public void OnControlLost()
        {
            // Called when AI resumes control
            _puzzleObserver?.OnPlayerReleasedControl();
        }

        private void OnRoomChanged(RoomController from, RoomController to)
        {
            if (to is PuzzleRoomController puzzleRoom)
            {
                _puzzleObserver?.OnRoomEntered(puzzleRoom);
            }
        }

        private void DisableAI()
        {
            if (_behaviorAgent != null) _behaviorAgent.enabled = false;
            
            if (_navAgent != null)
            {
                if (_navAgent.enabled && _navAgent.isOnNavMesh) _navAgent.isStopped = true;
                _navAgent.enabled = false;
                _navAgent.updatePosition = true;
                _navAgent.updateRotation = true;
            }
            
            if (_movementBridge != null) _movementBridge.SetAiControlled(false);
        }

        private void SubscribeToNavMeshReady()
        {
            if (_navMeshService == null)
            {
                if (_aiRequested) SnapAndEnable();
                return;
            }
            _navMeshService.OnNavMeshReady -= OnNavMeshReady;
            _navMeshService.OnNavMeshReady += OnNavMeshReady;
            
            if (_navMeshService.IsReady) OnNavMeshReady();
        }

        private void OnNavMeshReady()
        {
            if (_navMeshService != null) _navMeshService.OnNavMeshReady -= OnNavMeshReady;
            if (_aiRequested) SnapAndEnable();
        }

        private void SnapAndEnable()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _logger?.Log(this, $"[Companion] Snapping and enabling AI. Target: {_aiRequested}");
#endif
            if (!_aiRequested) return;
            if (_navAgent == null) return;

            // IMPORTANT: Disable automatic position/rotation sync before enabling the agent.
            // The companion uses a CharacterController driven by PlayerStateMachine for actual
            // movement. If updatePosition is left true (the default), the NavMeshAgent AND
            // CharacterController.Move() both write to transform.position every frame, causing
            // a tug-of-war that manifests as stuttering/drag. We use the "Agent as Advisor"
            // pattern instead: the agent calculates desiredVelocity, AIMovementBridge reads it
            // and feeds it to the state machine, and we sync nextPosition back each frame so
            // the agent's internal pathfinding ghost stays aligned with the real position.
            _navAgent.updatePosition = false;
            _navAgent.updateRotation = false;

            if (NavMesh.SamplePosition(transform.position, out var hit, 3.0f, NavMesh.AllAreas))
            {
                _navAgent.enabled = true;
                _navAgent.Warp(hit.position);
                _navAgent.isStopped = false;

                SyncLocalBlackboard();
                if (_behaviorAgent != null) _behaviorAgent.enabled = true;
                if (_movementBridge != null) _movementBridge.SetAiControlled(true);
            }
            else
            {
                // No valid NavMesh surface found within 3m. Enabling the agent here would put it
                // off-mesh, causing FollowPlayerAction to immediately return PathInvalid and the
                // companion to stand frozen with no error. Wait for the next OnNavMeshReady event
                // instead, which fires once DynamicNavMeshSurfaceService finishes a bake.
                Debug.LogWarning("[CompanionAIAdapter] SnapAndEnable: no NavMesh found within 3m of " +
                                 $"{transform.position}. Waiting for next NavMesh ready event.", this);
                SubscribeToNavMeshReady();
            }
        }

        private void Update()
        {
            // Keep the NavMeshAgent's internal simulation ghost aligned with where the
            // CharacterController actually moved us. Without this, the agent's pathfinding
            // drifts away from the real position and it starts re-routing incorrectly.
            if (_navAgent != null && _navAgent.enabled && _navAgent.isOnNavMesh)
            {
                _navAgent.nextPosition = transform.position;
            }
        }

        private void SyncLocalBlackboard()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _logger?.Log(this, "[Companion] Syncing Blackboard...");
#endif
            if (_behaviorAgent == null) return;

            _behaviorAgent.End();

            SetVariableSafe("Self", gameObject);
            SetVariableSafe("Agent", _navAgent);
            SetVariableSafe("Interactor", _interactor);
            SetVariableSafe("Abilities", _abilities);
            
            if (SceneCamera.Instance != null && SceneCamera.Instance.TrackingTarget.Value != null)
            {
                SetVariableSafe("Player", SceneCamera.Instance.TrackingTarget.Value);
            }
            
            _behaviorAgent.Start();
        }

        private void SetVariableSafe<T>(string varName, T value)
        {
            if (value == null) return;
            _behaviorAgent.SetVariableValue(varName, value);
        }

        private void OnNavMeshLost()
        {
            if (_behaviorAgent != null) _behaviorAgent.enabled = false;
            if (_movementBridge != null) _movementBridge.SetAiControlled(false);

            if (_navAgent != null && _navAgent.enabled)
            {
                if (_navAgent.isOnNavMesh) _navAgent.isStopped = true;
                _navAgent.enabled = false;
            }

            SubscribeToNavMeshReady();
        }

        private void OnDestroy()
        {
            if (_navMeshService != null)
            {
                _navMeshService.OnNavMeshReady -= OnNavMeshReady;
                _navMeshService.OnNavMeshDestroyed -= OnNavMeshLost;
            }
            
            var levelController = FindFirstObjectByType<LevelController>();
            if (levelController != null)
            {
                levelController.OnRoomChanged -= OnRoomChanged;
            }
        }
    }
}

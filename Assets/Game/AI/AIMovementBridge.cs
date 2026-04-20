using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Reads the NavMeshAgent's desired velocity each frame and feeds it to the
    /// IMovementControllable (PlayerStateMachine) as a world-space direction via
    /// OnMoveWorldSpace(). No camera-space conversion is performed here — the NavMesh
    /// already operates in world space, and the state machine accepts world-space input.
    ///
    /// Hysteresis thresholds prevent rapid idle/walk flickering at low speeds.
    /// </summary>
    public class AIMovementBridge : MonoBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _autoFindReferences = true;
        [SerializeField] private float _moveThreshold = 0.15f;  // fraction of max speed to start moving
        [SerializeField] private float _stopThreshold = 0.05f;  // fraction of max speed to stop

        [Header("State")]
        [SerializeField] private bool _isAiControlled = true;

        [Header("Optional References")]
        [SerializeField] private NavMeshAgent _agent;
        [SerializeField] private MonoBehaviour _movementTarget;

        private IMovementControllable _controllable;
        private bool _wasMovingLastFrame;

        private void Awake()
        {
            if (_autoFindReferences)
            {
                if (_agent == null) _agent = GetComponent<NavMeshAgent>();
                if (_movementTarget == null) _movementTarget = GetComponent<IMovementControllable>() as MonoBehaviour;
            }

            _controllable = _movementTarget as IMovementControllable;
        }

        private void Update()
        {
            if (_isAiControlled) UpdateAiMovement();
        }

        public void SetAiControlled(bool isAiControlled)
        {
            _isAiControlled = isAiControlled;
            if (!isAiControlled)
            {
                _wasMovingLastFrame = false;
                // Zero out movement so the state machine transitions back to Idle cleanly.
                _controllable?.OnMoveWorldSpace(Vector3.zero);
            }
        }

        private void UpdateAiMovement()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh || _controllable == null)
                return;

            Vector3 desiredVelocity = _agent.desiredVelocity;
            float rawMagnitude = desiredVelocity.magnitude / _agent.speed;

            // Hysteresis: require a higher threshold to START moving than to KEEP moving.
            // Prevents rapid idle/walk oscillation when the agent decelerates near its target.
            bool shouldMove = _wasMovingLastFrame
                ? rawMagnitude > _stopThreshold
                : rawMagnitude > _moveThreshold;

            if (shouldMove)
            {
                // desiredVelocity is already world-space from the NavMesh simulation.
                // Zero Y so vertical NavMesh surface offsets don't affect horizontal locomotion.
                // Normalize so the state machine always receives a unit direction and moves at
                // its configured WalkSpeed — the NavMesh speed is advisory, not absolute here.
                Vector3 worldDir = new Vector3(desiredVelocity.x, 0f, desiredVelocity.z);
                _controllable.OnMoveWorldSpace(worldDir.sqrMagnitude > 0.01f ? worldDir.normalized : Vector3.zero);
                _wasMovingLastFrame = true;
            }
            else
            {
                _controllable.OnMoveWorldSpace(Vector3.zero);
                _wasMovingLastFrame = false;
            }
        }
    }
}

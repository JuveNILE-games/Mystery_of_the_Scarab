using UnityEngine;
using UnityEngine.AI;

namespace Game.AI
{
    /// <summary>
    /// Bridges a NavMeshAgent's navigation output into a StateMachine's movement input.
    /// This allows the AI to "drive" the same movement system used by players.
    /// </summary>
    public class AIMovementBridge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private bool autoFindReferences = true;

        private IMovementControllable _movementTarget;
        private bool _isAiControlled;

        private void Awake()
        {
            if (autoFindReferences)
            {
                if (agent == null) agent = GetComponent<NavMeshAgent>();
                _movementTarget = GetComponent<IMovementControllable>();
            }
        }

        /// <summary>
        /// Enables or disables the AI movement bridge.
        /// </summary>
        public void SetAiControlled(bool enabled)
        {
            _isAiControlled = enabled;
            
            if (agent != null)
            {
                // When AI is controlled, we let the StateMachine handle physical movement.
                // We only use the agent for pathfinding logic.
                agent.updatePosition = !enabled;
                agent.updateRotation = !enabled;
                
                if (enabled)
                {
                    // Snap the agent to the current position to start clean
                    agent.nextPosition = transform.position;
                }
            }

            if (!enabled && _movementTarget != null)
            {
                // Clear input when AI stops
                _movementTarget.OnMove(Vector2.zero);
            }
        }

        private void Update()
        {
            if (!_isAiControlled || agent == null || _movementTarget == null) return;

            // 1. Keep the NavMeshAgent's internal position in sync with the CharacterController/Body
            agent.nextPosition = transform.position;

            // 2. Get the direction the AI WANTS to move (World Space)
            Vector3 desiredVelocity = agent.desiredVelocity;
            
            if (desiredVelocity.sqrMagnitude > 0.01f)
            {
                // 3. Coordinate Space Bridge:
                // The PlayerStateMachine (and its states) use GetMoveDirection() which is CAMERA-RELATIVE.
                // We need to translate our desired World-Space direction into an Input Vector [x, y]
                // that, when transformed by the Camera, results in the desired World-Space direction.
                
                Vector3 desiredDirection = desiredVelocity.normalized;
                
                // Project the desired direction into the Camera's forward/right planes
                // Standard Player transition: moveDir = (Forward * InputY + Right * InputX)
                // To solve for InputX/Y, we dot product with the Camera axes.
                Vector3 camForward = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                Vector3 camRight = Camera.main != null ? Camera.main.transform.right : transform.right;
                camForward.y = 0;
                camRight.y = 0;
                camForward.Normalize();
                camRight.Normalize();

                float inputX = Vector3.Dot(desiredDirection, camRight);
                float inputY = Vector3.Dot(desiredDirection, camForward);
                
                Vector2 virtualInput = new Vector2(inputX, inputY);
                
                // Clamp/Normalize for extreme angles
                if (virtualInput.sqrMagnitude > 1f) virtualInput.Normalize();

                // 4. Feed the state machine
                _movementTarget.OnMove(virtualInput);
                
                /* // Debug diagnostics
                if (Time.frameCount % 60 == 0) // Log once per second approx
                {
                    Debug.Log($"[AIMovementBridge] AI Input: {virtualInput}, State: {(_movementTarget as MonoBehaviour)?.name}");
                }
                */
            }
            else
            {
                // No movement needed
                _movementTarget.OnMove(Vector2.zero);
            }
        }
    }
}

using UnityEngine;

namespace NewInputByReference.Examples
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed;
        [SerializeField] private float jumpHeight = 3f;
        [SerializeField] private float gravity = -9.81f;
        
        [SerializeField] private Transform groundCheck;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float groundDistance = 0.4f;

        private CharacterController _controller;
        
        private Transform _transform;
        private Vector3 _velocity;
        private bool _isGrounded;
        private bool _wasGrounded;

        // Event for when player lands
        public System.Action OnLanded;

        // Property to check if player is grounded
        public bool IsGrounded => _isGrounded;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _transform = transform;
        }

        private void Update()
        {
            // Store previous grounded state
            _wasGrounded = _isGrounded;
            
            // Check if player is grounded
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            
            // Trigger landed event if player just landed
            if (!_wasGrounded && _isGrounded)
            {
                OnLanded?.Invoke();
            }
            
            // Reset vertical velocity when grounded
            if (_isGrounded && _velocity.y < 0)
                _velocity.y = -2f;
 
            float x = NewInput.GetAxis("Horizontal");
            float z = NewInput.GetAxis("Vertical");
            
            if(NewInput.GetButtonDown("Jump") && _isGrounded)
                _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            
            _velocity.y += gravity * Time.deltaTime;

            var move = _transform.right * x + _transform.forward * z;
            move = Vector3.ClampMagnitude(move, 1);
            
            _controller.Move((_velocity * Time.deltaTime) + (move * moveSpeed * Time.deltaTime));
        }

        // Method to add vertical velocity (for double jump)
        public void AddVerticalVelocity(float force)
        {
            _velocity.y += force;
        }
    }
}

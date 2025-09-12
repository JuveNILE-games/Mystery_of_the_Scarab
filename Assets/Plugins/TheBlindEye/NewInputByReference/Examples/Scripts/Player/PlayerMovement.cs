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

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _transform = transform;
        }

        private void Update()
        {
            _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
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
    }
}

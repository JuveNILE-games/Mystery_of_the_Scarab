using UnityEngine;

namespace NewInputByReference.Examples
{
    [RequireComponent(typeof(Camera))]
    public class MouseLook : MonoBehaviour
    {
        [SerializeField] private float mouseSensitivity = 100f;
        [SerializeField] private Transform playerBody;
        
        private float _xRotation; 

        private void Start ()  => Cursor.lockState = CursorLockMode.Locked;

        private void OnEnable() => UIPanelsManager.OnMenuUpdate += UpdateCursor;
        private void OnDisable() => UIPanelsManager.OnMenuUpdate -= UpdateCursor;
        
        private void Update () 
        {
            float mouseX = NewInput.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
            float mouseY = NewInput.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;
                
            _xRotation -= mouseY;
            _xRotation = Mathf.Clamp(_xRotation, -90f, 90f);

            transform.localRotation = Quaternion.Euler(_xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
        
        private void UpdateCursor(bool isOpen)
        {
            Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        }
    }
}

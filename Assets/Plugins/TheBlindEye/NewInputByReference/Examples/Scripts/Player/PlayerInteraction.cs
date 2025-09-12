using UnityEngine;

namespace NewInputByReference.Examples
{
    public class PlayerInteraction : MonoBehaviour
    {
        [SerializeField] private ButtonInputData inputData;
        
        [SerializeField] private float rayDistance = 5f;
        [SerializeField] private float rayRadius = 0.3f;
        [SerializeField] private LayerMask interactableLayer;
        
        private Camera _camera;

        private void Awake() => _camera = Camera.main;

        private void Update()
        {
            var cameraTransform = _camera.transform;
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);

            bool hitSomething = Physics.SphereCast(ray, rayRadius, out var hit, rayDistance, interactableLayer);

            if (!hitSomething)
                return;
           
            if(inputData.ButtonPressed) // or NewInput.GetButtonDown(inputData)
                Destroy(hit.transform.gameObject);
        }
    }
}
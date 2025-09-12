using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace Core.Systems.Navigation{
    [RequireComponent(typeof(CanvasGroup), typeof(UIDocument))]
    public abstract class ScreenComponent : MonoBehaviour{
        public ScreenDefinition definition;
        public UIDocument document;
        private CanvasGroup _canvasGroup;
        public CanvasGroup canvasGroup => _canvasGroup;
        
        [SerializeField] private GameObject firstSelectedObject;

        private void OnEnable(){
            _canvasGroup = GetComponent<CanvasGroup>(); 
            definition.document = document;
            definition.onOpen.AddListener(OnOpen);
            definition.onClose.AddListener(OnClose);

            if (firstSelectedObject)
            {
                ScreenNavigator.Instance.eventSystem.firstSelectedGameObject = firstSelectedObject;
            }
        }

        public virtual void OnOpen(){
        }

        public virtual void OnClose(){
        }
    }
}
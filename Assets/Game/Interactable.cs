using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Core;
using Core.Systems.Interaction;

namespace Game
{
    [System.Serializable]
    public class PlayerInteractEvent : UnityEvent<PlayerInteractor> { }

    [RequireComponent(typeof(Collider))]
    public class Interactable : MonoBehaviour, IInteractable
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        [Header("Interaction Settings")]
        public string interactionPrompt = "Press interact";
        [SerializeField] private bool isInteractableByCompanion = false;
        public PlayerInteractEvent OnInteract;

        private void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        private void OnDisable() { All.Remove(this); }

        void Reset()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            var interactor = other.GetComponent<PlayerInteractor>();
            if (interactor != null)
            {
                interactor.RegisterInteractable(this);
                SetFocus(true);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            var interactor = other.GetComponent<PlayerInteractor>();
            if (interactor != null)
            {
                interactor.UnregisterInteractable(this);
                SetFocus(false);
            }
        }

        /// <summary>
        /// Legacy interact call used by PlayerInteractor and CompanionAI.
        /// </summary>
        public void Interact(PlayerInteractor interactor)
        {
            OnInteract?.Invoke(interactor);
        }

        /// <summary>
        /// IInteractable.Interact implementation — bridges the Core interaction system
        /// with the Game-layer PlayerInteractor pattern.
        /// </summary>
        public bool Interact(GameObject interactor)
        {
            // If the interactor is an AI companion, check the companion gate first.
            if (interactor != null && interactor.GetComponent<IAIController>() != null)
            {
                if (!isInteractableByCompanion) return false;
            }

            var playerInteractor = interactor.GetComponent<PlayerInteractor>();
            if (playerInteractor != null)
            {
                OnInteract?.Invoke(playerInteractor);
                return true;
            }
            // Fallback: invoke with null if no PlayerInteractor found
            OnInteract?.Invoke(null);
            return true;
        }
        
        public void SetFocus(bool state)
        {
            // highlight
        }

        #region IInteractable Implementation

        public string InteractionPrompt => interactionPrompt;
        public bool IsInteractable => enabled && gameObject.activeInHierarchy;
        public bool IsInteractableByCompanion => isInteractableByCompanion;

        #endregion
    }
}

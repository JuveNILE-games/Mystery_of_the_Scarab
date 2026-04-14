using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;
using Core.Systems.Interaction;

[System.Serializable]
public class PlayerInteractEvent : UnityEvent<PlayerInteractor> { }

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour, IInteractable
{
    public static readonly List<Interactable> All = new List<Interactable>();

    [Header("Interaction Settings")]
    public string interactionPrompt = "Press interact";
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

    string IInteractable.InteractionPrompt => interactionPrompt;
    bool IInteractable.IsInteractable => enabled && gameObject.activeInHierarchy;

    #endregion
}

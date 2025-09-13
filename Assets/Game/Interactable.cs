using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class PlayerInteractEvent : UnityEvent<PlayerInteractor> { }

[RequireComponent(typeof(Collider))]
public class Interactable : MonoBehaviour
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

    public void Interact(PlayerInteractor interactor)
    {
        OnInteract?.Invoke(interactor);
    }

    public void SetFocus(bool state)
    {
        // highlight
    }
}

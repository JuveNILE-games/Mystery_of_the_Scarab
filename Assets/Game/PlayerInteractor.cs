using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(Collider))]
public class PlayerInteractor : MonoBehaviour
{
    private List<Interactable> nearbyInteractables = new List<Interactable>();
    private bool acceptInput = true;
    public bool IsControlled => acceptInput;

    // Logic moved to explicit methods called by Input System
    // void Update() removed to prevent double input handling

    public void RegisterInteractable(Interactable interactable)
    {
        if (!nearbyInteractables.Contains(interactable)) nearbyInteractables.Add(interactable);
    }

    public void UnregisterInteractable(Interactable interactable)
    {
        if (nearbyInteractables.Contains(interactable)) nearbyInteractables.Remove(interactable);
    }

    public List<Interactable> GetNearbyInteractables() => new List<Interactable>(nearbyInteractables);

    public void SetControlled(bool controlled)
    {
        acceptInput = controlled;
        if (!controlled)
        {
            foreach (var it in nearbyInteractables) it.SetFocus(false);
            nearbyInteractables.Clear();
            ClearTargetOnLoseControl();
        }
        else OnGainedControl();
    }

    // Method to try local interaction - attempts to interact with the closest nearby interactable
    public bool TryLocalInteract()
    {
        if (!acceptInput || nearbyInteractables.Count == 0)
            return false;

        // Find the closest interactable
        Interactable target = nearbyInteractables.OrderBy(i => Vector3.Distance(transform.position, i.transform.position)).First();
        if (target != null)
        {
            target.Interact(this);
            return true;
        }
        return false;
    }

    void ClearTargetOnLoseControl()
    {
        // optional visual clear
    }

    void OnGainedControl()
    {
        // optional
    }
}

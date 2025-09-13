using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(PlayerInteractor))]
public class PlayerAbilities : MonoBehaviour
{
    public List<AbilityBehaviour> abilities = new List<AbilityBehaviour>();
    PlayerInteractor interactor;

    void Awake() { interactor = GetComponent<PlayerInteractor>(); }

    public void OnControlGained() { /* update UI */ }
    public void OnControlLost() { /* maybe disable passive abilities */ }

    void Update()
    {
        // Example: primary ability button
        if (!interactor.IsControlled) return;
        if (Input.GetButtonDown("Fire1"))
        {
            var a = GetContextualAbility();
            if (a != null) a.TryUse();
        }
    }

    public AbilityBehaviour GetContextualAbility()
    {
        foreach (var a in abilities) if (a != null && a.IsAvailable) return a;
        return null;
    }
}

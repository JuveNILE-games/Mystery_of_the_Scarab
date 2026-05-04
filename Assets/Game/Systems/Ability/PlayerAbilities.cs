using UnityEngine;
using System.Collections.Generic;

namespace Game
{
    [RequireComponent(typeof(PlayerInteractor))]
    public class PlayerAbilities : MonoBehaviour
    {
        public List<AbilityBehaviour> abilities = new List<AbilityBehaviour>();
        PlayerInteractor interactor;

        void Awake() { interactor = GetComponent<PlayerInteractor>(); }

        public void OnControlGained() { /* update UI */ }
        public void OnControlLost() { /* maybe disable passive abilities */ }

        // Logic moved to explicit methods called by Input System
        // void Update() removed to prevent double input handling

        public AbilityBehaviour GetContextualAbility()
        {
            foreach (var a in abilities) if (a != null && a.IsAvailable) return a;
            return null;
        }
    }
}

using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes
{
    /// <summary>
    /// Unity Behavior action: uses a specific ability on the companion character.
    /// Requires a PlayerAbilities component and an ability ID to activate.
    /// </summary>
    [Serializable]
    [NodeDescription(
        name: "Use Ability",
        story: "Use ability [AbilityId] on [Abilities]",
        category: "Companion/Actions",
        id: "companion_use_ability")]
    public class UseAbilityAction : Action
    {
        [SerializeReference] public BlackboardVariable<PlayerAbilities> Abilities = new();
        [SerializeReference] public BlackboardVariable<string> AbilityId = new();

        protected override Status OnStart()
        {
            if (Abilities.Value == null || string.IsNullOrEmpty(AbilityId.Value))
                return Status.Failure;

            var ability = Abilities.Value.abilities.Find(
                a => a != null && a.data != null && a.data.abilityId == AbilityId.Value);

            if (ability == null || !ability.IsAvailable)
                return Status.Failure;

            ability.TryUse();
            return Status.Success;
        }
    }
}

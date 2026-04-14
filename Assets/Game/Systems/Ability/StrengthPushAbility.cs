using UnityEngine;

public class StrengthPushAbility : AbilityBehaviour
{
    public float pushForce = 10f;

    protected override bool CanUse()
    {
        var interactables = owner.GetNearbyInteractables();
        foreach (var it in interactables)
        {
            var req = it.GetComponent<RequiresAbility>();
            if (req != null && req.requiredAbilityId == data.abilityId) return true;
        }
        return false;
    }

    protected override void Use()
    {
        var interactables = owner.GetNearbyInteractables();
        foreach (var it in interactables)
        {
            var req = it.GetComponent<RequiresAbility>();
            if (req != null && req.requiredAbilityId == data.abilityId)
            {
                req.OnUsedByPlayer(owner);
                break;
            }
        }
        base.Use();
    }
}

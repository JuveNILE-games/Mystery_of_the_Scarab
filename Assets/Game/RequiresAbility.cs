using UnityEngine;
using UnityEngine.Events;

public class RequiresAbility : MonoBehaviour
{
    public string requiredAbilityId;
    public UnityEvent<PlayerInteractor> OnUsed;

    public void OnUsedByPlayer(PlayerInteractor player)
    {
        var abilities = player.GetComponent<PlayerAbilities>();
        if (abilities != null)
        {
            bool ok = false;
            foreach (var a in abilities.abilities)
            {
                if (a != null && a.data != null && a.data.abilityId == requiredAbilityId) { ok = true; break; }
            }
            if (!ok) return;
        }
        OnUsed?.Invoke(player);
    }
}

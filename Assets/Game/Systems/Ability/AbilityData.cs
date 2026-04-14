using UnityEngine;

[CreateAssetMenu(menuName = "Abilities/AbilityData")]
public class AbilityData : ScriptableObject
{
    public string abilityId;
    public GameObject effectPrefab;
    public float cooldown = 0f;
    public bool isToggle = false;
    public float parameterA = 1f;
    public float parameterB = 0f;
}

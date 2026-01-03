using UnityEngine;

namespace Game.Player.Data
{
    [CreateAssetMenu(fileName = "New Character Data", menuName = "Game/Player/Character Data")]
    public class CharacterData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string characterName = "Adventurer";
        
        [Header("Movement")]
        [SerializeField] private float walkSpeed = 3f;
        [SerializeField] private float sprintSpeed = 6f;
        [SerializeField] private float airControl = 2f;
        
        [Header("Physics")]
        [SerializeField] private float jumpForce = 5f;
        [SerializeField] private float gravityMultiplier = 2f;
        [SerializeField] private float maxFallSpeed = 20f;

        public string CharacterName => characterName;
        public float WalkSpeed => walkSpeed;
        public float SprintSpeed => sprintSpeed;
        public float AirControl => airControl;
        public float JumpForce => jumpForce;
        public float GravityMultiplier => gravityMultiplier;
        public float MaxFallSpeed => maxFallSpeed;
    }
}

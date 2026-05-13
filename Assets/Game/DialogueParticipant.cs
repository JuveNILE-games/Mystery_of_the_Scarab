using UnityEngine;
using Core.Definitions.Character;
using Core.Systems.Dialogue.Trigger;
using Game.Player;

namespace Game
{
    /// <summary>
    /// Generic <see cref="IDialogueParticipant"/> implementation for both players and NPCs.
    /// For players: auto-detects <see cref="PlayerStateMachine"/> and reads live character data.
    /// For NPCs: uses inspector-assigned <see cref="CharacterData"/>.
    /// </summary>
    public class DialogueParticipant : MonoBehaviour, IDialogueParticipant
    {
        [Tooltip("NPC mode: set in inspector. Leave null for player characters (auto-detected).")]
        [SerializeField] private CharacterData npcCharacterData;

        private PlayerStateMachine _playerStateMachine;

        private void Awake()
        {
            _playerStateMachine = GetComponent<PlayerStateMachine>();
        }

        public CharacterData GetCharacterData()
        {
            // Player mode: read live from bindable (supports character switching)
            if (_playerStateMachine != null)
                return _playerStateMachine.Data.Value;

            // NPC mode: return inspector-assigned data
            return npcCharacterData;
        }

        public Transform GetTransform() => transform;
    }
}

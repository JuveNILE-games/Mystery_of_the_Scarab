using System;
using UnityEngine;

namespace Game.Systems.PuzzleSystem.Definitions
{
    public enum PuzzleRewardType
    {
        UnlockDoor,
        SpawnItem,
        TriggerEvent,
        PlayDialogue,
        Custom
    }

    /// <summary>
    /// Data-driven description of what happens when a puzzle is solved or failed.
    /// Interpreted at runtime by PuzzleRewardExecutor.
    /// </summary>
    [Serializable]
    public class PuzzleReward
    {
        public PuzzleRewardType type;

        [Tooltip("Context-dependent ID: doorwayId for UnlockDoor, EventBus key for TriggerEvent, spawn point for SpawnItem.")]
        public string targetId;

        [Tooltip("Optional payload: item SO, DialogueReference (for PlayDialogue), event data SO, etc.")]
        public ScriptableObject rewardData;
    }
}

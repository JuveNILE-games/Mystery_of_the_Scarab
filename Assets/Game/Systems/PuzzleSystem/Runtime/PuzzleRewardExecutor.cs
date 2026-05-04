using System.Collections.Generic;
using Core.Systems.Dialogue;
using Core.Systems.Services.Interfaces;
using Game.Systems.PuzzleSystem.Definitions;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Core.Utility.Attributes;

namespace Game.Systems.PuzzleSystem.Runtime
{
    /// <summary>
    /// Translates PuzzleReward data into concrete game actions.
    /// Attach to the LevelManager root alongside LevelController.
    /// </summary>
    public class PuzzleRewardExecutor : MonoBehaviour
    {
        [Inject] private IDialogueService _dialogueService;

        public void Execute(IReadOnlyList<PuzzleReward> rewards)
        {
            if (rewards == null) return;
            foreach (var reward in rewards)
                Execute(reward);
        }

        public void Execute(PuzzleReward reward)
        {
            switch (reward.type)
            {
                case PuzzleRewardType.UnlockDoor:
                    // TODO: Find DoorwayMarker by reward.targetId and unlock
                    Debug.Log($"[PuzzleRewardExecutor] UnlockDoor: {reward.targetId}");
                    break;

                case PuzzleRewardType.TriggerEvent:
                    // TODO: Publish via IEventBus
                    Debug.Log($"[PuzzleRewardExecutor] TriggerEvent: {reward.targetId}");
                    break;

                case PuzzleRewardType.PlayDialogue:
                    var dialogueRef = reward.rewardData as DialogueReference;
                    _dialogueService?.StartAsync(dialogueRef).Forget();
                    break;

                case PuzzleRewardType.SpawnItem:
                    // TODO: Resolve spawn system
                    Debug.Log($"[PuzzleRewardExecutor] SpawnItem: {reward.targetId}");
                    break;

                case PuzzleRewardType.Custom:
                    // TODO: Custom reward handling
                    Debug.Log($"[PuzzleRewardExecutor] Custom: {reward.targetId}");
                    break;
            }
        }
    }
}

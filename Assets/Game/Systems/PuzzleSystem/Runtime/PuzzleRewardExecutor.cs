using System.Collections.Generic;
using Core.Systems.Dialogue;
using Core.Systems.Logging;
using Core.Systems.Services.Interfaces;
using Core.Systems.Signals;
using Game.Systems.PuzzleSystem.Definitions;
using Game.Systems.PuzzleSystem.Signals;
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
        // [Inject] fields are filled by MonoBehaviourInjection.InjectAllMonoBehaviours,
        // called by Bootstrapper.OnSceneLoaded. Attach this component to a GameObject
        // that is present in a scene loaded after the service bootstrapper.
        [Inject] private IDialogueService _dialogueService;
        [Inject] private IEventBus _eventBus;
        [Inject] private ILoggerService _logger;

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
                    // TODO: Find DoorwayMarker by reward.targetId and unlock — no door system
                    // exists in this codebase yet. Needs a design decision before implementing.
                    _logger?.LogWarning(this, $"[PuzzleRewardExecutor] UnlockDoor '{reward.targetId}' not implemented — no door system exists yet.");
                    break;

                case PuzzleRewardType.TriggerEvent:
                    _eventBus?.Publish(new PuzzleRewardEventSignal(reward.targetId));
                    break;

                case PuzzleRewardType.PlayDialogue:
                    var dialogueRef = reward.rewardData as DialogueReference;
                    _dialogueService?.StartAsync(dialogueRef).Forget();
                    break;

                case PuzzleRewardType.SpawnItem:
                    // TODO: Resolve spawn system — no item/inventory system exists in this
                    // codebase yet. Needs a design decision before implementing.
                    _logger?.LogWarning(this, $"[PuzzleRewardExecutor] SpawnItem '{reward.targetId}' not implemented — no item system exists yet.");
                    break;

                case PuzzleRewardType.Custom:
                    if (PuzzleCustomRewardRegistry.TryGet(reward.targetId, out var handler))
                        handler.Handle(reward);
                    else
                        _logger?.LogWarning(this, $"[PuzzleRewardExecutor] Custom reward '{reward.targetId}' has no registered handler.");
                    break;
            }
        }
    }
}

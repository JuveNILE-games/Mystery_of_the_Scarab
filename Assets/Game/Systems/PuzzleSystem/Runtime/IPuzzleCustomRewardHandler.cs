using Game.Systems.PuzzleSystem.Definitions;

namespace Game.Systems.PuzzleSystem.Runtime
{
    /// <summary>
    /// Implement and register with PuzzleCustomRewardRegistry to handle
    /// PuzzleRewardType.Custom rewards without editing PuzzleRewardExecutor itself.
    /// </summary>
    public interface IPuzzleCustomRewardHandler
    {
        void Handle(PuzzleReward reward);
    }
}

namespace Game.Systems.PuzzleSystem.Signals
{
    /// <summary>Published by PuzzleRewardExecutor for PuzzleRewardType.TriggerEvent rewards.</summary>
    public readonly struct PuzzleRewardEventSignal { public readonly string EventKey; public PuzzleRewardEventSignal(string eventKey) { EventKey = eventKey; } }
}

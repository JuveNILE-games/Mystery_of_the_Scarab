namespace Game.Systems.PuzzleSystem.Interfaces{
    public interface IPuzzleConditionDescriptor
    {
        string ConditionId { get; }
        // Resolved by PuzzleRoomController at level load
    }
}
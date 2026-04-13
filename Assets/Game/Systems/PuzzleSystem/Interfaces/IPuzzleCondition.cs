using System;

namespace Game.Systems.PuzzleSystem.Interfaces{
    public interface IPuzzleCondition{
        string ConditionId { get; }
        bool IsMet { get; }
        event Action<IPuzzleCondition> OnConditionChanged;
        void Reset();
    }
}
using Game.Systems.PuzzleSystem;
using Game.Systems.PuzzleSystem.Runtime;

namespace Game.AI
{
    /// <summary>
    /// Lightweight snapshot of puzzle state at the moment of the last AI evaluation.
    /// Passed to the blackboard so the Behavior Graph can make decisions without
    /// re-querying the puzzle system on every tick.
    /// </summary>
    public class CompanionPuzzleContext
    {
        public PuzzleController ActivePuzzle;
        public PuzzleComponent TargetComponent;
        public float ConfidenceScore;   // 0–1: how certain the AI is this is the right target
        public bool IsAmbiguous;       // true when multiple equally-scored targets exist
        public float ReactionDelay;     // seconds to wait before acting (set by observer)
    }
}

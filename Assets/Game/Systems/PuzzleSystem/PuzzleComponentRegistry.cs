using System.Collections.Generic;
using Game.Systems.PuzzleSystem.Interfaces;

namespace Game.Systems.PuzzleSystem{
    public static class PuzzleComponentRegistry
    {
        private static readonly Dictionary<string, IPuzzleCondition> _map = new();

        public static void Register(IPuzzleCondition c)
        {
            if (!string.IsNullOrEmpty(c.ConditionId))
                _map[c.ConditionId] = c;
        }

        public static void Unregister(IPuzzleCondition c)
        {
            if (!string.IsNullOrEmpty(c.ConditionId))
                _map.Remove(c.ConditionId);
        }

        public static bool TryGet(string id, out IPuzzleCondition condition)
            => _map.TryGetValue(id, out condition);

        public static void Clear() => _map.Clear();
    }
}
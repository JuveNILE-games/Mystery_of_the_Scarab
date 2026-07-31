using System.Collections.Generic;

namespace Game.Systems.PuzzleSystem.Runtime
{
    /// <summary>Maps PuzzleReward.targetId to a custom handler for PuzzleRewardType.Custom.</summary>
    public static class PuzzleCustomRewardRegistry
    {
        private static readonly Dictionary<string, IPuzzleCustomRewardHandler> _map = new();

        public static void Register(string targetId, IPuzzleCustomRewardHandler handler)
        {
            if (!string.IsNullOrEmpty(targetId))
                _map[targetId] = handler;
        }

        public static void Unregister(string targetId)
        {
            if (!string.IsNullOrEmpty(targetId))
                _map.Remove(targetId);
        }

        public static bool TryGet(string targetId, out IPuzzleCustomRewardHandler handler)
            => _map.TryGetValue(targetId, out handler);

        public static void Clear() => _map.Clear();
    }
}

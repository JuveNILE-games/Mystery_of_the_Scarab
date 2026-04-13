using System;
using System.Collections.Generic;
using System.Linq;
using Game.Systems.LogicSystem.Interfaces;
using Game.Systems.PuzzleSystem.Interfaces;

namespace Game.Systems.LogicSystem{
    [Serializable]
    public class LogicLeaf : ILogicNode
    {
        public string conditionId;

        [NonSerialized]
        public IPuzzleCondition ResolvedCondition;

        public bool Evaluate() => ResolvedCondition?.IsMet ?? false;
        public IEnumerable<ILogicNode> GetChildren() => Enumerable.Empty<ILogicNode>();
    }
}
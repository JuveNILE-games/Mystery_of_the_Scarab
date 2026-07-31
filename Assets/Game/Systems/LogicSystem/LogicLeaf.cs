using System;
using System.Collections.Generic;
using System.Linq;
using Game.Systems.LogicSystem.Interfaces;
using Game.Systems.PuzzleSystem.Interfaces;
using UnityEngine;

namespace Game.Systems.LogicSystem{
    [Serializable]
    public class LogicLeaf : ILogicNode
    {
        public string conditionId;

        [NonSerialized]
        public IPuzzleCondition ResolvedCondition;

        public bool Evaluate()
        {
            // ResolvedCondition is typically backed by a PuzzleComponent (MonoBehaviour). `?.`
            // on the interface reference uses plain C# null-checking, not Unity's fake-null
            // override, so a destroyed-but-not-cleared component would throw
            // MissingReferenceException from .IsMet instead of degrading to "unmet".
            if (ResolvedCondition is UnityEngine.Object unityObject)
                return unityObject != null && ResolvedCondition.IsMet;

            return ResolvedCondition != null && ResolvedCondition.IsMet;
        }

        public IEnumerable<ILogicNode> GetChildren() => Enumerable.Empty<ILogicNode>();
    }
}
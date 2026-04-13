using System;
using Game.Systems.PuzzleSystem.Interfaces;
using UnityEngine;

namespace Game.Systems.PuzzleSystem{
    // A self-contained condition that needs no scene object (e.g. event-driven)
    [Serializable]
    public class InlineConditionDescriptor : IPuzzleConditionDescriptor
    {
        [SerializeReference] public IPuzzleCondition condition;
        public string ConditionId => condition?.ConditionId;
    }
}
using System;
using Game.Systems.PuzzleSystem.Interfaces;

namespace Game.Systems.PuzzleSystem{
    // Finds a MonoBehaviour on a scene object by its conditionId
    [Serializable]
    public class SceneConditionDescriptor : IPuzzleConditionDescriptor
    {
        public string conditionId;
        public string ConditionId => conditionId;
    }
}
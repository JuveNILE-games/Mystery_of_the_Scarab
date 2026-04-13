using System;
using System.Collections.Generic;
using Core.Systems.AudioSystem;
using Core.Systems.Dialogue;
using Game.Systems.LogicSystem.Interfaces;
using Game.Systems.PuzzleSystem.Interfaces;
using UnityEngine;

namespace Game.Systems.PuzzleSystem.Definitions{
    [CreateAssetMenu(menuName = "JuveNILE Games/Mystery of the Scarab/Puzzle System/Puzzle Definition")]
    public class PuzzleDefinition : ScriptableObject{
        [Header("Identity")] 
        public string puzzleId;
        public string displayName;
        [TextArea] public string description;

        [Header("Logic Tree")]
        [SerializeReference]
        public ILogicNode rootNode;

        [Header("Conditions")] 
        [SerializeReference]
        public List<IPuzzleConditionDescriptor> conditionDescriptors;

        [Header("Locking")] 
        // The puzzle is hidden/inert until this other puzzle is solved first.
        public PuzzleDefinition prerequisite;

        [Header("Puzzle Components")] 
        // Optional: list every conditionId this puzzle uses.
        // // Used by the Editor to validate that all IDs exist in the scene.
        // // At runtime, PuzzleController resolves them from the scene automatically.
        public List<string> expectedConditionIds;

        [Header("State")] public bool startsLocked;
        public bool resetsOnRoomExit;
        public bool resetsOnLevelRestart;

        [Header("Failure")] public bool canFail; // e.g. a timed puzzle
        public float timeLimit; // seconds; ignored if canFail = false

        [Header("Rewards & Consequences")] public List<PuzzleReward> onSolvedRewards;
        public List<PuzzleReward> onFailedPenalties;

        [Header("Hints")] public List<HintDefinition> hints;

        [Header("Narrative")] public DialogueReference onSolvedDialogue;
        public DialogueReference onFailedDialogue;
        public SoundData solvedSfx;
        public SoundData failedSfx;
    }

    [Serializable]
    public class HintDefinition{
        public string hintText;
        public Sprite hintImage;
        public int requiredAttempts; // 0 = available immediately
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using SpriteAnimations;

namespace Game.Systems.AnimationEvents
{
    [CreateAssetMenu(fileName = "AnimationEventProfile", menuName = "Game/Animation Event Profile")]
    public class AnimationEventProfile : ScriptableObject
    {
        [Tooltip("List of event definitions for specific animations.")]
        public List<AnimationEventDefinition> Events = new List<AnimationEventDefinition>();

        public AnimationEventDefinition GetDefinition(SpriteAnimation animation)
        {
            return Events.Find(x => x.Animation == animation);
        }
    }

    [Serializable]
    public class AnimationEventDefinition
    {
        [Tooltip("The animation asset to listen to.")]
        public SpriteAnimation Animation;

        [Tooltip("Events to trigger on specific frame indices.")]
        public List<FrameEvent> FrameEvents = new List<FrameEvent>();

        [Tooltip("Event to trigger when the animation ends.")]
        public UnityEvent OnEnd;
    }

    [Serializable]
    public class FrameEvent
    {
        [Tooltip("The frame index (0-based) to trigger the event on.")]
        public int FrameIndex;

        [Tooltip("The UnityEvent to invoke.")]
        public UnityEvent OnEvent;
    }
}

using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;

namespace Game.AI.Nodes
{
    [Serializable]
    [NodeDescription(
        name: "Companion Reaction Delay",
        story: "Pause [Duration] seconds before acting (reaction simulation)",
        category: "Companion/Actions",
        id: "companion_reaction_delay")]
    public class ReactionDelayAction : Action
    {
        [SerializeReference] public BlackboardVariable<float> Duration = new(0.8f);
        [SerializeReference] public BlackboardVariable<bool> Randomize = new(true);
        [SerializeReference] public BlackboardVariable<float> RandomMin = new(0.4f);
        [SerializeReference] public BlackboardVariable<float> RandomMax = new(1.6f);

        private float _elapsed;
        private float _target;

        protected override Status OnStart()
        {
            _elapsed = 0f;
            _target = Randomize.Value
                ? UnityEngine.Random.Range(RandomMin.Value, RandomMax.Value)
                : Duration.Value;
            return Status.Running;
        }

        protected override Status OnUpdate()
        {
            _elapsed += Time.deltaTime;
            return _elapsed >= _target ? Status.Success : Status.Running;
        }
    }
}

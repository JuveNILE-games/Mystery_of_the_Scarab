using System.Collections.Generic;
using UnityEngine;
using SpriteAnimations;
using UnityEngine.Events;

namespace Game.Systems.AnimationEvents
{
    /// <summary>
    /// An extended SpriteAnimator that supports Animation Events via an AnimationEventProfile.
    /// </summary>
    /// <summary>
    /// An extended SpriteAnimator that supports Animation Events via Profile, Inspector, or Code.
    /// </summary>
    [AddComponentMenu("Game/Systems/AnimationEvents/Sprite Animator With Events")]
    public class SpriteAnimatorWithEvents : SpriteAnimator
    {
        [Header("Events Configuration")]
        [Tooltip("Directly bind events to animations in the Inspector.")]
        [SerializeField] private List<InspectorEventBinding> _inspectorBindings = new List<InspectorEventBinding>();

        [Tooltip("Optional Profile containing shared event definitions.")]
        [SerializeField] private AnimationEventProfile _eventProfile;

        // Runtime storage for code bindings
        private Dictionary<SpriteAnimation, Dictionary<int, System.Action>> _codeIndexBindings = new Dictionary<SpriteAnimation, Dictionary<int, System.Action>>();
        private Dictionary<SpriteAnimation, Dictionary<string, System.Action>> _codeIdBindings = new Dictionary<SpriteAnimation, Dictionary<string, System.Action>>();

        /// <summary>
        /// Gets or sets the event profile at runtime.
        /// </summary>
        public AnimationEventProfile EventProfile
        {
            get => _eventProfile;
            set => _eventProfile = value;
        }

        protected override void Awake()
        {
            base.Awake();
            if (AnimationChanged != null)
                AnimationChanged.AddListener(OnAnimationChanged);
        }

        protected virtual void OnDestroy()
        {
            if (AnimationChanged != null)
                AnimationChanged.RemoveListener(OnAnimationChanged);
        }

        // --- Binding API ---

        public void Bind(SpriteAnimation animation, int frameIndex, System.Action callback)
        {
            if (animation == null || callback == null) return;
            if (!_codeIndexBindings.ContainsKey(animation)) _codeIndexBindings[animation] = new Dictionary<int, System.Action>();
            
            if (_codeIndexBindings[animation].ContainsKey(frameIndex))
                 _codeIndexBindings[animation][frameIndex] += callback;
            else _codeIndexBindings[animation][frameIndex] = callback;

            RefreshBindings();
        }

        public void Bind(SpriteAnimation animation, string frameId, System.Action callback)
        {
            if (animation == null || callback == null || string.IsNullOrEmpty(frameId)) return;
            if (!_codeIdBindings.ContainsKey(animation)) _codeIdBindings[animation] = new Dictionary<string, System.Action>();

            if (_codeIdBindings[animation].ContainsKey(frameId))
                 _codeIdBindings[animation][frameId] += callback;
            else _codeIdBindings[animation][frameId] = callback;

            RefreshBindings();
        }

        public void Unbind(SpriteAnimation animation, int frameIndex, System.Action callback)
        {
            if (animation == null || !_codeIndexBindings.ContainsKey(animation)) return;
            if (_codeIndexBindings[animation].ContainsKey(frameIndex))
            {
                _codeIndexBindings[animation][frameIndex] -= callback;
                if (_codeIndexBindings[animation][frameIndex] == null) _codeIndexBindings[animation].Remove(frameIndex);
                RefreshBindings();
            }
        }

        public void Unbind(SpriteAnimation animation, string frameId, System.Action callback)
        {
            if (animation == null || !_codeIdBindings.ContainsKey(animation)) return;
            if (_codeIdBindings[animation].ContainsKey(frameId))
            {
                _codeIdBindings[animation][frameId] -= callback;
                if (_codeIdBindings[animation][frameId] == null) _codeIdBindings[animation].Remove(frameId);
                RefreshBindings();
            }
        }

        // --- Internal Logic ---

        private void OnAnimationChanged(SpriteAnimation animation)
        {
            RefreshBindings();
        }

        private void RefreshBindings()
        {
            if (_currentAnimation == null || _currentPerformer == null) return;

            // Temporary collection to aggregate actions per frame/id
            var indexActions = new Dictionary<int, System.Action>();
            var idActions = new Dictionary<string, System.Action>();
            System.Action endActions = null;

            // 1. Collect from Profile
            if (_eventProfile != null)
            {
                var definition = _eventProfile.GetDefinition(_currentAnimation);
                if (definition != null)
                {
                    foreach (var frameEvent in definition.FrameEvents)
                    {
                        AddToDictionary(indexActions, frameEvent.FrameIndex, () => frameEvent.OnEvent?.Invoke());
                    }
                    if (definition.OnEnd != null && definition.OnEnd.GetPersistentEventCount() > 0)
                    {
                        endActions += () => definition.OnEnd.Invoke();
                    }
                }
            }

            // 2. Collect from Inspector Bindings
            if (_inspectorBindings != null)
            {
                foreach (var binding in _inspectorBindings)
                {
                    if (binding.Animation == _currentAnimation)
                    {
                        var action = (System.Action)(() => binding.OnEvent?.Invoke());
                        if (binding.Trigger == BindingTriggerType.FrameId && !string.IsNullOrEmpty(binding.FrameId))
                        {
                            AddToDictionary(idActions, binding.FrameId, action);
                        }
                        else if (binding.Trigger == BindingTriggerType.FrameIndex)
                        {
                            AddToDictionary(indexActions, binding.FrameIndex, action);
                        }
                        else if (binding.Trigger == BindingTriggerType.OnEnd)
                        {
                            endActions += action;
                        }
                    }
                }
            }

            // 3. Collect from Code Bindings
            if (_codeIndexBindings.TryGetValue(_currentAnimation, out var indices))
            {
                foreach (var kvp in indices) AddToDictionary(indexActions, kvp.Key, kvp.Value);
            }
            if (_codeIdBindings.TryGetValue(_currentAnimation, out var ids))
            {
                foreach (var kvp in ids) AddToDictionary(idActions, kvp.Key, kvp.Value);
            }

            // 4. Register Accumulated Actions to Performer
            foreach (var kvp in indexActions)
            {
                // We must recapture variables for the lambda
                var acts = kvp.Value;
                _currentPerformer.SetOnFrame(kvp.Key, (frame) => acts?.Invoke());
            }

            foreach (var kvp in idActions)
            {
                var acts = kvp.Value;
                _currentPerformer.SetOnFrame(kvp.Key, (frame) => acts?.Invoke());
            }

            if (endActions != null)
            {
                _currentPerformer.SetOnEnd(() => endActions.Invoke());
            }
        }

        private void AddToDictionary<T>(Dictionary<T, System.Action> dict, T key, System.Action action)
        {
            if (dict.ContainsKey(key)) dict[key] += action;
            else dict[key] = action;
        }
    }

    [System.Serializable]
    public class InspectorEventBinding
    {
        public string Name;
        public SpriteAnimation Animation;
        public BindingTriggerType Trigger = BindingTriggerType.FrameId;
        public string FrameId;
        public int FrameIndex;
        public UnityEvent OnEvent;
    }

    public enum BindingTriggerType
    {
        FrameId,
        FrameIndex,
        OnEnd
    }
}

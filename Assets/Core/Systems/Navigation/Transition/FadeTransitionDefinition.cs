using System;
using System.Collections;
using UnityEngine;

namespace Core.Systems.Navigation.Transition{
    [CreateAssetMenu(fileName = "FadeTransitionDefinition", menuName = "Core/Navigation/TransitionDefinition/Fade")]
    public class FadeTransitionDefinition : BaseTransitionDefinition
    {
        [Header("Fade Settings")]
        public float duration;
        
        public override IEnumerator Play(CanvasGroup canvasGroup, Action onSwitch, Action onFinish)
        {
            if (canvasGroup == null)
            {
                // No screen to fade out — just fade in the new screen after switch
                onSwitch?.Invoke();
        
                // Assume new screen is now active — get its CanvasGroup
                if (ScreenNavigator.Instance.TryGetActiveCanvasGroup(out var newGroup))
                {
                    float time = 0;
                    while (time < duration)
                    {
                        newGroup.alpha = Mathf.Lerp(0f, 1f, time / duration);
                        time += Time.deltaTime;
                        yield return null;
                    }
                    newGroup.alpha = 1f;
                }
        
                onFinish?.Invoke();
                yield break;
            }

            // Normal fade out → switch → fade in
            yield return DoFade(canvasGroup, onSwitch, onFinish);
        }

        private IEnumerator DoFade(CanvasGroup group, Action onSwitch, Action onFinish)
        {
            // Fade out
            float time = 0;
            while (time < duration)
            {
                group.alpha = Mathf.Lerp(1f, 0f, time / duration);
                time += Time.deltaTime;
                yield return null;
            }
            group.alpha = 0f;
            onSwitch?.Invoke(); // Switch the screen content here

            // Fade in
            time = 0;
            while (time < duration)
            {
                group.alpha = Mathf.Lerp(0f, 1f, time / duration);
                time += Time.deltaTime;
                yield return null;
            }
            group.alpha = 1f;
            onFinish?.Invoke();
        }
    }
}

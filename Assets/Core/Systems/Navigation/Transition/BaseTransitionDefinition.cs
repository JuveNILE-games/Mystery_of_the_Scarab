using System;
using System.Collections;
using UnityEngine;

namespace Core.Systems.Navigation.Transition{
    public abstract class BaseTransitionDefinition : ScriptableObject
    {
        public abstract IEnumerator Play(CanvasGroup canvasGroup, Action onSwitch, Action onFinished);
    }
}

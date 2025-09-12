using System.Collections;
using System.Collections.Generic;
using Core.Utility.Attributes;
using Core.Systems.Navigation.Transition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityUtils;

namespace Core.Systems.Navigation
{
    public class ScreenNavigator : PersistentSingleton<ScreenNavigator>
    {
        public EventSystem eventSystem;

        [SerializeField, ReadOnly] private Transform _screenContainer;
        private readonly Dictionary<ScreenDefinition, GameObject> _screenMap = new();
        [SerializeField, ReadOnly] private ScreenDefinition _currentScreen;
        [SerializeField] private BaseTransitionDefinition defaultTransition;

        public void RegisterScreenContainer(Transform screenContainer)
        {
            if (_screenContainer) return;
            _screenContainer = screenContainer;
            InitializeScreens();
        }

        private void InitializeScreens()
        {
            foreach (Transform child in _screenContainer)
            {
                var screenComponent = child.GetComponent<ScreenComponent>();
                if (screenComponent && screenComponent.definition)
                {
                    _screenMap[screenComponent.definition] = child.gameObject;
                    child.gameObject.SetActive(false);
                }
            }
        }

        public void NavigateTo(ScreenDefinition targetScreen)
        {
            if (targetScreen == null)
            {
                Debug.LogWarning("Target screen is null.");
                return;
            }

            if (_currentScreen != null && !targetScreen.CanTransitionFrom(_currentScreen))
            {
                Debug.LogWarning($"Transition from {_currentScreen.name} to {targetScreen.name} is not allowed.");
                return;
            }

            StartCoroutine(HandleTransition(_currentScreen, targetScreen));
        }

        private IEnumerator HandleTransition(ScreenDefinition fromScreen, ScreenDefinition toScreen)
        {
            _screenMap.TryGetValue(toScreen, out var toGO);

            // SAFE: fromScreen can be null on first boot
            GameObject fromGO = null;
            if (fromScreen != null)
            {
                _screenMap.TryGetValue(fromScreen, out fromGO);
            }

            var fromComponent = fromGO ? fromGO.GetComponent<ScreenComponent>() : null;
            var toComponent = toGO.GetComponent<ScreenComponent>();

            BaseTransitionDefinition transition = null;

            // 1. Prefer per-screen transition
            var incoming = toScreen.GetTransitionEffect(fromScreen);
            if (incoming.HasValue && incoming.Value.transitionEffect is BaseTransitionDefinition incomingTransition)
            {
                transition = incomingTransition;
            }
            else if (toScreen.screenTransitionEffect != null)
            {
                transition = toScreen.screenTransitionEffect;
            }
            else
            {
                transition = defaultTransition; // ✅ Fallback
            }

            if (transition == null)
            {
                // Completely fallback if nothing was assigned
                if (fromGO) fromGO.SetActive(false);
                fromScreen?.TriggerOnClose();
                fromComponent?.OnClose();

                _currentScreen = toScreen;
                toGO.SetActive(true);
                toScreen.TriggerOnOpen();
                toComponent?.OnOpen();
                yield break;
            }

            yield return transition.Play(
                fromComponent?.canvasGroup,
                onSwitch: () =>
                {
                    if (fromGO) fromGO.SetActive(false);
                    fromScreen?.TriggerOnClose();
                    fromComponent?.OnClose();

                    _currentScreen = toScreen;
                    toGO.SetActive(true);
                    toScreen.TriggerOnOpen();
                    toComponent?.OnOpen();
                },
                onFinished: () => { }
            );
        }



        public void GoBack()
        {
            if (_currentScreen != null && _currentScreen.screenOnEscape != null)
            {
                NavigateTo(_currentScreen.screenOnEscape);
            }
        }
        
        public bool TryGetActiveCanvasGroup(out CanvasGroup group)
        {
            group = null;
            if (_currentScreen == null) return false;
            if (_screenMap.TryGetValue(_currentScreen, out var go))
            {
                var comp = go.GetComponent<ScreenComponent>();
                if (comp != null)
                {
                    group = comp.canvasGroup;
                    return true;
                }
            }
            return false;
        }

    }
}

using System;
using System.Collections.Generic;
using Core.Systems.Navigation.Transition;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace Core.Systems.Navigation{
    [CreateAssetMenu(fileName = "ScreenDefinition", menuName = "Core/Navigation/Screen Definition", order = 1)]
    public class ScreenDefinition : ScriptableObject
    {
        [Serializable]
        public struct IncomingScreenSetup
        {
            public ScreenDefinition screen;
            public ScriptableObject transitionEffect;
            // public ScriptableObject screenManipulator;
        }

        public enum IncomingScreenListMode
        {
            AllowOnlySpecificScreens,
            AllowAllExceptSpecificScreens
        }
        
        [Header("View")]
        public UIDocument document;

        [Header("Navigation Settings")]
        public IncomingScreenListMode screenListMode = IncomingScreenListMode.AllowOnlySpecificScreens;
        public List<IncomingScreenSetup> incomingScreens = new List<IncomingScreenSetup>();
        public BaseTransitionDefinition screenTransitionEffect;
        
        [Header("Conditions")]
        public List<ScriptableObject> screenConditions;

        [Header("On Escape")]
        public ScreenDefinition screenOnEscape;
        public bool showConfirmPopup = false;
        public string confirmTitle;
        public string confirmMessage;

        [Header("Events")]
        public UnityEvent onOpen;
        public UnityEvent onClose;

        public bool CanTransitionFrom(ScreenDefinition sourceScreen)
        {
            if (screenListMode == IncomingScreenListMode.AllowAllExceptSpecificScreens)
            {
                return !incomingScreens.Exists(s => s.screen == sourceScreen);
            }
            else
            {
                return incomingScreens.Exists(s => s.screen == sourceScreen);
            }
        }

        public void TriggerOnOpen()
        {
            onOpen?.Invoke();
        }

        public void TriggerOnClose()
        {
            onClose?.Invoke();
        }

        public IncomingScreenSetup? GetTransitionEffect(ScreenDefinition incomingScreen)
        {
            var match = incomingScreens.Find(s => s.screen == incomingScreen);
            return match.screen != null ? (IncomingScreenSetup?)match : null;
        }
    }
}
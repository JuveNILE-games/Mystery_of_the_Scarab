using Core.Systems.Navigation;
using Core.Systems.Navigation.Definitions;
using Core.Systems.SceneManagement.Components;
using Core.UI;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Cysharp.Threading.Tasks;
using FluentUI;
using NetCore.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views.ModeSelect
{
    /// <summary>
    /// First real click-through step after "Play" — Single Player vs Multiplayer.
    /// Reuses the existing LoadSceneByButton (already configured for SampleScene.unity)
    /// rather than reimplementing scene loading; only sets the session mode first.
    /// </summary>
    public class ModeSelectScreen : MonoBehaviour
    {
        [Inject] private ISessionService _session;

        [SerializeField] private UIDocument document;
        [SerializeField] private OverlayDefinition _multiplayerOverlay;
        [SerializeField] private LoadSceneByButton _singlePlayerSceneLoader;

        public void OnOpen()
        {
            var root = document.rootVisualElement;
            root.Clear();

            var column = Layout.Column("ModeSelect").Classes("menu-container").Grow();
            column.Add(CreateButton("Single Player", OnSinglePlayerClicked));
            column.Add(CreateButton("Multiplayer", OnMultiplayerClicked));
            root.Add(column);
        }

        public void OnClose()
        {
            document.rootVisualElement.Clear();
        }

        private static Button CreateButton(string text, System.Action onClick)
        {
            var btn = new Button(onClick);
            btn.AddToClassList("menu-button");
            btn.text = text;
            return btn;
        }

        private void OnSinglePlayerClicked()
        {
            StartSessionThenLoad(SessionMode.Solo).Forget();
        }

        private void OnMultiplayerClicked()
        {
            Navigation.ShowOverlay(_multiplayerOverlay);
        }

        private async UniTask StartSessionThenLoad(SessionMode mode)
        {
            if (_session != null)
                await _session.StartSession(mode);

            if (_singlePlayerSceneLoader != null)
                _singlePlayerSceneLoader.LoadScene();
            else
                Debug.LogError("[ModeSelectScreen] _singlePlayerSceneLoader not assigned.", this);
        }
    }
}

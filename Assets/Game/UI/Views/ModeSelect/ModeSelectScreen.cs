using Core.Systems.InputManagement;
using Core.Systems.Navigation;
using Core.Systems.Navigation.Definitions;
using Core.Systems.SceneManagement.Components;
using Core.Systems.Theming;
using Core.UI;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Core.Utility.FluentUI.Icons.Lucide;
using Cysharp.Threading.Tasks;
using FluentUI;
using NetCore.Interfaces;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [Inject] private IThemeService _themeService;
        [Inject] private InputReader _inputReader;

        [SerializeField] private UIDocument document;
        [SerializeField] private OverlayDefinition _multiplayerOverlay;
        [SerializeField] private LoadSceneByButton _singlePlayerSceneLoader;

        public void OnOpen()
        {
            var root = document.rootVisualElement;
            root.Clear();

            if (_themeService != null)
                _themeService.ApplyTheme(root);
            else
                Debug.LogWarning("[ModeSelectScreen] ThemeService not injected!");

            var column = Layout.Column("ModeSelect").Classes("menu-container").Grow().Opacity(0);
            column.Add(CreateMenuButton("Single Player", LucideIconName.User, OnSinglePlayerClicked));
            column.Add(CreateMenuButton("Multiplayer", LucideIconName.Users, OnMultiplayerClicked));
            column.FocusFirstInteractableOnLayout();
            root.Add(column);

            root.Q(className: "menu-container").style.opacity = 1f;

            if (_inputReader != null)
                _inputReader.SubscribePerformed("Cancel", OnCancelPressed);
        }

        public void OnClose()
        {
            if (_inputReader != null)
                _inputReader.UnsubscribePerformed("Cancel", OnCancelPressed);

            document.rootVisualElement.Clear();
        }

        private void OnCancelPressed(InputAction.CallbackContext _)
        {
            // While the Multiplayer overlay is up, Cancel should close it, not navigate this
            // screen away from underneath it — its own Cancel handler owns that case.
            if (Navigation.Service != null && Navigation.Service.IsOverlayOpen(_multiplayerOverlay))
                return;

            Navigation.NavigateBack();
        }

        private static Button CreateMenuButton(string text, LucideIconName icon, System.Action onClick)
        {
            var btn = new Button(onClick).Classes("menu-button");
            btn.Add(new LucideIcon(icon).Classes("menu-button__icon"));
            btn.Add(new Label(text));
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
            {
                _singlePlayerSceneLoader.LoadScene();
            }
            
            else
                Debug.LogError("[ModeSelectScreen] _singlePlayerSceneLoader not assigned.", this);
        }
    }
}

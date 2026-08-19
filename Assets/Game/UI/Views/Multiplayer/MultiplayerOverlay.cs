using System.Threading;
using Core.Systems.Navigation.Canvases;
using Core.Systems.SceneManagement.Components;
using Core.Systems.Theming;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Core.Utility.FluentUI.Icons.Lucide;
using Cysharp.Threading.Tasks;
using FluentUI;
using NetCore.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views.Multiplayer
{
    /// <summary>
    /// Local vs Online choice, styled and animated the same way as SettingsPanel (backdrop +
    /// left-edge sliding drawer, reusing its .settings-backdrop/.settings-panel USS classes) —
    /// achieved by extending OverlayCanvas directly rather than sitting next to a separate one,
    /// same as SettingsPanel does, so the same OnOpenAnimatedAsync/OnCloseAnimatedAsync hooks apply.
    /// Local reuses the same local scene load as Single Player (no PurrNet involved) — only the
    /// session mode differs. Online hands off to the lobby flow (Lobby.unity); actual networking
    /// starts later, once all lobby players are ready (see NetCoreConnectionService.RetryConnection).
    /// </summary>
    public class MultiplayerOverlay : OverlayCanvas
    {
        [Header("UI Document")]
        [SerializeField] private UIDocument document;

        [Header("Styling")]
        [SerializeField] private StyleSheet _panelStyleSheet;

        [Inject] private ISessionService _session;
        [Inject] private IThemeService _themeService;

        [SerializeField] private LoadSceneByButton _localSceneLoader;   // targets SampleScene.unity
        [SerializeField] private LoadSceneByButton _onlineSceneLoader;  // targets Lobby.unity

        private VisualElement _backdrop;
        private VisualElement _panel;
        private bool _uiBuilt;

        protected override void OnEnable()
        {
            base.OnEnable();

            if (!_uiBuilt)
            {
                BuildUI();
                _uiBuilt = true;
            }
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            _uiBuilt = false;

            // Reset visual state so it doesn't flash open on next show.
            _backdrop?.RemoveFromClassList("settings-backdrop--open");
            _panel?.RemoveFromClassList("settings-panel--open");
        }

        private void BuildUI()
        {
            if (document == null)
            {
                Debug.LogWarning("[MultiplayerOverlay] UIDocument not assigned!");
                return;
            }

            var root = document.rootVisualElement;
            root.Clear();

            if (_panelStyleSheet != null)
                root.styleSheets.Add(_panelStyleSheet);

            if (_themeService != null)
                _themeService.ApplyTheme(root);
            else
                Debug.LogWarning("[MultiplayerOverlay] ThemeService not injected!");

            _backdrop = new VisualElement().Classes("settings-backdrop");

            // Click-away: only close when clicking the backdrop itself, not the panel.
            _backdrop.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.target == _backdrop) CloseOverlay();
            });

            _panel = Layout.Column("MultiplayerPanel").Classes("settings-panel");
            _panel.Add(BuildHeader());

            var content = Layout.Column("MultiplayerContent").Classes("settings-content");
            content.Add(CreateMenuButton("Local", LucideIconName.MonitorSmartphone, OnLocalClicked));
            content.Add(CreateMenuButton("Online", LucideIconName.Globe, OnOnlineClicked));
            content.FocusFirstInteractableOnLayout();
            _panel.Add(content);

            _backdrop.Add(_panel);
            root.Add(_backdrop);
        }

        private VisualElement BuildHeader()
        {
            var header = Layout.Row("MultiplayerHeader").Classes("settings-header");
            header.Add(new Label("Multiplayer").Classes("settings-title"));

            var closeBtn = new Button(CloseOverlay) { text = "✕" };
            closeBtn.AddToClassList("settings-close-btn");
            closeBtn.focusable = true;
            header.Add(closeBtn);

            return header;
        }

        /// <summary>Same CSS-transition-driven slide as SettingsPanel — see its OnOpenAnimatedAsync.</summary>
        protected override async UniTask OnOpenAnimatedAsync(CancellationToken cancellationToken)
        {
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate, cancellationToken);

            _backdrop?.AddToClassList("settings-backdrop--open");
            _panel?.AddToClassList("settings-panel--open");

            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: cancellationToken);
        }

        protected override async UniTask OnCloseAnimatedAsync(CancellationToken cancellationToken)
        {
            _backdrop?.RemoveFromClassList("settings-backdrop--open");
            _panel?.RemoveFromClassList("settings-panel--open");

            await UniTask.Delay(350, ignoreTimeScale: true, cancellationToken: cancellationToken);
        }

        private static Button CreateMenuButton(string text, LucideIconName icon, System.Action onClick)
        {
            var btn = new Button(onClick).Classes("menu-button");
            btn.Add(new LucideIcon(icon).Classes("menu-button__icon"));
            btn.Add(new Label(text));
            return btn;
        }

        private void OnLocalClicked()
        {
            CloseOverlay();
            StartSessionThenLoad(SessionMode.SplitScreen, _localSceneLoader).Forget();
        }

        private void OnOnlineClicked()
        {
            CloseOverlay();

            // Online doesn't set SessionMode here — the lobby flow doesn't know yet whether
            // this peer ends up hosting or joining. ISessionService.StartSession(Online) is
            // called once networking actually starts (NetCoreConnectionService.RetryConnection,
            // wired to LobbyManager.OnAllReady), not at this button press.
            if (_onlineSceneLoader != null)
                _onlineSceneLoader.LoadScene();
            else
                Debug.LogError("[MultiplayerOverlay] _onlineSceneLoader not assigned.", this);
        }

        private async UniTask StartSessionThenLoad(SessionMode mode, LoadSceneByButton loader)
        {
            if (_session != null)
                await _session.StartSession(mode);

            if (loader != null)
                loader.LoadScene();
            else
                Debug.LogError("[MultiplayerOverlay] scene loader not assigned.", this);
        }
    }
}

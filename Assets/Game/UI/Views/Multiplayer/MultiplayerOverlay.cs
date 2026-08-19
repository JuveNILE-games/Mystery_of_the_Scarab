using Core.Systems.Navigation.Definitions;
using Core.Systems.SceneManagement.Components;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Cysharp.Threading.Tasks;
using FluentUI;
using NetCore.Interfaces;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views.Multiplayer
{
    /// <summary>
    /// Local vs Online choice, layered as a modal overlay over ModeSelectScreen (per design:
    /// this is a choice, not a navigation step). Local reuses the same local scene load as
    /// Single Player (no PurrNet involved) — only the session mode differs. Online hands off
    /// to the lobby flow (Lobby.unity); actual networking starts later, once all lobby
    /// players are ready (see NetCoreConnectionService.RetryConnection).
    /// </summary>
    public class MultiplayerOverlay : MonoBehaviour
    {
        [Inject] private ISessionService _session;

        [SerializeField] private UIDocument document;
        [SerializeField] private OverlayDefinition _definition;
        [SerializeField] private LoadSceneByButton _localSceneLoader;   // targets SampleScene.unity
        [SerializeField] private LoadSceneByButton _onlineSceneLoader;  // targets Lobby.unity

        // Unlike ScreenCanvas, OverlayCanvas has no onOpen/onClose UnityEvents — it fires
        // plain C# events on the OverlayDefinition asset instead. Subscribe directly rather
        // than relying on Inspector wiring that doesn't exist for overlays.
        private void Awake()
        {
            if (_definition != null)
            {
                _definition.OnOpen += OnOpen;
                _definition.OnClose += OnClose;
            }
        }

        private void OnDestroy()
        {
            if (_definition != null)
            {
                _definition.OnOpen -= OnOpen;
                _definition.OnClose -= OnClose;
            }
        }

        public void OnOpen()
        {
            var root = document.rootVisualElement;
            root.Clear();

            var column = Layout.Column("MultiplayerOverlay").Classes("menu-container").Grow();
            column.Add(CreateButton("Local", OnLocalClicked));
            column.Add(CreateButton("Online", OnOnlineClicked));
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

        private void OnLocalClicked()
        {
            StartSessionThenLoad(SessionMode.SplitScreen, _localSceneLoader).Forget();
        }

        private void OnOnlineClicked()
        {
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

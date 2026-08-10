using System;
using System.Collections.Generic;
using Core.Systems.AudioSystem;
using Core.Systems.Localization;
using Core.Systems.Localization.Definitions;
using Core.Systems.Localization.Interfaces;
using Core.Systems.Navigation;
using Core.Systems.Navigation.Definitions;
using Core.Systems.PopUp;
using Core.Systems.Theming;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Core.UI;
using Core.Utility.FluentUI.Icons.Lucide;
using FluentUI;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;

namespace Game.UI.Views.MainMenu{
    public class MainMenu : MonoBehaviour, ILocalizationListener
    {
        [Inject] private IThemeService _themeService;
        [Inject] private AudioService _audioService;
        [Inject] private LocalizationService _localizationService;
        [SerializeField] private SoundData themeMusic;
        [SerializeField] private UIDocument document;
        [SerializeField] private OverlayDefinition _settingsOverlay;
        //[SerializeField] private ThemeConfig theme; // Deprecated
        
        private SoundEmitter _activeTheme;
        
        [Serializable]
        public struct MainMenuItem
        {
            public LocalizedString Text;
            public LucideIconName Icon;
            public UnityEvent OnClick;
        }

        [Header("Menu Configuration")]
        [SerializeField] private List<MainMenuItem> menuItems;

        // Tracks each button's Label alongside the LocalizedString that drives it, so
        // OnLanguageChanged can refresh text in place instead of rebuilding the whole menu
        // (which would restart the fade-in animation and the theme music).
        private readonly List<(Label label, LocalizedString text)> _localizedLabels = new();
        private bool _isRegisteredForLocalization = false;

        public void OnOpen(){
            var root = document.rootVisualElement;
            root.Clear();
            _localizedLabels.Clear();

            if (!_isRegisteredForLocalization && _localizationService != null)
            {
                _localizationService.RegisterListener(this);
                _isRegisteredForLocalization = true;
            }

            root.RegisterCallback<NavigationMoveEvent>(evt => {
                Debug.Log($"[MainMenu] NavigationMove fired | Direction: {evt.direction} | Target: {(evt.target as VisualElement)?.name}");
            }, TrickleDown.TrickleDown); // TrickleDown catches it before anything swallows it
            
            // Apply theme via service
            if (_themeService != null)
            {
                _themeService.ApplyTheme(root);
            }
            else
            {
                Debug.LogWarning("[MainMenu] ThemeService not injected!");
            }

            // Build UI with new simplified Layout syntax
            var column = Layout.Column("MainMenu")
                .Classes("menu-container")
                .Grow()
                .Opacity(0); // Start invisible for fade-in

            // Create buttons dynamically
            for (int i = 0; i < menuItems.Count; i++)
            {
                var item = menuItems[i];
                // Select the first item by default
                bool isSelected = (i == 0);
                Button btn = CreateMenuButton(item.Text, item.Icon, isSelected, () => item.OnClick?.Invoke());
                btn.name = $"MenuButton_{i}_{item.Text?.Key ?? "Unassigned"}"; // Assign name for debug
                column.Add(btn);

                // Button is already focusable by default — these are not needed
                btn.focusable = true;
                // btn.tabIndex = i; // Interferes with directional (D-pad/arrow) navigation

                if (isSelected)
                {
                    // One-shot callback: focus once layout is ready, then unregister
                    // so future geometry changes on this button don't steal focus back
                    EventCallback<GeometryChangedEvent> onGeometry = null;
                    onGeometry = evt =>
                    {
                        if (btn.layout.width > 0 && btn.layout.height > 0)
                        {
                            btn.UnregisterCallback<GeometryChangedEvent>(onGeometry);
                            btn.Focus();
                            Debug.Log($"[MainMenu] Focused button after layout. Rect: {btn.worldBound}");
                        }
                    };
                    btn.RegisterCallback<GeometryChangedEvent>(onGeometry);
                }
            }

            root.Add(column);
            
            // Fade in animation
            root.Q(className: "menu-container").style.opacity = 1f;
            
            _activeTheme = _audioService.CreateSound()
                .WithSoundData(themeMusic)
                .Play();
        }

        private Button CreateMenuButton(LocalizedString text, LucideIconName icon, bool isSelected, System.Action onClick)
        {
            var btn = new Button(onClick)
                .Classes("menu-button");

            // if (isSelected)
            // {
            //    btn.AddToClassList("menu-button--selected");
            // }

            // Icon
            btn.Add(
                new LucideIcon(icon)
                    .Classes("menu-button__icon")
            );

            // Text
            string displayContent = text != null ? text.GetLocalizedValue() : "[UNASSIGNED]";
            var label = new Label(displayContent);
            btn.Add(label);

            if (text != null)
            {
                _localizedLabels.Add((label, text));
            }

            return btn;
        }

        /// <summary>
        /// ILocalizationListener - refreshes button labels in place when the language changes,
        /// so a language switch while the menu is open (or already applied since it last opened)
        /// is actually reflected instead of staying baked to whatever was current at OnOpen().
        /// </summary>
        public void OnLanguageChanged()
        {
            foreach (var (label, text) in _localizedLabels)
            {
                label.text = text.GetLocalizedValue();
            }
        }

        public void OnClose(){
            if (_isRegisteredForLocalization && _localizationService != null)
            {
                _localizationService.UnregisterListener(this);
                _isRegisteredForLocalization = false;
            }
            _localizedLabels.Clear();

            // Clear the UI to clean up elements and listeners
            document.rootVisualElement.Clear();

            if (_activeTheme != null)
            {
                _activeTheme.Stop();
                _activeTheme = null;
            }
        }

        public void Settings(){
            Navigation.ShowOverlay(_settingsOverlay);
        }

        public void Quit(){
            Popup.ShowConfirmPopup(
                message: "Are you sure you want to quit?",
                onYes: Application.Quit,
                onNo: () => {},
                title: "Quit Game"
            );
        }
    }
}
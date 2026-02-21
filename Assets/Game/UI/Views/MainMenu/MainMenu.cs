using System;
using System.Collections.Generic;
using Core.Systems.AudioSystem;
using Core.Systems.Localization.Definitions;
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
    public class MainMenu : MonoBehaviour
    {
        [Inject] private IThemeService _themeService;
        [Inject] private AudioService _audioService;
        [SerializeField] private SoundData themeMusic;
        [SerializeField] private UIDocument document;
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
        
        public void OnOpen(){
            var root = document.rootVisualElement;
            root.Clear();
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
            btn.Add(
                new Label(displayContent)
            );

            return btn;
        }

        public void OnClose(){
            // Clear the UI to clean up elements and listeners
            document.rootVisualElement.Clear();

            if (_activeTheme != null)
            {
                _activeTheme.Stop();
                _activeTheme = null;
            }
        }

        public void Settings(){
            throw new Exception("Testing exception for ErrorHandler");
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
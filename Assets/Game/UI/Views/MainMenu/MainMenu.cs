using Core.Systems.AudioSystem;
using Core.Systems.Localization.Definitions;
using Core.Systems.PopUp;
using Core.Utility.Attributes;
using Core.Utility.FluentUI;
using Core.UI;
using Core.Utility.FluentUI.Icons.Lucide;
using FluentUI;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI.Views.MainMenu{
    public class MainMenu : MonoBehaviour
    {
        [Inject] private AudioService _audioService;
        [SerializeField] private SoundData themeMusic;
        [SerializeField] private UIDocument document;
        [SerializeField] private ThemeConfig theme;
        
        [Header("Localization")]
        [SerializeField] private LocalizedString startButtonText;
        [SerializeField] private LocalizedString optionsButtonText;
        [SerializeField] private LocalizedString creditsButtonText;
        [SerializeField] private LocalizedString quitButtonText;
        
        public void OnOpen(){
            var root = document.rootVisualElement;
            root.Clear();
            
            // Apply theme
            theme?.ApplyTo(root);

            // Load game-specific stylesheet
            // Assuming stylesheet is in Resources or we load it manually. 
            // For now, let's assume it's linked via the Theme or we add it if possible.
            // Since we can't easily load asset by path without Resources, we'll assume the user
            // will assign it to the Theme or we rely on the class names being there.
            // PROACTIVE: Let's try to load it if it's in a standard location or assume Theme handles it.
            // As a fallback, we can inject styles directly or rely on the ThemeConfig having it.
            // Note: The user requested creating MainMenu.uss, but linking it in script usually requires it being referenced.
            // I will delegate this to the ThemeConfig or manual assignment in Editor for now to avoid compilation errors with AssetDatabase in runtime code.
            
            // Build UI with new simplified Layout syntax
            root.Add(
                Layout.Column(
                    // Creative Enhancement: Diamond/Scarab Icon for Start (Selected)
                    // We simulate "Selected" state on the first item for now
                    "MainMenu", 
                    CreateMenuButton(startButtonText, LucideIconName.Diamond, true, PopupRegistrar.TestPopups),
                    CreateMenuButton(optionsButtonText, LucideIconName.Settings, false, () => Debug.Log("Options clicked")),
                    CreateMenuButton(creditsButtonText, LucideIconName.Scroll, false, () => Debug.Log("Credits clicked")),
                    CreateMenuButton(quitButtonText, LucideIconName.LogOut, false, () => Application.Quit())
                )
                .Classes("menu-container") // defined in MainMenu.uss
                .Grow()
                .Opacity(0) // Start invisible for fade-in
            );
            
            // Fade in animation
            root.Q(className: "menu-container").style.opacity = 1f;
            
            _audioService.CreateSound()
                .WithSoundData(themeMusic)
                .Play();
        }

        private VisualElement CreateMenuButton(LocalizedString text, LucideIconName icon, bool isSelected, System.Action onClick)
        {
            var btn = new Button(onClick)
                .Classes("menu-button");

            if (isSelected)
            {
                btn.AddToClassList("menu-button--selected");
            }

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
        }
    }
}
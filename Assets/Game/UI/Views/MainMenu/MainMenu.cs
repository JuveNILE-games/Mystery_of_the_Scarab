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
        
        [System.Serializable]
        public struct MainMenuItem
        {
            public LocalizedString Text;
            public LucideIconName Icon;
            public UnityEngine.Events.UnityEvent OnClick;
        }

        [Header("Menu Configuration")]
        [SerializeField] private System.Collections.Generic.List<MainMenuItem> menuItems;
        
        public void OnOpen(){
            var root = document.rootVisualElement;
            root.Clear();
            
            // Apply theme
            theme?.ApplyTo(root);

            // Build UI with new simplified Layout syntax
            var column = Layout.Column("MainMenu")
                .Classes("menu-container") // defined in MainMenu.uss
                .Grow()
                .Opacity(0); // Start invisible for fade-in

            // Create buttons dynamically
            for (int i = 0; i < menuItems.Count; i++)
            {
                var item = menuItems[i];
                // Select the first item by default
                bool isSelected = (i == 0);
                column.Add(CreateMenuButton(item.Text, item.Icon, isSelected, () => item.OnClick?.Invoke()));
            }

            root.Add(column);
            
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
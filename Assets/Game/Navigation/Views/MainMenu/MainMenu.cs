using Core.Systems.AudioSystem;
using Core.Systems.Localization.Definitions;
using Core.Systems.Navigation;
using Core.Systems.PopUp;
using Core.Utility.Attributes;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Navigation.Views.MainMenu{
    public class MainMenu : MonoBehaviour
    {
        [Inject] private AudioService _audioService;
        [SerializeField] private SoundData themeMusic;
        [SerializeField] private UIDocument document;
        [Header("Localization")]
        [SerializeField] private LocalizedString startButtonText;
        
        private Button startButton;
        public void OnOpen(){
            
            
            
            document.rootVisualElement.Q("container").style.opacity = 1f;
            _audioService.CreateSound()
                .WithSoundData(themeMusic)
                .Play();
            
            InitMenu(document.rootVisualElement);
        }

        private void InitMenu(VisualElement root){
            startButton = root.Q<Button>("StartButton");
            startButton.text = startButtonText.GetLocalizedValue();
            startButton.clicked += PopupRegistrar.TestPopups;
        }

        public void OnClose(){
            startButton.clicked -= PopupRegistrar.TestPopups;
        }
    }
}
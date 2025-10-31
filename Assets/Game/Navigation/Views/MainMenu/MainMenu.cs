using Core.Systems.AudioSystem;
using Core.Systems.Navigation;
using Core.Systems.PopUp;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Navigation.Views.MainMenu{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField] private SoundData themeMusic;
        [SerializeField] private UIDocument document;
        private Button startButton;
        public void OnOpen(){
            
            
            
            document.rootVisualElement.Q("container").style.opacity = 1f;
            SoundManager.Instance.CreateSound()
                .WithSoundData(themeMusic)
                .Play();
            
            InitMenu(document.rootVisualElement);
        }

        private void InitMenu(VisualElement root){
            startButton = root.Q<Button>("StartButton");
            startButton.clicked += PopupRegistrar.TestPopups;
        }

        public void OnClose(){
            startButton.clicked -= PopupRegistrar.TestPopups;
        }
    }
}
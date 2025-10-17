using Core.Systems.AudioSystem;
using Core.Systems.Navigation;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Navigation.Views.MainMenu{
    public class MainMenu : ScreenComponent
    {
        [SerializeField] private SoundData themeMusic;
        public override void OnOpen(){
            definition.document.rootVisualElement.Q("container").style.opacity = 1f;
            SoundManager.Instance.CreateSound()
                .WithSoundData(themeMusic)
                .Play();
            
            InitMenu(definition.document.rootVisualElement);
            
            base.OnOpen();
        }

        private void InitMenu(VisualElement root){
            Button startButton = root.Q<Button>("StartButton");
            startButton.clicked += () => {
                Debug.Log("Start");
            };
        }

        public override void OnClose(){
            base.OnClose();
        }
    }
}
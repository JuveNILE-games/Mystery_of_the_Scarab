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
            base.OnOpen();
        }
        public override void OnClose(){
            base.OnClose();
        }
    }
}
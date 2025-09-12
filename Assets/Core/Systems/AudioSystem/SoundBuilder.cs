using UnityEngine;

namespace Core.Systems.AudioSystem{
    public class SoundBuilder{
        private readonly SoundManager _soundManager;
        private SoundData _soundData;
        private Vector3 _clipPosition = Vector3.zero;
        private bool _randomizePitch;

        public SoundBuilder(SoundManager soundManager){
            _soundManager = soundManager;
        }

        public SoundBuilder WithSoundData(SoundData soundData){
            _soundData = soundData;
            return this;
        }

        public SoundBuilder WithClipPosition(Vector3 position){
            _clipPosition = position;
            return this;
        }

        public SoundBuilder WithRandomPitch(bool randomize){
            _randomizePitch = randomize;
            return this;
        }

        public void Play(){
            if (!_soundManager.CanPlaySound(_soundData)) return;
            SoundEmitter soundEmitter = _soundManager.Get();
            soundEmitter.Initialize(_soundData);
            soundEmitter.transform.position = _clipPosition;

            if (_randomizePitch)
            {
                soundEmitter.WithRandomPitch();
            }

            if (_soundData.isFrequentlyPlayed)
            {
                _soundManager.FrequentSoundEmitters.Enqueue(soundEmitter);
            }
            soundEmitter.Play();
        }
    }
}
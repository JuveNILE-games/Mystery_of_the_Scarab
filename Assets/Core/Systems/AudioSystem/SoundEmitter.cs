using System;
using System.Collections;
using UnityEngine;
using UnityUtils;
using Random = UnityEngine.Random;

namespace Core.Systems.AudioSystem{
    [RequireComponent(typeof(AudioSource))]
    public class SoundEmitter : MonoBehaviour{
        [SerializeField] private SoundData soundData;
        private AudioSource _audioSource;
        private Coroutine _playCoroutine;
        
        public SoundData Data { get; private set; }

        private void Awake(){
            _audioSource = gameObject.GetOrAdd<AudioSource>();
        }

        public void Play(){
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
            }
            _audioSource.Play();
            _playCoroutine = StartCoroutine(WaitForSoundToFinish());
        }

        public void Play(AudioSource audioSource){
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
            }
            audioSource.Play();
            _playCoroutine = StartCoroutine(WaitForSoundToFinish());
        }

        public void Stop(){
            if (_playCoroutine != null)
            {
                StopCoroutine(_playCoroutine);
                _playCoroutine = null;
            }
            _audioSource.Stop();
            SoundManager.Instance.ReturnToPool(this);
        }

        private IEnumerator WaitForSoundToFinish(){
            yield return new WaitWhile(() => _audioSource.isPlaying);
            SoundManager.Instance.ReturnToPool(this);
        }
        
        public void Initialize(SoundData data){
            Data = data;
            _audioSource.clip = data.clip;
            _audioSource.outputAudioMixerGroup = data.mixerGroup;
            _audioSource.loop = data.loop;
            _audioSource.playOnAwake = data.playOnAwake;

        }

        public void WithRandomPitch(float minShift = -0.05f, float maxShift = 0.05f){
            _audioSource.pitch += Random.Range(minShift, maxShift);
        }
    }
}
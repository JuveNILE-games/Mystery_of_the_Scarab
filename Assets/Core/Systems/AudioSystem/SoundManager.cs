using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Core.Systems.AudioSystem{
    [CreateAssetMenu(fileName = "SoundManager", menuName = "Core/Audio/SoundManager")]
    public class SoundManager : ScriptableObject{
        private static SoundManager _instance;

        public static SoundManager Instance
        {
            get
            {
                if (!_instance)
                {
                    _instance = Resources.Load<SoundManager>("AudioSystem/SoundManager");
                }

                return _instance;
            }
        }

        private ObjectPool<SoundEmitter> _soundEmitterPool;
        private readonly List<SoundEmitter> _activeSoundEmitters = new();
        public readonly Queue<SoundEmitter> FrequentSoundEmitters = new();
        
        [SerializeField] private SoundEmitter soundEmitterPrefab;
        [SerializeField] private bool collectionCheck = true;
        [SerializeField] private int defaultCapacity = 10;
        [SerializeField] private int maxPoolSize = 100;
        [SerializeField] private int maxSoundInstances = 30;

        private void OnEnable(){
            InitializePool();
        }
        
        public SoundBuilder CreateSound() => new SoundBuilder(this);
        
        public bool CanPlaySound(SoundData soundData){
            if (!soundData.isFrequentlyPlayed) return true;

            if (FrequentSoundEmitters.Count >= maxSoundInstances && FrequentSoundEmitters.TryDequeue(out var soundEmitter))
            {
                try
                {
                    soundEmitter.Stop();
                    return true;
                }
                catch {
                    Debug.LogError($"Failed to stop sound emitter: {soundEmitter.name}");
                    return false;
                }
            }

            return true;
        }

        public SoundEmitter Get(){
            return _soundEmitterPool.Get();
        }
        
        public void ReturnToPool(SoundEmitter soundEmitter){
            _soundEmitterPool.Release(soundEmitter);
        }
        
        private void OnDestroyPoolObject(SoundEmitter soundEmitter){
            Destroy(soundEmitter.gameObject);
        }
        
        private void OnReturnedToPool(SoundEmitter soundEmitter){
            soundEmitter.gameObject.SetActive(false);
            _activeSoundEmitters.Remove(soundEmitter);
        }
        private void OnTakeFromPool(SoundEmitter soundEmitter){
            soundEmitter.gameObject.SetActive(true);
            _activeSoundEmitters.Add(soundEmitter);
        }
        
        private SoundEmitter CreateSoundEmitter(){
            SoundEmitter soundEmitter = Instantiate(soundEmitterPrefab);
            soundEmitter.gameObject.SetActive(false);
            return soundEmitter;
        }

        private void InitializePool(){
            _soundEmitterPool = new ObjectPool<SoundEmitter>(
                CreateSoundEmitter,
                OnTakeFromPool,
                OnReturnedToPool,
                OnDestroyPoolObject,
                collectionCheck,
                defaultCapacity,
                maxPoolSize);
        }
    }
}
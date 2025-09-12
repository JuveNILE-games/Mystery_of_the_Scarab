using System;
using UnityEngine;
using UnityEngine.Audio;

namespace Core.Systems.AudioSystem{
    [Serializable]
    public class SoundData{
        public AudioClip clip;
        public AudioMixerGroup mixerGroup;
        public bool isFrequentlyPlayed;
        public bool loop;
        public bool playOnAwake;
    }
}
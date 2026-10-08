using System;
using Horcrux.Runtime.Abstractions.Audio;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    [Serializable]
    public class AudioEntry
    {
        [SerializeField] private string displayName;
        [SerializeField] private int id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0, 1)] private float volume;
        [SerializeField] private float minIntervalSeconds = 0.05f;
        
        public string DisplayName => displayName;
        public AudioId Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public float MinIntervalSeconds => minIntervalSeconds;
    } 
}
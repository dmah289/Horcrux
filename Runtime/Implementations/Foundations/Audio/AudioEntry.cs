using System;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    [Serializable]
    public class AudioEntry<TSfx> where TSfx : struct, Enum
    {
        [SerializeField] private TSfx id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0, 1)] private float volume = 1f;
        [SerializeField, Min(0f)] private float minIntervalSeconds = 0.05f;
        
        public TSfx Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
        public float MinIntervalSeconds => minIntervalSeconds;
    } 
}
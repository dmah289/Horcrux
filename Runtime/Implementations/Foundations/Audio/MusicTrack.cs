using System;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    [Serializable]
    public class MusicTrack<TMusic> where TMusic : struct, Enum
    {
        [SerializeField] private TMusic id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0, 1)] private float volume = 1f;
        
        public TMusic Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
    }
}
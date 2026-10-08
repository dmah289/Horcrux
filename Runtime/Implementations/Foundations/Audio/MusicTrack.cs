using System;
using Horcrux.Runtime.Abstractions.Audio;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    [Serializable]
    public class MusicTrack
    {
        [SerializeField] private string displayName;
        [SerializeField, Tooltip("Mapping AudioId Value")] private int id;
        [SerializeField] private AudioClip clip;
        [SerializeField, Range(0, 1)] private float volume;
        
        public string DisplayName => displayName;
        public AudioId Id => id;
        public AudioClip Clip => clip;
        public float Volume => volume;
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    [CreateAssetMenu(fileName = "AudioCatalog", menuName = "Horcrux/Audio Catalog")]
    public partial class AudioCatalog : ScriptableObject
    {
        [SerializeField] private AudioEntry[] entries =  Array.Empty<AudioEntry>();
        [SerializeField] private MusicTrack[] tracks = Array.Empty<MusicTrack>();
        
        public IReadOnlyList<AudioEntry> Entries => entries;
        public IReadOnlyList<MusicTrack> Tracks => tracks;
        
        
    }
}
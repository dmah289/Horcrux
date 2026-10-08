using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioCatalog<TSfx, TMusic> : ScriptableObject
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        [SerializeField] private AudioEntry<TSfx>[] entries =  Array.Empty<AudioEntry<TSfx>>();
        [SerializeField] private MusicTrack<TMusic>[] tracks = Array.Empty<MusicTrack<TMusic>>();
        
        public IReadOnlyList<AudioEntry<TSfx>> Entries => entries;
        public IReadOnlyList<MusicTrack<TMusic>> Tracks => tracks;
        
        
    }
}
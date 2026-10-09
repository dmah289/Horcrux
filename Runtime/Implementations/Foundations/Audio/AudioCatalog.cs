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

        private readonly Dictionary<TSfx, AudioEntry<TSfx>> _entryById = new();
        private readonly Dictionary<TMusic, MusicTrack<TMusic>> _musicById = new();
        
        public IReadOnlyList<AudioEntry<TSfx>> Entries => entries;


        #region API
        
        public bool TryGetEntryById(TSfx id, out AudioEntry<TSfx> entry)
            => _entryById.TryGetValue(id, out entry);
        
        public bool TryGetMusicById(TMusic music, out MusicTrack<TMusic> track)
            => _musicById.TryGetValue(music, out track);

        public void BuildTables()
        {
            _entryById.Clear();
            _musicById.Clear();
            
            for(int i = 0; i < entries.Length; i++)
                _entryById.Add(entries[i].Id, entries[i]);
            
            for(int i = 0; i < tracks.Length; i++)
                _musicById.Add(tracks[i].Id, tracks[i]);
        }

        #endregion
    }
}
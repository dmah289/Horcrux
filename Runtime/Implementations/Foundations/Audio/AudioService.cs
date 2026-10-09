using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Utilities;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioService<TSfx, TMusic> : MonoBehaviour, IAudioService<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        private const float PitchSpread = 0.05f;
        
        [Splitter("References")]
        [SerializeField] private AudioSource[] voices = Array.Empty<AudioSource>();
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioCatalog<TSfx, TMusic> catalog;
        
        private bool _isMusicOn = true;
        private bool _isSfxOn = true;
        private MusicTrack<TMusic> _currMusicTrack;
        
        private readonly Dictionary<TSfx, float> _lastPlayTimeById = new();
        private float[] _voicesBusyUntil = Array.Empty<float>();
        
        
        #region Properties

        public bool IsSfxOn
        {
            get => _isSfxOn;
            set
            {
                _isSfxOn = value;

                if (!_isSfxOn)
                    StopAllVoices();
            }
        }

        public bool IsMusicOn
        {
            get =>  _isMusicOn;
            set
            {
                _isMusicOn = value;
                ApplyMusicState();
            }
        }
        
        #endregion

        #region Unity Callbacks

        protected virtual void Awake()
        {
            catalog.BuildTables();
            
            _lastPlayTimeById.Clear();
            for (int i = 0; i < catalog.Entries.Count; i++)
                _lastPlayTimeById[catalog.Entries[i].Id] = float.NegativeInfinity;
            
            _voicesBusyUntil = new float[voices.Length];
        }

        #endregion

        #region API
        
        public void PlaySfx(TSfx sfx)
        {
            if (!_isSfxOn)
                return;

            if (!catalog.TryGetEntryById(sfx, out AudioEntry<TSfx> entry))
            {
                Debug.LogError($"[AudioService]: Sfx {sfx} is not in the catalog.", this);
                return;
            }
            
            float now = UnityEngine.Time.unscaledTime;
            if (now - _lastPlayTimeById[sfx] < entry.MinIntervalSeconds)
                return;
            
            AudioClip clip = entry.Clip;
            float pitch = Random.Range(1f - PitchSpread, 1 + PitchSpread);
            int voiceIndex = RentVoice(now);
            AudioSource voice = voices[voiceIndex];
            
            voice.clip = clip;
            voice.pitch = pitch;
            voice.volume = entry.Volume;
            voice.Play();

            _voicesBusyUntil[voiceIndex] = now + clip.length / pitch;
            _lastPlayTimeById[sfx] = now;
        }

        public void PlayMusic(TMusic music)
        {
            if(!catalog.TryGetMusicById(music, out MusicTrack<TMusic> track))
            {
                Debug.LogError($"[AudioService]: Music {music} is not in the catalog.", this);
                return;
            }
            
            _currMusicTrack = track;
            ApplyMusicState();
        }

        public void StopMusic()
        {
            _currMusicTrack = null;
            ApplyMusicState();
        }
        
        #endregion

        #region Class Methods

        private void StopAllVoices()
        {
            if (voices == null || voices.Length == 0 || _voicesBusyUntil == null || _voicesBusyUntil.Length == 0)
                return;
            
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].Stop();
                _voicesBusyUntil[i] = 0f;
            }
        }

        private void ApplyMusicState()
        {
            if (!_isMusicOn || _currMusicTrack == null)
            {
                musicSource.Stop();
                return;
            }

            if (musicSource.isPlaying && musicSource.clip == _currMusicTrack.Clip)
                return;
            
            musicSource.volume = _currMusicTrack.Volume;
            musicSource.clip = _currMusicTrack.Clip;
            musicSource.Play();
        }

        private int RentVoice(float now)
        {
            int soonestIndex = 0;
            float soonestBusyUntil = float.MaxValue;

            for (int i = 0; i < voices.Length; i++)
            {
                if (now >= _voicesBusyUntil[i] && !voices[i].isPlaying)
                    return i;

                if (_voicesBusyUntil[i] < soonestBusyUntil)
                {
                    soonestBusyUntil = _voicesBusyUntil[i];
                    soonestIndex = i;
                }
            }

            return soonestIndex;
        }

        #endregion
    }
}
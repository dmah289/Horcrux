using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioCatalog<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        #if UNITY_EDITOR

        private int LogInvalidEntries()
        {
            int invalidAmount = 0;

            for (int i = 0; i < entries.Length; i++)
            {
                if (!IsEntryValid(i))
                    invalidAmount++;
            }

            for (int i = 0; i < tracks.Length; i++)
            {
                if (!IsMusicTrackValid(i))
                    invalidAmount++;
            }

            return invalidAmount;
        }

        private bool IsMusicTrackValid(int index)
        {
            MusicTrack<TMusic> track = tracks[index];
            string label = $"Music track #{index} ({track.Id})";

            if (EqualityComparer<TMusic>.Default.Equals(track.Id, default))
            {
                Debug.LogError($"[AudioCatalog]: {label} has no Id (unassigned).", this);
                return false;
            }

            if (!Enum.IsDefined(typeof(TMusic), track.Id))
            {
                Debug.LogError($"[AudioCatalog]: {label} is no member of {typeof(TMusic).Name}.", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (EqualityComparer<TMusic>.Default.Equals(tracks[j].Id, track.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {track.Id} of Music track #{j}. Only the first one plays.", this);
                    return false;
                }
            }

            if (track.Clip == null)
            {
                Debug.LogError($"[AudioCatalog]: {label} has no clip.", this);
                return false;
            }

            return true;
        }

        private bool IsEntryValid(int index)
        {
            AudioEntry<TSfx> entry = entries[index];
            string label = $"Sfx entry #{index} ({entry.Id})";

            if (EqualityComparer<TSfx>.Default.Equals(entry.Id, default))
            {
                Debug.LogError($"[AudioCatalog]: {label} has no Id (unassigned).", this);
                return false;
            }

            if (!Enum.IsDefined(typeof(TSfx), entry.Id))
            {
                Debug.LogError($"[AudioCatalog]: {label} is no member of {typeof(TSfx).Name}.", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (EqualityComparer<TSfx>.Default.Equals(entries[j].Id, entry.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {entry.Id} of Sfx entry #{j}. Only the first one plays.", this);
                    return false;
                }
            }

            if (entry.Clip == null)
            {
                Debug.LogError($"[AudioCatalog]: {label} has no clip.", this);
                return false;
            }

            return true;
        }
        
        [Button]
        private void ValidateEntries()
        {
            int invalidAmount = LogInvalidEntries();

            if (invalidAmount == 0)
                Debug.Log($"[AudioCatalog]: {entries.Length} Sfx entries, {tracks.Length} Music tracks — ids unique and assigned, every slot has a clip.", this);
        }

        #endif
    }
}
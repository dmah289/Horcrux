using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public partial class AudioCatalog
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
            MusicTrack track = tracks[index];
            string label = $"music track #{index} '{track.DisplayName}'";

            if (!track.Id.IsValid)
            {
                Debug.LogError($"[AudioCatalog]: {label} has Id 0 (unassigned).", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (tracks[j].Id.Equals(track.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {track.Id} of music track #{j}. Only the first one plays.", this);
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
            AudioEntry entry = entries[index];
            string label = $"entry #{index} '{entry.DisplayName}'";

            if (!entry.Id.IsValid)
            {
                Debug.LogError($"[AudioCatalog]: {label} has Id 0 (unassigned).", this);
                return false;
            }

            for (int j = 0; j < index; j++)
            {
                if (entries[j].Id.Equals(entry.Id))
                {
                    Debug.LogError($"[AudioCatalog]: {label} repeats Id {entry.Id} of entry #{j}. Only the first one plays.", this);
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
                Debug.Log($"[AudioCatalog]: {entries.Length} entries, {tracks.Length} music tracks — ids unique and assigned, every slot has a clip.", this);
        }

        #endif
    }
}
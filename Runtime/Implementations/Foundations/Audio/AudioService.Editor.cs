using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Audio
{
    public abstract partial class AudioService<TSfx, TMusic>
        where TSfx : struct, Enum
        where TMusic : struct, Enum
    {
        #if UNITY_EDITOR

        private const int DefaultVoiceAmount = 8;

        [Button]
        protected virtual void ConfigureAllSources()
        {
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null)
                    ConfigureVoice(voices[i]);
            }

            if (musicSource != null)
            {
                ConfigureVoice(musicSource);
                musicSource.loop = true;
            }
        }

        [Button]
        private void ValidateReferences()
        {
            int invalidAmount = 0;

            if (catalog == null)
            {
                Debug.LogError("[AudioService]: Catalog is not assigned.", this);
                invalidAmount++;
            }

            if (musicSource == null)
            {
                Debug.LogError("[AudioService]: Music Source is not assigned.", this);
                invalidAmount++;
            }

            if (voices.Length == 0)
            {
                Debug.LogError("[AudioService]: No Sfx voice assigned — use 'Setup Audio Sources' on the component.", this);
                invalidAmount++;
            }

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null)
                {
                    Debug.LogError($"[AudioService]: Sfx Voices[{i}] is empty.", this);
                    invalidAmount++;
                }
            }

            if (invalidAmount == 0)
                Debug.Log($"[AudioService]: Catalog, Music Source and {voices.Length} Sfx voices assigned.", this);
        }

        [Button]
        private void SetupAudioSources()
        {
            UnityEditor.Undo.RecordObject(this, "Setup Audio Sources");

            if (voices.Length < DefaultVoiceAmount)
                Array.Resize(ref voices, DefaultVoiceAmount);

            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] == null)
                    voices[i] = CreateAudioSource($"sfx_voice_{i}");
            }

            if (musicSource == null)
                musicSource = CreateAudioSource("music_source");

            ConfigureAllSources();
            UnityEditor.EditorUtility.SetDirty(this);
        }

        private AudioSource CreateAudioSource(string objectName)
        {
            var child = new GameObject(objectName);
            UnityEditor.Undo.RegisterCreatedObjectUndo(child, "Setup Audio Sources");
            child.transform.SetParent(transform, false);

            return child.AddComponent<AudioSource>();
        }

        private static void ConfigureVoice(AudioSource voice)
        {
            voice.playOnAwake = false;
            voice.loop = false;
            voice.spatialBlend = 0f;
        }

        #endif
    }
}

using System;
using Sirenix.OdinInspector;
using Newtonsoft.Json;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    public partial class SaveEntry<T>
    {
#if UNITY_EDITOR
        [NonSerialized, ShowInInspector, MultiLineProperty]
        private string payloadToImport;

        [Button]
        private void ImportPayload()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[SaveCollection]: Live value must be imported in Play Mode.");
                return;
            }

            if (string.IsNullOrEmpty(payloadToImport))
            {
                Debug.LogWarning($"[SaveCollection]: Import to {key} skipped because payload is empty.");
                return;
            }

            try
            {
                Value = JsonConvert.DeserializeObject<T>(payloadToImport);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveCollection]: Import into {key} failed.");
                Debug.LogException(e);
            }
        }
#endif
    }
}
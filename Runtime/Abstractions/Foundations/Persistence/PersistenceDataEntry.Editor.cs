using System;
using Sirenix.OdinInspector;
using Newtonsoft.Json;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    public partial class PersistenceDataEntry<T>
    {
#if UNITY_EDITOR
        [ShowInInspector, MultiLineProperty]
        private string payloadToImport;

        [Button]
        private void ImportPayload()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("[PersistenceDataCollection]: Live value must be imported in Play Mode.");
                return;
            }

            if (string.IsNullOrEmpty(payloadToImport))
            {
                Debug.LogWarning($"[PersistenceDataCollection]: Import to {key} skipped because payload is empty.");
                return;
            }

            try
            {
                Value = JsonConvert.DeserializeObject<T>(payloadToImport);
            }
            catch (Exception e)
            {
                Debug.LogError($"[PersistenceDataCollection]: Import into {key} failed.");
                Debug.LogException(e);
            }
        }
#endif
    }
}
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    public abstract partial class BaseSaveCollection
    {
#if UNITY_EDITOR
        [Button]
        public void ValidateKeys()
        {
            List<ISaveEntry> scratch = new();
            int rejected = ScanEntries(scratch);
            
            if (rejected == 0)
                Debug.Log($"[SaveCollection]: {scratch.Count} entries — no duplicate key, no empty key, no unassigned field.", this);
            else
                Debug.LogError($"[SaveCollection]: {rejected}/{scratch.Count + rejected} declared entries were rejected.", this);
        }
#endif
    }
}
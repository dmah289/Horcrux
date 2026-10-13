using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Pooling;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Pooling
{
    public abstract partial class PoolService<TKey> : MonoBehaviour, IPoolService<TKey> where TKey : struct, Enum
    {
        #if UNITY_EDITOR
        
        [Button]
        private void ValidateConfigs()
        {
            for (int i = 0; i < poolConfigs.Length; i++)
            {
                PoolConfig<TKey> config = poolConfigs[i];

                if (config.prefab == null || !config.prefab.RuntimeKeyIsValid())
                    Debug.LogError($"[PoolService]: poolConfigs[{i}] '{config.key}' has not assigned prefab yet.", this);

                for (int j = 0; j < i; j++)
                {
                    if(EqualityComparer<TKey>.Default.Equals(poolConfigs[j].key, config.key))
                        Debug.LogError($"[PoolService]: poolConfigs[{j}] and [{i}] have the same key '{config.key}'.", this);
                }
            }
        }
        
        #endif
    }
}
using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Serialization;

namespace Horcrux.Runtime.Implementations.Pooling
{
    [Serializable]
    public class PoolConfig<TKey> where TKey : struct, Enum
    {
        public TKey key;
        public AssetReference prefab;
        [Min(0)] public int initialPoolSize = 3;
        [Min(1)] public int maxPoolSize = 5;
    }
}

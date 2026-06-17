using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Horcrux.Runtime.Implementations.Pooling
{
    [Serializable]
    public struct PoolConfig
    {
        public AssetReference prefab;
        public bool lazyInit;
        [Min(1)] public int initialPoolSize;
        [Min(2)] public int maxPoolSize;
    }
}

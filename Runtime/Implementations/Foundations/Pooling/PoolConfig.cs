using System;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Horcrux.Runtime.Implementations.Pooling
{
    [Serializable]
    public class PoolConfig
    {
        public AssetReference prefab;
        public int initialPoolSize;
        public int maxPoolSize;
    }
}

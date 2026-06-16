using System;
using UnityEngine.AddressableAssets;

namespace Horcrux.Runtime.Implementations.Pooling
{
    public enum PoolType
    {
        
    }
    
    [Serializable]
    public struct PoolPrefabConfig
    {
        public PoolType PoolType;
        public AssetReference Prefab;
    }
}
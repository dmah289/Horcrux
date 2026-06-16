using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Pooling;
using Sisus.Init;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Horcrux.Runtime.Implementations.Pooling
{
    [Service(typeof(IPoolManager))]
    public class PoolManager : MonoBehaviour, IPoolManager
    {
        [SerializeField] private PoolPrefabConfig[] poolingPrefab;
        private Dictionary<PoolType, List<IPoolable>> pools;
    }
}
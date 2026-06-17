using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Pooling;
using Sisus.Init;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Horcrux.Runtime.Implementations.Pooling
{
    [Service(typeof(IPoolManager))]
    public class PoolManager : MonoBehaviour, IPoolManager
    {
        [SerializeField] private PoolConfig[] poolConfigs;

        private readonly Dictionary<Type, PoolEntry> _pools = new();
        private readonly HashSet<int> _loadedConfigIndices = new();

        #region Initialization
        public async UniTask Initialize(CancellationToken cancellationToken)
        {
            for (int i = 0; i < poolConfigs.Length; i++)
            {
                if (poolConfigs[i].lazyInit || !poolConfigs[i].prefab.RuntimeKeyIsValid())
                    continue;

                await LoadAndCreatePool(i, cancellationToken);
            }
        }

        private async UniTask<PoolEntry> LoadAndCreatePool(int configIndex, CancellationToken cancellationToken)
        {
            PoolConfig config = poolConfigs[configIndex];
            AsyncOperationHandle<GameObject> handle = config.prefab.LoadAssetAsync<GameObject>();
            GameObject prefab = await handle.ToUniTask(cancellationToken: cancellationToken);

            Component poolable = prefab.GetComponent<IPoolable>() as Component;
            if (poolable == null)
            {
                Debug.LogError($"[PoolManager] Prefab '{prefab.name}' does not have an IPoolable component. Releasing.");
                Addressables.Release(handle);
                return null;
            }

            Type type = poolable.GetType();
            if (_pools.ContainsKey(type))
            {
                Debug.LogWarning($"[PoolManager] Pool for type '{type.Name}' already exists. Skipping duplicate config.");
                Addressables.Release(handle);
                return _pools[type];
            }

            var entry = new PoolEntry(handle, poolable, config.maxPoolSize, config.initialPoolSize);
            _pools[type] = entry;
            _loadedConfigIndices.Add(configIndex);

            // Pre-warm
            for (int i = 0; i < config.initialPoolSize; i++)
            {
                Component instance = Instantiate(poolable, transform);
                instance.gameObject.SetActive(false);
                entry.Inactive.Push(instance);
            }

            return entry;
        }

        private async UniTask<PoolEntry> LazyLoad<T>(CancellationToken cancellationToken) where T : Component, IPoolable
        {
            for (int i = 0; i < poolConfigs.Length; i++)
            {
                if (_loadedConfigIndices.Contains(i))
                    continue;

                if (!poolConfigs[i].prefab.RuntimeKeyIsValid())
                    continue;

                PoolEntry entry = await LoadAndCreatePool(i, cancellationToken);
                if (entry != null && entry.Prefab is T)
                    return entry;
            }

            return null;
        }
        #endregion

        #region Unity Callbacks
        private void OnDestroy()
        {
            Dispose();
        }
        #endregion

        #region Methods
        public async UniTask<T> Get<T>(Transform parent = null, CancellationToken cancellationToken = default) where T : Component, IPoolable
        {
            Type type = typeof(T);
            if (!_pools.TryGetValue(type, out PoolEntry entry))
            {
                entry = await LazyLoad<T>(cancellationToken);
                if (entry == null)
                    throw new InvalidOperationException($"[PoolManager] No PoolConfig found for type '{type.Name}'.");
            }

            T instance = null;

            while (entry.Inactive.Count > 0)
            {
                instance = entry.Inactive.Pop() as T;
                if (instance != null) break;
            }

            if (instance == null)
                instance = Instantiate(entry.Prefab) as T;

            instance.transform.SetParent(parent);
            instance.gameObject.SetActive(true);
            instance.OnGetFromPool();

            return instance;
        }

        public void Return<T>(T instance) where T : Component, IPoolable
        {
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));

            Type type = instance.GetType();
            if (!_pools.TryGetValue(type, out PoolEntry entry))
            {
                Debug.LogWarning($"[PoolManager] No pool for type '{type.Name}'. Destroying instance.");
                Destroy(instance.gameObject);
                return;
            }

            if (!instance.gameObject.activeSelf)
            {
                Debug.LogWarning($"[PoolManager] '{type.Name}' is already inactive — possible double Return. Ignoring.");
                return;
            }

            instance.OnReturnToPool();
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(transform);

            if (entry.Inactive.Count >= entry.MaxPoolSize)
            {
                Destroy(instance.gameObject);
                return;
            }

            entry.Inactive.Push(instance);
        }

        public void Dispose()
        {
            if (_pools.Count == 0)
                return;

            foreach (PoolEntry entry in _pools.Values)
                CleanupEntry(entry);

            _pools.Clear();
            _loadedConfigIndices.Clear();
        }

        private void CleanupEntry(PoolEntry entry)
        {
            while (entry.Inactive.Count > 0)
            {
                Component instance = entry.Inactive.Pop();
                if (instance != null)
                    Destroy(instance.gameObject);
            }

            if (entry.Handle.IsValid())
                Addressables.Release(entry.Handle);
        }
        #endregion

        private class PoolEntry
        {
            public readonly AsyncOperationHandle<GameObject> Handle;
            public readonly Component Prefab;
            public readonly int MaxPoolSize;
            public readonly Stack<Component> Inactive;

            public PoolEntry(AsyncOperationHandle<GameObject> handle, Component prefab, int maxPoolSize, int initialCapacity)
            {
                Handle = handle;
                Prefab = prefab;
                MaxPoolSize = maxPoolSize;
                Inactive = new Stack<Component>(initialCapacity);
            }
        }
    }
}

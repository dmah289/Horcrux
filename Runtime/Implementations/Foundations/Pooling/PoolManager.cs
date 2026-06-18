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
    [Service(typeof(IPoolManager), FindFromScene = true)]
    public class PoolManager : MonoBehaviour, IPoolManager
    {
        [SerializeField] private PoolConfig[] poolConfigs;

        private readonly Dictionary<Type, PoolEntry> _pools = new();

        #region Unity Callbacks
        private void Awake()
        {
            DontDestroyOnLoad(this);
        }

        private void OnDestroy()
        {
            Dispose();
        }
        #endregion

        #region Methods
        public async UniTask Initialize(CancellationToken cancellationToken)
        {
            _pools.Clear();
            for (int i = 0; i < poolConfigs.Length; i++)
            {
                if (!poolConfigs[i].prefab.RuntimeKeyIsValid())
                    continue;

                await LoadAndCreatePool(poolConfigs[i], cancellationToken);
            }
        }

        private async UniTask LoadAndCreatePool(PoolConfig config, CancellationToken cancellationToken)
        {
            AsyncOperationHandle<GameObject> handle = config.prefab.LoadAssetAsync<GameObject>();
            GameObject prefab = await handle.ToUniTask(cancellationToken: cancellationToken);

            Component poolablePrefab = prefab.GetComponent<IPoolable>() as Component;
            if (poolablePrefab == null)
            {
                Debug.LogError($"[PoolManager] Prefab '{prefab.name}' does not have an IPoolable component. Releasing.");
                Addressables.Release(handle);
                return;
            }

            Type type = poolablePrefab.GetType();
            if (_pools.ContainsKey(type))
            {
                Debug.LogWarning($"[PoolManager] Pool for type '{type.Name}' already exists. Skipping duplicate config.");
                Addressables.Release(handle);
                return;
            }

            var entry = new PoolEntry(handle, poolablePrefab, config.maxPoolSize, config.initialPoolSize);
            _pools[type] = entry;

            // Pre-warm
            for (int i = 0; i < config.initialPoolSize; i++)
            {
                Component instance = Instantiate(poolablePrefab, transform);
                instance.gameObject.SetActive(false);
                instance.transform.SetParent(transform);
                entry.Inactive.Add(instance);
            }
        }
        
        public T Get<T>(Transform parent = null) where T : Component, IPoolable
        {
            Type type = typeof(T);
            if (!_pools.TryGetValue(type, out PoolEntry entry))
                throw new InvalidOperationException($"[PoolManager] No pool registered for type '{type.Name}'. Ensure it is configured in poolConfigs.");

            T instance = null;

            for (int i = entry.Inactive.Count - 1; i >= 0; i--)
            {
                Component candidate = entry.Inactive[i];
                entry.Inactive.RemoveAt(i);
                if (!candidate) continue; // Unity destroyed check — non-generic nên operator hoạt động
                instance = candidate as T;
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
            
            if (entry.Inactive.Count >= entry.MaxPoolSize)
            {
                Destroy(instance.gameObject);
                return;
            }

            if (entry.Inactive.Contains(instance))
            {
                Debug.LogWarning($"[PoolManager] '{type.Name}' is already in the pool — double Return. Ignoring.");
                return;
            }

            instance.OnReturnToPool();
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(transform);

            entry.Inactive.Add(instance);
        }

        public void Dispose()
        {
            if (_pools.Count == 0)
                return;

            foreach (PoolEntry entry in _pools.Values)
                CleanupEntry(entry);

            _pools.Clear();
        }

        private void CleanupEntry(PoolEntry entry)
        {
            for (int i = 0; i < entry.Inactive.Count; i++)
            {
                if (entry.Inactive[i] != null)
                    Destroy(entry.Inactive[i].gameObject);
            }

            entry.Inactive.Clear();

            if (entry.Handle.IsValid())
                Addressables.Release(entry.Handle);
        }
        #endregion

        private class PoolEntry
        {
            public readonly AsyncOperationHandle<GameObject> Handle;
            public readonly Component Prefab;
            public readonly int MaxPoolSize;
            public readonly List<Component> Inactive;

            public PoolEntry(AsyncOperationHandle<GameObject> handle, Component prefab, int maxPoolSize, int initialCapacity)
            {
                Handle = handle;
                Prefab = prefab;
                MaxPoolSize = maxPoolSize;
                Inactive = new List<Component>(initialCapacity);
            }
        }
    }
}

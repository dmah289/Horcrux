using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Pooling;
using Horcrux.Runtime.Utilities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Horcrux.Runtime.Implementations.Pooling
{
    public abstract partial class PoolService<TKey> : MonoBehaviour, IPoolService<TKey> where TKey : struct, Enum
    {
        [Splitter("Configs")]
        [SerializeField] private PoolConfig<TKey>[] poolConfigs = Array.Empty<PoolConfig<TKey>>();

        private readonly Dictionary<TKey, PoolEntry> _entryByKey = new();
        private readonly Dictionary<Component, PoolEntry> _entryByActiveInstance = new();
        private CancellationTokenSource _warmCts;

        #region Unity Callbacks
        
        private void OnDestroy()
        {
            ReleaseAll();
        }
        
        #endregion

        #region API
        
        public async UniTask WarmAsync(CancellationToken ct)
        {
            if (_warmCts != null)
            {
                Debug.LogError("[PoolService]: WarmAsync is running, skip overlap calling.", this);
                return;
            }
            
            _warmCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                CancellationToken warmCt = _warmCts.Token;
                UniTask[] warms = new UniTask[poolConfigs.Length];

                for (int i = 0; i < poolConfigs.Length; i++)
                    warms[i] = WarmOneAsync(poolConfigs[i], warmCt);

                await UniTask.WhenAll(warms);
            }
            finally
            {
                _warmCts.Dispose();
                _warmCts = null;
            }
        }
        
        public T Get<T>(TKey key, Transform parent) where T : Component, IPoolable
        {
            if (!_entryByKey.TryGetValue(key, out PoolEntry entry))
            {
                Debug.LogError($"[PoolService]: pool '{key}' has not been warmed.", this);
                return null;
            }
            
            Component instance = PopAlive(entry);
            if (instance == null)
                instance = Instantiate(entry.PrefabPoolable);
            
            instance.transform.SetParent(parent);
            T typed = (T)instance;
            _entryByActiveInstance.Add(instance, entry);
            instance.gameObject.SetActive(true);
            typed.OnGetFromPool();
            
            return typed;
        }

        public void Return<T>(T instance) where T : Component, IPoolable
        {
            if (!_entryByActiveInstance.Remove(instance, out PoolEntry entry))
            {
                Debug.LogError($"[PoolService]: {instance} was not found in any pool.", this);
                return;
            }
            
            if (instance == null)
            {
                Debug.LogError($"[PoolService]: instance in pool {entry.Key} has been destroyed before returning.", this);
                return;
            }
            
            instance.OnReturnToPool();

            if (entry.Inactive.Count >= entry.MaxPoolSize)
            {
                Destroy(instance.gameObject);
                return;
            }
            
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(transform);
            entry.Inactive.Push(instance);
        }

        public void ReleaseAll()
        {
            _warmCts?.Cancel();

            foreach (var pair in _entryByActiveInstance)
            {
                if(pair.Key)
                    Destroy(pair.Key.gameObject);
            }
            _entryByActiveInstance.Clear();

            foreach (PoolEntry entry in _entryByKey.Values)
            {
                while (entry.Inactive.Count > 0)
                {
                    Component instance = entry.Inactive.Pop();
                    if (instance)
                        Destroy(instance.gameObject);
                }
                
                if(entry.Prefab.IsValid())
                    entry.Prefab.ReleaseAsset();
            }
            _entryByKey.Clear();
        }
        
        #endregion
        
        #region Class Methods

        private async UniTask WarmOneAsync(PoolConfig<TKey> config, CancellationToken ct)
        {
            if (_entryByKey.ContainsKey(config.key))
            {
                Debug.LogError($"[PoolService]: Pool {config.key} has already existed.", this);
                return;
            }
            
            if (!config.prefab.RuntimeKeyIsValid())
            {
                Debug.LogError($"[PoolService]: Prefab {config.key} is invalid.", this);
                return;
            }
            
            AsyncOperationHandle<GameObject> handle = config.prefab.LoadAssetAsync<GameObject>();
            bool isCanceled;
            GameObject prefab;
            bool loaded = false;

            try
            {
                (isCanceled, prefab) = await handle.ToUniTask(cancellationToken: ct)
                    .SuppressCancellationThrow();
                if (isCanceled)
                    return;
                loaded = true;
            }
            finally
            {
                if(!loaded)
                    config.prefab.ReleaseAsset();
            }
            
            if (prefab.GetComponent<IPoolable>() is not Component poolable)
            {
                Debug.LogError($"[PoolService]: prefab {prefab.name} of {config.key} doesn't implement IPoolable.");
                config.prefab.ReleaseAsset();
                return;
            }

            PoolEntry entry = new PoolEntry(config.key, config.prefab, poolable, config.maxPoolSize);
            if (!_entryByKey.TryAdd(config.key, entry))
            {
                Debug.LogError($"[PoolService]: Pool with key '{config.key}' already exists.");
                config.prefab.ReleaseAsset();
                return;
            }

            for (int i = 0; i < config.initialPoolSize; i++)
            {
                Component instance = Instantiate(poolable, transform);
                instance.gameObject.SetActive(false);
                entry.Inactive.Push(instance);
            }
        }

        private static Component PopAlive(PoolEntry entry)
        {
            while (entry.Inactive.Count > 0)
            {
                Component candidate = entry.Inactive.Pop();
                if (candidate)
                    return candidate;
                
                Debug.LogError($"[PoolService]: An instance in pool {entry.Key} is destroyed, skip.");
            }

            return null;
        }
        
        #endregion

        private class PoolEntry
        {
            public readonly TKey Key;
            public readonly AssetReference Prefab;
            public readonly Component PrefabPoolable;
            public readonly int MaxPoolSize;
            public readonly Stack<Component> Inactive;

            public PoolEntry(TKey key, AssetReference prefab, Component prefabPoolable, int maxPoolSize)
            {
                Key = key;
                Prefab = prefab;
                PrefabPoolable = prefabPoolable;
                MaxPoolSize = maxPoolSize;
                Inactive = new Stack<Component>(maxPoolSize);
            }
        }
    }
}

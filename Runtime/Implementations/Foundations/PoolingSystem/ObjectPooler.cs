using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Horcrux.Runtime.Utilities;

namespace Horcrux.Runtime.Implementations.PoolingSystem
{
    public static class ObjectPooler
    {
        private static List<Component>[] poolLists = new List<Component>[ConstantKey.POOL_AMOUNT];
        private static Component[] prefabCache = new Component[ConstantKey.POOL_AMOUNT];

        private static readonly Transform gameplayPoolParent =
            GameObject.FindGameObjectWithTag(ConstantKey.GAMEPLAY_POOL_PARENT_TAG).transform;
        private static readonly Transform uiPoolParent =
            GameObject.FindGameObjectWithTag(ConstantKey.UI_POOL_PARENT_TAG).transform;
        
        /// <summary>
        /// Check addressable key for prefab and cache prefab, then create pool for type
        /// </summary>
        /// <param name="type">The enum value index-based mapping to the pool list</param>
        /// <typeparam name="T">Type of the object need pooling</typeparam>
        /// <exception cref="KeyNotFoundException">No addressable key found for pooling type</exception>
        private static async UniTask CreatePool<T>(PoolingType type, CancellationToken cancellationToken) where T : Component
        {
            Debug.LogError("Creating pool for type: " + type);
            if(!ConstantKey.ADRESSABLE_POOLING_KEY.ContainsKey(type) || string.IsNullOrEmpty(ConstantKey.ADRESSABLE_POOLING_KEY[type]))
                throw new KeyNotFoundException($"No addressable key found for pooling type: {type}");
            
			// GameObject go = await Addressables.LoadAssetAsync<GameObject>(ConstantKey.ADRESSABLE_POOLING_KEY[type])
   //              			.ToUniTask(cancellationToken: cancellationToken);
            // TODO : Fix this
            GameObject go = null;
            T prefab = go.GetComponent<T>();
            prefabCache[(byte)type] = prefab;
            
            int size = ConstantKey.INITIAL_POOL_SIZE;
            poolLists[(byte)type] = new List<Component>(size);
            for (int i = 0; i < size; i++)
            {
                T instance = GameObject.Instantiate(prefab);
                ReturnToPool(type, instance, cancellationToken);
            }
        }
        
        /// <summary>
        /// WARNING : Call in async method and .Forget() that method to avoid conflict during pool creation
        /// <br></br>
        /// Create pool if not created yet and get object from pool by type (create new object if pool is empty)
        /// </summary>
        /// <param name="type">The enum value index-based mapping to the pool list</param>
        /// <param name="parent">The returned object's parent</param>
        /// <param name="onGetObject">Callback when the object is returned</param>
        /// <param name="allowChecking">Allow checking if the returned object has the same type with generics type</param>
        /// <typeparam name="T">Type of the object need pooling</typeparam>
        /// <returns>Object with mapped type</returns>
        /// <exception cref="InvalidCastException">The returned object doesn't have T component</exception>
        public static async UniTask<T> GetFromPool<T>(PoolingType type,
                                                    CancellationToken cancellationToken,
                                                    Transform parent = null,
                                                    Action<T> onGetObject = null,
                                                    bool allowChecking = false) where T : Component
        {
            if (poolLists[(byte)type] == null)
                await CreatePool<T>(type, cancellationToken);

            T instance;
            
            if (poolLists[(byte)type].Count == 0)
            {
                instance = GameObject.Instantiate(prefabCache[(byte)type]) as T;
            }
            else
            {
                if (allowChecking)
                {
                    Component comp = poolLists[(byte)type][^1];
                    instance = comp as T;
                    if (instance == null)
                        throw new InvalidCastException($"Type {typeof(T)} mismatch with pooled type {type}");
                }
                else instance = poolLists[(byte)type][^1] as T;
                poolLists[(byte)type].RemoveAt(poolLists[(byte)type].Count - 1);
            }
            
            instance.transform.SetParent(parent);
            instance.gameObject.SetActive(true);
            
            if (instance is IPoolableObject poolable)
                poolable.OnGetFromPool();
            else onGetObject?.Invoke(instance);
            
            return instance;
        }
        
        /// <summary>
        /// Return object to pool by type (index-based mapping)
        /// </summary>
        /// <param name="type">The enum value index-based mapping to the pool list</param>
        /// <param name="instance">Instance that need returning to pool</param>
        /// <param name="allowChecking">Allow checking if the object returned to pool has the same type with cached prefab</param>
        /// <typeparam name="T">Type of the object need returning to pool</typeparam>
        /// <exception cref="InvalidCastException">Throw if instance doesn't have the same type with objects in respectively pool</exception>
        public static void ReturnToPool<T>(PoolingType type, T instance, CancellationToken cancellationToken, bool allowChecking = false) where T : Component
        {
            if(instance == null)
                throw new ArgumentNullException(nameof(instance), $"Cannot return a null {type} instance to the pool.");

            if (allowChecking)
            {
                Type prefabType = prefabCache[(byte)type].GetType();
                if (prefabType != typeof(T))
                    throw new InvalidCastException($"Type {typeof(T)} mismatch with pooled type {prefabType.Name}");
            }
            
            if (poolLists[(byte)type].Count >= ConstantKey.MAX_POOL_SIZE)
            {
                GameObject.Destroy(instance.gameObject);
                return;
            }
            
            instance.gameObject.SetActive(false);
            instance.transform.SetParent(type > PoolingType.Separator ? uiPoolParent : gameplayPoolParent);
            poolLists[(byte)type].Add(instance);
        }
        
        /// <summary>
        /// Cleanup single pool and release respectively addressable asset
        /// </summary>
        public static void CleanupSinglePool(PoolingType type, CancellationToken cancellationToken)
        {
            if (poolLists[(byte)type] == null)
                return;

            List<Component> instances = poolLists[(byte)type];
            foreach (Component instance in instances)
            {
                if (instance != null)
                    GameObject.Destroy(instance.gameObject);
            }
    
            poolLists[(byte)type].Clear();
            poolLists[(byte)type] = null;

            if (prefabCache[(byte)type] != null)
            {
                // TODO: Fix this
                // Addressables.Release(prefabCache[(byte)type].gameObject);
                prefabCache[(byte)type] = null;
            }
        }

        /// <summary>
        /// Cleanup all pools and release all addressable assets
        /// </summary>
        public static void Dispose(CancellationToken cancellationToken)
        {
            for (int i = 0; i < poolLists.Length; i++)
            {
                CleanupSinglePool((PoolingType)i, cancellationToken);
            }

            poolLists = null;
            Debug.Log("ObjectPooler disposed and all pools cleared.");
        }
    }
}

using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class AssetReferenceGameObjectExtensions
    {
        public static async UniTask<T> SpawnAsync<T>(this AssetReferenceGameObject asset, Transform parent = null) where T : Component
            => (await asset.InstantiateAsync(parent)).GetComponent<T>();
    }
}
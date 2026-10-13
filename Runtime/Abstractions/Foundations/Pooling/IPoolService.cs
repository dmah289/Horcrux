using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Pooling
{
    public interface IPoolService<TKey> : IService<IPoolService<TKey>> 
        where TKey : struct, Enum
    {
        UniTask WarmAsync(CancellationToken ct);
        T Get<T>(TKey key, Transform parent) where T : Component, IPoolable;
        void Return<T>(T instance) where T : Component, IPoolable;
        void ReleaseAll();
    }
}

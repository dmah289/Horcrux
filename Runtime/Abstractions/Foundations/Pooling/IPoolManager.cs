using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Pooling
{
    public interface IPoolManager : IService<IPoolManager>
    {
        UniTask Initialize(CancellationToken cancellationToken);
        UniTask<T> Get<T>(Transform parent = null, CancellationToken cancellationToken = default) where T : Component, IPoolable;
        void Return<T>(T instance) where T : Component, IPoolable;
        void Dispose();
    }
}

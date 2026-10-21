using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    public interface IRemoteConfigProvider : IService<IRemoteConfigProvider>
    {
        public event Action OnFetched;
        public bool IsFetched { get; }

        UniTask FetchAndActivateAsync(TimeSpan timeout, CancellationToken ct);
        public bool TryGetRemoteValue(string firebaseKey, out string value);
    }
}
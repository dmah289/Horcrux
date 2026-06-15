using System;

namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    public interface IRemoteConfigProvider : IService<IRemoteConfigProvider>
    {
        public event Action OnFetched;
        public bool IsFetched { get; }

        public bool TryGetRemoteValue(string firebaseKey, out string value);
    }
}
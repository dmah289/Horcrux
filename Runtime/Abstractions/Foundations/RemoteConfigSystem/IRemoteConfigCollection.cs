using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    /// <summary>
    /// An interface in game project implements this.
    /// </summary>
    public interface IRemoteConfigCollection
    {
        public IEnumerable<IRemoteConfig> RemoteConfigs { get; }
        public IRemoteConfigProvider RemoteConfigProvider { get; }
        public bool AllRemoteConfigsApplied { get; }
        
        public void Initialize();
    }
}
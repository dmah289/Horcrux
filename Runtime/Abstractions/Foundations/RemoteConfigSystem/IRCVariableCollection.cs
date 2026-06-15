using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    public interface IRCVariableCollection
    {
        public IEnumerable<IRCVariable> RCVariables { get; }
        public IRemoteConfigProvider RemoteConfigProvider { get; }
        
        public void Initialize();
    }
}
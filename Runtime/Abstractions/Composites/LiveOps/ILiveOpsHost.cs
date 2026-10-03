using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public interface ILiveOpsHost : IService<ILiveOpsHost>
    {
        IReadOnlyList<ILiveOpsModule> Modules { get; }
        
        void Register(ILiveOpsModule liveOpsModule);
        void Unregister(ILiveOpsModule liveOpsModule);
    }
}
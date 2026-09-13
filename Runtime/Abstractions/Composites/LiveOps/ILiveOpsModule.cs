using Horcrux.Runtime.Utilities.EventBus;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public enum LiveOpsModuleState
    {
        Inactive, Running, Finished
    }

    public readonly struct LiveOpsSecondTick : IEvent
    {
        public readonly long NowUnix;
        
        public LiveOpsSecondTick(long nowUnix)
        {
            NowUnix = nowUnix;
        }
    }
    
    public interface ILiveOpsModule
    {
         string ModuleId { get; }
         LiveOpsModuleState State { get; }
         
         void Initialize(long nowUnix);
         void Tick(long nowUnix);
    }
}
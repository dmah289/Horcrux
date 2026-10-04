using Horcrux.Runtime.Utilities.EventBus;

namespace Horcrux.Runtime.Abstractions.LiveOps
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

    public readonly struct LiveOpsHomeFlowRequested : IEvent { }
    
    public interface ILiveOpsModule
    {
         string ModuleId { get; }
         LiveOpsModuleState State { get; }
         /// <summary>
         /// Higher goes first. Ties fall back to ModuleId.
         /// </summary>
         int Priority { get; }
         ALiveOpsHomeFlow HomeFlow { get; }
         ALiveOpsLoseFlow LoseFlow { get; }
         
         void Initialize(long nowUnix);
         void Tick(long nowUnix);
    }
}
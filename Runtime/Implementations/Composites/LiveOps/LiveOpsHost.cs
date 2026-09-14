using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Composites.LiveOps;
using Horcrux.Runtime.Abstractions.Time;
using Horcrux.Runtime.Utilities.EventBus;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
    [Service(typeof(ILiveOpsHost), FindFromScene = true)]
    public class LiveOpsHost : BaseBootStep, ILiveOpsHost, IInitializable<ITimeService>
    {
        private const int TickMilliseconds = 1000;

        private readonly List<ILiveOpsModule> modules = new();
        private bool isReady;

        #region API

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            RunAsync(destroyCancellationToken).Forget();
            return UniTask.CompletedTask;
        }

        public void Register(ILiveOpsModule liveOpsModule)
        {
            modules.Add(liveOpsModule);
            
            if(isReady)
                liveOpsModule.Initialize(timeService.UtcNowUnix);
        }

        public void Unregister(ILiveOpsModule liveOpsModule)
        {
            modules.Remove(liveOpsModule);
        }

        #endregion

        #region Class Methods

        private async UniTaskVoid RunAsync(CancellationToken ct)
        {
            long now = timeService.UtcNowUnix;

            for (int i = 0; i < modules.Count; i++)
                modules[i].Initialize(now);
            isReady = true;

            while (!ct.IsCancellationRequested)
            {
                await UniTask.Delay(TickMilliseconds, DelayType.Realtime, cancellationToken: ct);
                
                now = timeService.UtcNowUnix;
                for(int i = 0; i < modules.Count; i++)
                    modules[i].Tick(now);
                
                EventBus<LiveOpsSecondTick>.Publish(new LiveOpsSecondTick(now));
            }
        }

        #endregion

        #region DI

        private ITimeService timeService;
        public void Init(ITimeService argument)
        {
            throw new NotImplementedException();
        }

        #endregion
    }
}
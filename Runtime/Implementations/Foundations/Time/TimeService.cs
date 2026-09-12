using System;
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Abstractions.Time;
using Sisus.Init;

namespace Horcrux.Runtime.Implementations.Time
{
    [Service(typeof(ITimeService), FindFromScene = true)]
    public class TimeService : MonoBehaviour<BasePersistenceDataCollection>, ITimeService
    {
        private const int GuardWriteStepSecond = 60;

        #region Properties

        public long UtcNowUnix
        {
            get
            {
                long device = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                if (!saveCollection.IsInitialized)
                    return device;

                PersistenceDataEntry<long> guard = saveCollection.LastSeenUtcSeconds;
                if (device >= guard + GuardWriteStepSecond)
                    guard.Value = device;

                return Math.Max(device, guard);
            }
        }
        
        #endregion

        #region DI
        
        private BasePersistenceDataCollection saveCollection;
        
        protected override void Init(BasePersistenceDataCollection argument)
        {
            saveCollection = argument;
        }
        
        #endregion
    }
}

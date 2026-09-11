using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    public abstract partial class BasePersistenceDataCollection
    {
        [Splitter("Time Service")]
        [MarkedPersistence, SerializeField]
        protected internal PersistenceDataEntry<long> lastSeenUtcSeconds;
        
        public PersistenceDataEntry<long> LastSeenUtcSeconds  => lastSeenUtcSeconds;
    }
}
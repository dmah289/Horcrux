using Horcrux.Runtime.Abstractions.UserSegmentation;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    // Lives in the system folder, not Foundations/Persistence: the foundation layer must not reference a Composites type.
    public abstract partial class BasePersistenceDataCollection
    {
        [Splitter("User Segment")]
        [SerializeField, MarkedPersistence]
        protected internal PersistenceDataEntry<UserSegmentState> userSegmentState;
        
        public PersistenceDataEntry<UserSegmentState> UserSegmentState => userSegmentState;
    }
}
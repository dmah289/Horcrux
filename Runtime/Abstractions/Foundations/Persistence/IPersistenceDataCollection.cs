using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>
    /// A game project implements its own service from this one.
    /// </summary>
    public interface IPersistenceDataCollection
    {
        IReadOnlyList<IPersistenceDataEntry> Entries { get; }
        bool IsInitialized { get; }
        void Initialize();
        void FlushAll();
        void Flush(IPersistenceDataEntry entry);
    }
}
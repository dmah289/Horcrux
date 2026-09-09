using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>
    /// A game project implements its own service from this one.
    /// </summary>
    public interface ISaveCollection
    {
        IReadOnlyList<ISaveEntry> Entries { get; }
        void Initialize();
        bool IsInitialized { get; }
        void FlushAll();
    }
}
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    /// <summary>
    /// A game project implements its own service from this one.
    /// </summary>
    public interface IPersistenceDataCollection
    {
        IReadOnlyList<IPersistenceDataEntry> Entries { get; }
        bool IsInitialized { get; set; }
        void Initialize();
        void FlushAll();
        void Flush(IPersistenceDataEntry entry);
        UniTask RunAutosaveAsync(CancellationToken ct);
    }
}
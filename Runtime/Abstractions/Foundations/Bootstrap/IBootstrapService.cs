using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Bootstrap
{
    public interface IBootstrapService : IService<IBootstrapService>
    {
        /// <summary>True once cold start has run the whole chain.</summary>
        public bool IsInitialized { get; }
        
        /// <summary>Waits until cold start is done. Awaitable many times, by many consumers.</summary>
        /// <param name="ct">The consumer's token. Cancels the wait only; boot keeps running.</param>
        public UniTask UntilInitializedAsync(CancellationToken ct = default);
    }
}
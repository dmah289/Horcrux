using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions;

namespace Horcrux.Runtime.Implementations.Bootstrap
{
    public interface IBootrapService : IService<IBootrapService>
    {
        public bool IsInitialized { get; }
        
        // wait until all steps are initialized
        public UniTask UntilInitializedAsync(CancellationToken ct = default);
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.Time
{
    public interface ITimeService : IService<ITimeService>
    {
        long UtcNowUnix { get; }
        bool IsServerTimeTrusted { get; }
    }

    public interface IServerTimeProvider : IService<IServerTimeProvider>
    {
        UniTask<long> FetchUtcNowUnixAsync(CancellationToken cancellationToken);
    }
}
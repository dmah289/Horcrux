namespace Horcrux.Runtime.Abstractions.Time
{
    public interface ITimeService : IService<ITimeService>
    {
        long UtcNowUnix { get; }
    }
}

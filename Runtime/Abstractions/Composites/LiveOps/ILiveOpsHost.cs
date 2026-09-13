namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    public interface ILiveOpsHost : IService<ILiveOpsHost>
    {
        void Register(ILiveOpsModule liveOpsModule);
        void Unregister(ILiveOpsModule liveOpsModule);
    }
}
namespace Horcrux.Runtime.Abstractions.Pooling
{
    public interface IPoolable
    {
        void OnGetFromPool();
        void OnReturnToPool();
    }
}

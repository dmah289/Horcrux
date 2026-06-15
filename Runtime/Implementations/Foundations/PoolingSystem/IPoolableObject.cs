namespace Horcrux.Runtime.Implementations.PoolingSystem
{
    public interface IPoolableObject
    {
        public void OnGetFromPool();
    }
}
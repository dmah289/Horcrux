namespace Horcrux.Runtime.Abstractions.Pooling
{
    public interface IPoolable
    {
        public void OnGetFromPool();
    }
}
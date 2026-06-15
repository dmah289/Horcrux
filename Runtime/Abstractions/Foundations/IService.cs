namespace Horcrux.Runtime.Abstractions
{
    public interface IService<out T>
    {
        public static T Service => Sisus.Init.Service.Get<T>();
        
        public static bool TryGet(out T service)
        {
            return Sisus.Init.Service.TryGet(out service);
        }
    }
}
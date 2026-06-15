using System;
namespace manhnd_sdk.Scripts.SystemDesign.EventBus
{
    public interface IEventDTO { }
    
    public static class EventBus<T> where T : struct, IEventDTO
    {
        static readonly EventBinding<T> Binding = new();

        // Lower priority values are called first
        public static void Register(Action<T> callback, int priority = 0)
            => Binding.AddCallback(callback, priority);

        public static void Deregister(Action<T> callback)
            => Binding.RemoveCallback(callback);

        public static void Raise(T eventDTO = default)
            => Binding.Raise(eventDTO);

        public static void Clear()
            => Binding.Clear();
    }
}

using System;
using Horcrux.Runtime.Implementations.Utilities.Common;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.NewEventBus
{
    public interface IEvent {}
    
    public static class EventBus<T> where T : struct, IEvent
    {
        private static readonly DeferredList<Action<T>> Listeners = new();
        private static int dispatchDepth;

        public static int ListenerCount => Listeners.Count - Listeners.TombstoneCount;

        public static Subscription<T> Subscribe(Action<T> callback)
        {
            if (callback == null)
                return default;

            if (!Listeners.Add(callback))
            {
#if UNITY_EDITOR
                Debug.LogError($"[EventBus<{typeof(T).Name}>] : Ignore duplicate subscription for" + 
                               $"{callback.Method.Name} in {callback.Method.DeclaringType?.Name}");
#endif
                return default;
            }
            
            return new Subscription<T>(callback);
        }
    }
}
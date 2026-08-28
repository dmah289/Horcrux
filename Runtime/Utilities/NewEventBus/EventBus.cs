using System;
using Horcrux.Runtime.Implementations.Utilities.Common;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Runtime.Utilities.NewEventBus
{
    public interface IEvent {}
    
    public static class EventBus<T> where T : struct, IEvent
    {
        private static readonly DeferredList<Action<T>> Listeners = new();
        private static int dispatchDepth;

        public static int ActiveListenerCount => Listeners.Count - Listeners.TombstoneCount;

        // not included lambda
        private static bool IsOwnerDestroyed(Action<T> callback) => callback.Target is Object owner && owner;

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
        
        public static void Unsubscribe(Action<T> callback) => Listeners.Remove(callback);

        public static void Publish(in T e = default)
        {
            dispatchDepth++;

            try
            {
                int cnt = Listeners.Count;
                for (int i = 0; i < cnt; i++)
                {
                    Action<T> callback = Listeners[i];

                    if (callback == null)
                        continue;

                    if (IsOwnerDestroyed(callback))
                    {
                        Listeners.RemoveAt(i);
                        continue;
                    }

                    try
                    {
                        callback(e);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
            finally
            {
                dispatchDepth--;
                
                // only outer compact, inner not allowed to compact, avoid index out of range.
                if(dispatchDepth == 0)
                    Listeners.Compact();
            }
        }
    }
}
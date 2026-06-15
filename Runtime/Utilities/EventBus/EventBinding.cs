using System;
using System.Collections.Generic;
using UnityEngine;
namespace manhnd_sdk.Scripts.SystemDesign.EventBus
{
    public sealed class EventBinding<T> where T : struct, IEventDTO
    {
        struct ListenerEntry
        {
            public Action<T> Callback;
            public int Priority;
        }

        readonly List<ListenerEntry> listeners = new(4);

        public void AddCallback(Action<T> callback, int priority = 0)
        {
            if (callback == null)
                return;

            var entry = new ListenerEntry
            {
                Callback = callback,
                Priority = priority
            };

            int index = listeners.Count;
            for (int i = 0; i < listeners.Count; i++)
            {
                if (priority < listeners[i].Priority)
                {
                    index = i;
                    break;
                }
            }
            listeners.Insert(index, entry);
        }

        public void RemoveCallback(Action<T> callback)
        {
            if (callback == null)
                return;

            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                if (listeners[i].Callback == callback)
                {
                    listeners.RemoveAt(i);
                    return;
                }
            }
        }

        public void Raise(T dto)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                try
                {
                    listeners[i].Callback(dto);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

        public void Clear() => listeners.Clear();
    }
}

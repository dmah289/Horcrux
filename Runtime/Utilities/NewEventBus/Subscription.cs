using System;

namespace Horcrux.Runtime.Utilities.NewEventBus
{
    public readonly struct Subscription<T> : IDisposable where T : struct, IEvent
    {
        private readonly Action<T> callback;
        public Subscription(Action<T> callback) => this.callback = callback;
        
        public void Dispose()
        {
        }
    }
}
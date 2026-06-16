namespace Horcrux.Runtime.Utilities.EventBus
{
    public interface IEventBusListener
    {
        public void RegisterCallbacks();
        public void DeregisterCallbacks();
    }
}
namespace manhnd_sdk.Scripts.SystemDesign.EventBus
{
    public interface IEventBusListener
    {
        public void RegisterCallbacks();
        public void DeregisterCallbacks();
    }
}
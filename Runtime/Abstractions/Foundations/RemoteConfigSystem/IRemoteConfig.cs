namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    public interface IRemoteConfig
    {
        public string FirebaseKey { get;}
        public bool AllowFetching { get; set; }

        public void ApplyRemoteValue(IRemoteConfigProvider provider);
        public void ResetFetchedState();
    }
}
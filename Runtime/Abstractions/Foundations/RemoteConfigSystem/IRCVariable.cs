namespace Horcrux.Runtime.Abstractions.RemoteConfigSystem
{
    public interface IRCVariable
    {
        public string FirebaseKey { get;}
        public bool AllowFetching { get; set; }

        public void ApplyRemoteValue(IRemoteConfigProvider provider);
    }
}
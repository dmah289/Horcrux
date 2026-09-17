namespace Horcrux.Runtime.Abstractions.Persistence
{
    public interface IPersistenceDataEntry
    {
        string Key { get; }
        string StoredPayload { get; }
        void Setup(IPersistenceDataCollection owner);
        void ReadPayload(string payload);
        string WritePayload();
        void CacheStoredPayload(string payload);
    }
}
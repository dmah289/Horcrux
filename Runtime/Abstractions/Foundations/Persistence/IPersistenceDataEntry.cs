namespace Horcrux.Runtime.Abstractions.Persistence
{
    public interface IPersistenceDataEntry
    {
        string Key { get; }
        /// <summary>
        /// Has changes not yet stored.
        /// </summary>
        bool IsDirty { get; }
        void Setup(IPersistenceDataCollection owner);
        void ReadPayload(string payload);
        string WritePayload();
        /// <summary>
        /// Clearing it from game code drops unsaved progress
        /// with nothing to show it happened.
        /// </summary>
        void ClearDirty();
    }
}
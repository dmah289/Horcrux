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
        /// Flags unsaved changes so the next flush writes this entry.
        /// </summary>
        void MarkDirty();
        /// <summary>
        /// Clearing it from game code drops unsaved progress
        /// with nothing to show it happened.
        /// </summary>
        void ClearDirty();
    }
}
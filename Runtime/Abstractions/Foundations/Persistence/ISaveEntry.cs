namespace Horcrux.Runtime.Abstractions.Persistence
{
    public interface ISaveEntry
    {
        string Key { get; }
        
        /// <summary>
        /// Has changed not yet stored.
        /// </summary>
        bool IsDirty { get; }

        string WritePayload();

    }
}
namespace Horcrux.Runtime.Abstractions.Composites
{
    public interface ILevelManager
    {
        public int LevelDataAmount { get; }
        // 0-based index of the current level data in the level data list.
        public int CurrLevelDataIndex { get; }
        // 1-based index.
        public int CurrPlayerLevelIndex { get; }
    }
}
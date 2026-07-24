namespace Horcrux.Runtime.Abstractions.Composites
{
    public interface ILevelCheater
    {
        public void NextLevel();
        public void PreviousLevel();
        public void JumpToLevel(int levelIndex);
    }
}
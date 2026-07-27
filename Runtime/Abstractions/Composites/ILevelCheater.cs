namespace Horcrux.Runtime.Abstractions.Composites
{
    public interface ILevelCheater : IService<ILevelCheater>
    {
        public void NextLevel();
        public void PreviousLevel();
        public void JumpToLevel(int levelIndex);
    }
}
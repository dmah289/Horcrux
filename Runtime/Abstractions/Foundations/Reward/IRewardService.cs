namespace Horcrux.Runtime.Abstractions.Reward
{
    public readonly struct RewardData
    {
        public readonly int TypeId;
        public readonly int Amount;
        
        public RewardData(int typeId, int amount)
        {
            TypeId = typeId;
            Amount = amount;
        }
    }

    public interface IRewardHandler
    {
        void Grant(in RewardData reward, string placement);
    }
    
    public interface IRewardService : IService<IRewardService>
    {
        void Register(int typeId, IRewardHandler handler);
        void Grant(in RewardData reward, string placement);
    }
}
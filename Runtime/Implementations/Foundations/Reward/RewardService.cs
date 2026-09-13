using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.Reward;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Reward
{
    [Service(typeof(IRewardService), FindFromScene = true)]
    public class RewardService : MonoBehaviour, IRewardService
    {
        private readonly Dictionary<int, IRewardHandler> handlers = new();
        
        public void Register(int typeId, IRewardHandler handler)
        {
            if (handler == null)
            {
                Debug.LogError($"[RewardService]: Register null handler for type {typeId}.", this);
                return;
            }

            if (!handlers.TryAdd(typeId, handler))
                Debug.LogError($"[RewardService]: Type {typeId} already has a handler.", this);
        }

        public void Grant(in RewardData reward, string placement)
        {
            if (reward.Amount <= 0)
            {
                Debug.LogError($"[RewardService]: Invalid reward amount ({reward.Amount}) for type {reward.TypeId} at {placement}.", this);
                return;
            }

            if (!handlers.TryGetValue(reward.TypeId, out IRewardHandler handler))
            {
                Debug.LogError($"[RewardService]: No handler for type {reward.TypeId} at {placement}.", this);
                return;
            }
            
            handler.Grant(reward, placement);
        }
    }
}
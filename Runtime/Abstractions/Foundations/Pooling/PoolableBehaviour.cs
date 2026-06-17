using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Pooling
{
    /// <summary>
    /// Base class for poolable components. When multiple prefabs share the same behavior.
    /// </summary>
    public abstract class PoolableBehaviour : MonoBehaviour, IPoolable
    {
        public virtual void OnGetFromPool() { }
        public virtual void OnReturnToPool() { }
    }
}

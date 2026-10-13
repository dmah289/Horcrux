using Horcrux.Runtime.Abstractions.Pooling;
using Horcrux.Runtime.Implementations.Pooling;

namespace Horcrux.Tests
{
    /// <summary>Key enum for the PlayMode pool tests. Only <see cref="A"/> gets an entry; <see cref="B"/> stands for "not warmed".</summary>
    public enum TestPool { A = 1, B = 2 }

    /// <summary>A game's pool service, as a project would derive it: empty, only closes the key enum.</summary>
    public sealed class TestPoolService : PoolService<TestPool> { }

    /// <summary>Pooled component that counts its hooks, so a test can tell "called once" from "called twice" or "not at all".</summary>
    public sealed class TestPoolable : PoolableBehaviour
    {
        public int GetAmount;
        public int ReturnAmount;

        public override void OnGetFromPool() => GetAmount++;

        public override void OnReturnToPool() => ReturnAmount++;
    }

    /// <summary>A second poolable type, unrelated to <see cref="TestPoolable"/>, for the wrong-<c>T</c> cast case.</summary>
    public sealed class OtherPoolable : PoolableBehaviour { }
}

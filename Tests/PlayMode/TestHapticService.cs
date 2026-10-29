using Horcrux.Runtime.Abstractions.Haptics;

namespace Horcrux.Tests
{
    /// <summary>Id enum for the PlayMode haptic tests.</summary>
    public enum TestHaptic { A = 1, B = 2 }

    /// <summary>A game's service class, as a project would derive it: empty, only closes the enum.</summary>
    public sealed class TestHapticService : HapticService<TestHaptic> { }
}

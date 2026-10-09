using Horcrux.Runtime.Implementations.Audio;

namespace Horcrux.Tests
{
    /// <summary>Id enums for the PlayMode audio tests. <see cref="TestMusic"/> is <c>byte</c>-backed on purpose.</summary>
    public enum TestSfx { A = 1, B = 2, C = 3 }

    public enum TestMusic : byte { M = 1, N = 2 }

    /// <summary>A game's service class, as a project would derive it: empty, only closes the two enums.</summary>
    public sealed class TestAudioService : AudioService<TestSfx, TestMusic> { }
}

using Horcrux.Runtime.Implementations.Audio;

namespace Horcrux.Tests
{
    /// <summary>Id enums for audio tests. <see cref="TestMusic"/> is <c>byte</c>-backed on purpose: the catalog must work with either.</summary>
    public enum TestSfx { A = 1, B = 2, C = 3 }

    public enum TestMusic : byte { M = 1, N = 2 }

    /// <summary>A game's catalog class, as a project would derive it: empty, only closes the two enums.</summary>
    public sealed class TestAudioCatalog : AudioCatalog<TestSfx, TestMusic> { }
}

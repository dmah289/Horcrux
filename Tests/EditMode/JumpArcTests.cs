using Horcrux.Runtime.Utilities.ExtensionMethods;
using NUnit.Framework;
using UnityEngine;

namespace Horcrux.Tests
{
    /// <summary>Covers E1–E2 of the case table in docs/plans/Horcrux_CharmJump_Plan.md Task 4.</summary>
    public sealed class JumpArcTests
    {
        private const float CheckpointTolerance = 1e-5f;
        private const float SweepTolerance = 1e-4f;

        // The plan's §0 checkpoints: start at the origin, height 1.
        [TestCase(4f, 0f, 2f, 0f, 0f)]
        [TestCase(4f, 0f, 2f, 0.5f, 1f)]   // both ends level: the peak is the height
        [TestCase(4f, 0f, 2f, 1f, 0f)]
        [TestCase(0f, 2f, 0f, 0.5f, 2.5f)] // a higher end bulges the arc by Δy/4 more
        [TestCase(0f, 2f, 0f, 1f, 2f)]     // lands exactly on the end height
        public void Evaluate_MatchesCheckpoints(float toX, float toY, float toZ, float t, float expectedY)
        {
            Vector3 point = JumpArc.Evaluate(Vector3.zero, new Vector3(toX, toY, toZ), 1f, t);

            Assert.AreEqual(expectedY, point.y, CheckpointTolerance);
        }

        // t outside [0, 1] is what an overshooting ease such as OutBack feeds in.
        [Test]
        public void Evaluate_MatchesDOTweenFormula_IncludingOvershoot()
        {
            var random = new System.Random(20261022);
            float[] heights = { 0f, 1f, 3.5f };

            for (int sample = 0; sample < 50; sample++)
            {
                Vector3 from = RandomPoint(random);
                Vector3 to = RandomPoint(random);

                foreach (float height in heights)
                {
                    for (int step = -20; step <= 120; step++)
                    {
                        float t = step * 0.01f;
                        Vector3 point = JumpArc.Evaluate(from, to, height, t);
                        Vector3 line = Vector3.LerpUnclamped(from, to, t);

                        Assert.AreEqual(line.x, point.x, SweepTolerance, $"x at t={t}");
                        Assert.AreEqual(line.z, point.z, SweepTolerance, $"z at t={t}");
                        Assert.AreEqual(DOTweenJumpHeight(from.y, to.y, height, t), point.y, SweepTolerance,
                            $"y at t={t}, from={from}, to={to}, height={height}");
                    }
                }
            }
        }

        // DOTween's one-jump DOJump: OutQuad height offset plus the Yoyo bump 4h·t(1−t), as Card computed it by hand.
        private static float DOTweenJumpHeight(float fromY, float toY, float height, float t)
            => fromY + (toY - fromY) * (t * (2f - t)) + height * 4f * t * (1f - t);

        private static Vector3 RandomPoint(System.Random random)
            => new Vector3(RandomCoordinate(random), RandomCoordinate(random), RandomCoordinate(random));

        private static float RandomCoordinate(System.Random random) => (float)(random.NextDouble() * 10.0 - 5.0);
    }
}

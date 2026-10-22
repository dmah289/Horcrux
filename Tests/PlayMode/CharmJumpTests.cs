using System.Collections;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using NUnit.Framework;
using PrimeTween;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Horcrux.Tests
{
    /// <summary>
    /// Covers P1–P4 and A0–A2 of the case table in docs/plans/Horcrux_CharmJump_Plan.md Task 4: the running tweens land
    /// on <see cref="JumpArc"/>, and a call allocates nothing.
    /// </summary>
    /// <remarks>
    /// Allocation goes through Unity's GC.Alloc recorder, not <see cref="AllocationMeter"/>: in the Editor the heap did not
    /// grow for 1000 closures (~100 KB), so a heap-size meter cannot see a per-call closure at this call count.
    /// </remarks>
    public sealed class CharmJumpTests
    {
        private const float Tolerance = 1e-4f;
        private const float Height = 1.5f;
        private const float Duration = 1f;
        private const int CallAmount = 1000;

        private static readonly float[] Progresses = { 0f, 0.25f, 0.5f, 0.9f, 1f };
        private static readonly Vector3 From = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 To = new Vector3(4f, 2f, 1f);

        private static float s_sink;

        private GameObject _parent;
        private Transform _jumper;

        [SetUp]
        public void SetUp()
        {
            // Pool growth inside the measured run would read as an allocation.
            PrimeTweenConfig.SetTweensCapacity(30_000);

            _parent = new GameObject("charm_jump_test_parent");
            _jumper = new GameObject("charm_jump_test_jumper").transform;
            _jumper.SetParent(_parent.transform, false);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Tween.StopAll();
            Object.Destroy(_parent);
            yield return null; // stopped tweens go back to the pool on the next update
        }

        [UnityTest]
        public IEnumerator CharmJump_Linear_LandsOnJumpArc()
        {
            _jumper.position = From;
            Sequence jump = _jumper.CharmJump(To, Height, Duration, Ease.Linear);
            jump.isPaused = true;
            yield return null;

            foreach (float progress in Progresses)
            {
                jump.progress = progress;
                AssertNear(JumpArc.Evaluate(From, To, Height, progress), _jumper.position, $"progress {progress}");
            }
        }

        [UnityTest]
        public IEnumerator CharmJump_OutQuad_EasesTheWholeArc()
        {
            _jumper.position = From;
            Sequence jump = _jumper.CharmJump(To, Height, Duration, Ease.OutQuad);
            jump.isPaused = true;
            yield return null;

            foreach (float progress in Progresses)
            {
                jump.progress = progress;
                float eased = Easing.Evaluate(progress, Ease.OutQuad);
                AssertNear(JumpArc.Evaluate(From, To, Height, eased), _jumper.position, $"progress {progress}");
            }
        }

        [UnityTest]
        public IEnumerator CharmLocalJump_UnderMovedAndRotatedParent_WorksInLocalSpace()
        {
            _parent.transform.SetPositionAndRotation(new Vector3(5f, 1f, -2f), Quaternion.Euler(0f, 0f, 30f));
            var localFrom = new Vector3(1f, 0f, 0f);
            _jumper.localPosition = localFrom;
            Sequence jump = _jumper.CharmLocalJump(To, Height, Duration, Ease.Linear);
            jump.isPaused = true;
            yield return null;

            foreach (float progress in Progresses)
            {
                jump.progress = progress;
                AssertNear(JumpArc.Evaluate(localFrom, To, Height, progress), _jumper.localPosition, $"progress {progress}");
            }
        }

        [UnityTest]
        public IEnumerator CharmJump_NestedAndStopped_StaysWhereItWas()
        {
            _jumper.position = From;
            Sequence outer = Sequence.Create().Group(_jumper.CharmJump(To, Height, Duration, Ease.Linear));
            outer.isPaused = true;
            yield return null;

            outer.progress = 0.4f;
            Vector3 stoppedAt = _jumper.position;
            outer.Stop();
            yield return null;
            yield return null;

            AssertNear(stoppedAt, _jumper.position, "after Stop");
            Assert.Greater(Vector3.Distance(To, _jumper.position), 0.5f, "a stopped jump must not snap to its end");
        }

        // Control: if the recorder misses a per-call closure, the zero-allocation tests below prove nothing.
        [Test]
        public void Recorder_SeesAllocation_WhenEachCallCapturesAClosure()
        {
            Tween.Custom(0f, 1f, Duration, t => s_sink = t); // warm-up: JIT and PrimeTween's manager

            Assert.That(() =>
            {
                for (int i = 0; i < CallAmount; i++)
                {
                    float captured = i;
                    Tween.Custom(0f, 1f, Duration, t => s_sink = t + captured);
                }
            }, Is.AllocatingGCMemory());
        }

        [Test]
        public void CharmJump_DoesNotAllocate()
        {
            _jumper.CharmJump(To, Height, Duration); // warm-up: JIT, static setters, PrimeTween's manager

            Assert.That(() =>
            {
                for (int i = 0; i < CallAmount; i++)
                    _jumper.CharmJump(To, Height, Duration);
            }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void CharmLocalJump_DoesNotAllocate()
        {
            _jumper.CharmLocalJump(To, Height, Duration);

            Assert.That(() =>
            {
                for (int i = 0; i < CallAmount; i++)
                    _jumper.CharmLocalJump(To, Height, Duration);
            }, Is.Not.AllocatingGCMemory());
        }

        private static void AssertNear(Vector3 expected, Vector3 actual, string where)
        {
            Assert.AreEqual(expected.x, actual.x, Tolerance, $"x, {where}");
            Assert.AreEqual(expected.y, actual.y, Tolerance, $"y, {where}");
            Assert.AreEqual(expected.z, actual.z, Tolerance, $"z, {where}");
        }
    }
}

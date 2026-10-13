using System;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>
    /// Heap meter for "this hot path allocates 0 B" tests. Collection cannot be switched off in the Editor, so an allocation shows
    /// as heap growth or as a collection that ran during the measured run.
    /// </summary>
    /// <remarks>
    /// The answer that matters comes from a player built with the IL2CPP backend: Test Runner → PlayMode → Run on the target
    /// platform. A control test per caller (<see cref="EnumKeyedLookupAllocationTests.Meter_SeesAllocation_WhenKeysAreBoxed"/>)
    /// proves the meter sees a known allocation on that platform; if it fails, the other results mean nothing.
    /// </remarks>
    internal static class AllocationMeter
    {
        // Boxing 200k keys costs several MB; this is far below that and above test-runner noise.
        public const long ToleranceBytes = 64 * 1024;

        // Other threads can trigger a collection mid-run; a real per-call allocation fails every attempt.
        public const int AttemptAmount = 3;

        public static void AssertNoAllocation(Action action, string what)
        {
            long bytes = 0;
            int collectionAmount = 0;

            for (int attempt = 0; attempt < AttemptAmount; attempt++)
            {
                (bytes, collectionAmount) = Measure(action);

                if (collectionAmount == 0 && bytes <= ToleranceBytes)
                    return;
            }

            Assert.Fail($"{what} allocated: heap +{bytes} B, {collectionAmount} collections, in all {AttemptAmount} attempts.");
        }

        /// <summary>Runs the action once to warm up (JIT, comparer creation), then measures a second run.</summary>
        public static (long bytes, int collectionAmount) Measure(Action action)
        {
            action();

            int collectionsBefore = GC.CollectionCount(0);
            long heapBefore = GC.GetTotalMemory(false);

            action();

            long heapAfter = GC.GetTotalMemory(false);
            return (heapAfter - heapBefore, GC.CollectionCount(0) - collectionsBefore);
        }
    }
}

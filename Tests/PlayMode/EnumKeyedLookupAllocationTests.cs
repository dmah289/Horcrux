using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>
    /// Does <c>Dictionary&lt;TEnum,int&gt;.TryGetValue</c> allocate when the enum is <c>int</c> or <c>byte</c>?
    /// This is the lookup <c>AudioService.PlaySfx</c> and <c>PlayMusic</c> do per call.
    /// </summary>
    /// <remarks>
    /// Runs in the Editor (Mono) and, for the answer that matters, on a player built with the IL2CPP backend:
    /// Test Runner → PlayMode → Run on the target platform.
    /// Collection cannot be switched off in the Editor, so an allocation shows as heap growth or as a collection that ran
    /// during the run. The control test proves the meter sees boxing; if it fails, the other results mean nothing.
    /// </remarks>
    public class EnumKeyedLookupAllocationTests
    {
        private enum IntKey { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 }
        private enum ByteKey : byte { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 }

        private const int LookupAmount = 200_000;

        // Boxing 200k keys costs several MB; this is far below that and above test-runner noise.
        private const long ToleranceBytes = 64 * 1024;

        // Other threads can trigger a collection mid-run; a real per-lookup allocation fails every attempt.
        private const int AttemptAmount = 3;

        private static object s_boxSink;

        private static readonly IntKey[] IntKeys = { IntKey.A, IntKey.B, IntKey.C, IntKey.D, IntKey.E, IntKey.F, IntKey.G, IntKey.H };
        private static readonly ByteKey[] ByteKeys = { ByteKey.A, ByteKey.B, ByteKey.C, ByteKey.D, ByteKey.E, ByteKey.F, ByteKey.G, ByteKey.H };

        [Test]
        public void Meter_SeesAllocation_WhenKeysAreBoxed()
        {
            (long bytes, int collectionAmount) = Measure(() =>
            {
                for (int i = 0; i < LookupAmount; i++)
                    s_boxSink = ByteKeys[i % ByteKeys.Length];
            });

            Assert.IsTrue(bytes > ToleranceBytes || collectionAmount > 0,
                $"The meter did not see 200k boxings (heap +{bytes} B, {collectionAmount} collections): this platform cannot measure allocation this way.");
        }

        [Test]
        public void IntKey_TryGetValue_DoesNotAllocate()
        {
            Dictionary<IntKey, int> table = BuildTable(IntKeys);
            int hitAmount = 0;

            AssertNoAllocation(() => hitAmount = LookupAll(table, IntKeys), "int-backed enum key");

            Assert.AreEqual(LookupAmount, hitAmount);
        }

        [Test]
        public void ByteKey_TryGetValue_DoesNotAllocate()
        {
            Dictionary<ByteKey, int> table = BuildTable(ByteKeys);
            int hitAmount = 0;

            AssertNoAllocation(() => hitAmount = LookupAll(table, ByteKeys), "byte-backed enum key");

            Assert.AreEqual(LookupAmount, hitAmount);
        }

        private static Dictionary<TKey, int> BuildTable<TKey>(TKey[] keys) where TKey : struct, Enum
        {
            var table = new Dictionary<TKey, int>();

            for (int i = 0; i < keys.Length; i++)
                table.TryAdd(keys[i], i);

            return table;
        }

        private static int LookupAll<TKey>(Dictionary<TKey, int> table, TKey[] keys) where TKey : struct, Enum
        {
            int hitAmount = 0;

            for (int i = 0; i < LookupAmount; i++)
            {
                if (table.TryGetValue(keys[i % keys.Length], out int _))
                    hitAmount++;
            }

            return hitAmount;
        }

        private static void AssertNoAllocation(Action action, string what)
        {
            long bytes = 0;
            int collectionAmount = 0;

            for (int attempt = 0; attempt < AttemptAmount; attempt++)
            {
                (bytes, collectionAmount) = Measure(action);

                if (collectionAmount == 0 && bytes <= ToleranceBytes)
                    return;
            }

            Assert.Fail($"{what} allocated over {LookupAmount} lookups: heap +{bytes} B, {collectionAmount} collections, in all {AttemptAmount} attempts.");
        }

        /// <summary>Runs the action once to warm up (JIT, comparer creation), then measures a second run.</summary>
        private static (long bytes, int collectionAmount) Measure(Action action)
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

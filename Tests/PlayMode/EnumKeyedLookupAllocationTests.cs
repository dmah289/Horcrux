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
    /// Test Runner → PlayMode → Run on the target platform. Measured with <see cref="AllocationMeter"/>; the control test
    /// proves the meter sees boxing; if it fails, the other results mean nothing.
    /// </remarks>
    public class EnumKeyedLookupAllocationTests
    {
        private enum IntKey { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 }
        private enum ByteKey : byte { A = 1, B = 2, C = 3, D = 4, E = 5, F = 6, G = 7, H = 8 }

        private const int LookupAmount = 200_000;

        private static object s_boxSink;

        private static readonly IntKey[] IntKeys = { IntKey.A, IntKey.B, IntKey.C, IntKey.D, IntKey.E, IntKey.F, IntKey.G, IntKey.H };
        private static readonly ByteKey[] ByteKeys = { ByteKey.A, ByteKey.B, ByteKey.C, ByteKey.D, ByteKey.E, ByteKey.F, ByteKey.G, ByteKey.H };

        [Test]
        public void Meter_SeesAllocation_WhenKeysAreBoxed()
        {
            (long bytes, int collectionAmount) = AllocationMeter.Measure(() =>
            {
                for (int i = 0; i < LookupAmount; i++)
                    s_boxSink = ByteKeys[i % ByteKeys.Length];
            });

            Assert.IsTrue(bytes > AllocationMeter.ToleranceBytes || collectionAmount > 0,
                $"The meter did not see 200k boxings (heap +{bytes} B, {collectionAmount} collections): this platform cannot measure allocation this way.");
        }

        [Test]
        public void IntKey_TryGetValue_DoesNotAllocate()
        {
            Dictionary<IntKey, int> table = BuildTable(IntKeys);
            int hitAmount = 0;

            AllocationMeter.AssertNoAllocation(() => hitAmount = LookupAll(table, IntKeys), $"int-backed enum key over {LookupAmount} lookups");

            Assert.AreEqual(LookupAmount, hitAmount);
        }

        [Test]
        public void ByteKey_TryGetValue_DoesNotAllocate()
        {
            Dictionary<ByteKey, int> table = BuildTable(ByteKeys);
            int hitAmount = 0;

            AllocationMeter.AssertNoAllocation(() => hitAmount = LookupAll(table, ByteKeys), $"byte-backed enum key over {LookupAmount} lookups");

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
    }
}

using System.Globalization;
using System.Text;
using Horcrux.Editor.FeelTrace;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace Horcrux.Tests
{
    /// <summary>Token format is the contract with feeltrace.py: same bytes for the same state, whatever the machine culture.</summary>
    public sealed class FeelTraceWriterTests
    {
        private readonly StringBuilder sb = new StringBuilder(64);
        private FeelTraceWriter writer;

        [SetUp]
        public void SetUp()
        {
            sb.Clear();
            writer = new FeelTraceWriter(sb);
        }

        [Test]
        public void Entity_OpensKeyThenValues()
        {
            writer.Entity("T", 3).Value(1.5f);

            Assert.AreEqual(" |T3 1.50000", sb.ToString());
        }

        [Test]
        public void EntityByOrder_VectorIsOneToken()
        {
            writer.EntityByOrder("B").Value(new Vector3(1, -2, 0.000004f));

            Assert.AreEqual(" |B? 1.00000,-2.00000,0.00000", sb.ToString());
        }

        [Test]
        public void BoolAndInt_WriteAsIntegers()
        {
            writer.Value(true).Value(false).Value(7);

            Assert.AreEqual(" 1 0 7", sb.ToString());
        }

        [Test]
        public void Value_IgnoresCurrentCulture()
        {
            var savedCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                writer.Value(0.5f).Value(-1234567);
            }
            finally
            {
                CultureInfo.CurrentCulture = savedCulture;
            }

            Assert.AreEqual(" 0.50000 -1234567", sb.ToString());
        }

        // Upper edge of the stack buffer: the longest F5 float and int must come out whole, same as ToString.
        [TestCase(float.MaxValue)]
        [TestCase(-float.MaxValue)]
        [TestCase(float.NaN)]
        public void Value_ExtremeFloatMatchesToString(float value)
        {
            writer.Value(value);

            Assert.AreEqual(" " + value.ToString("F5", CultureInfo.InvariantCulture), sb.ToString());
        }

        // Control: the constraint must see the per-number string the old path made, or the next test proves nothing.
        [Test]
        public void Meter_SeesAllocation_WhenFloatGoesThroughToString()
        {
            Assert.That(() =>
            {
                sb.Append(0.37f.ToString("F5", CultureInfo.InvariantCulture));
                sb.Clear();
            }, Is.AllocatingGCMemory());
        }

        [Test]
        public void Value_DoesNotAllocate()
        {
            // Warm-up run first: JIT and number-format tables allocate once. Clearing keeps the builder from growing.
            TestDelegate writeOneEntity = () =>
            {
                writer.Entity("T", 12).Value(0.37f).Value(new Vector3(1, 2, 3)).Value(-7).Value(true);
                sb.Clear();
            };
            writeOneEntity();

            Assert.That(writeOneEntity, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void Value_IntMinValue()
        {
            writer.Value(int.MinValue);

            Assert.AreEqual(" -2147483648", sb.ToString());
        }
    }
}

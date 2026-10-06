using System.Text;
using Horcrux.Runtime.Utilities.Common;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers the case table in Collection_Steps.md Task 9: branch edges at one hour, one day, and a full week.</summary>
    public sealed class CountdownFormatterTests
    {
        private readonly StringBuilder sb = new StringBuilder(16);

        [TestCase(0L, "0m 0s")]            // out of time: never an empty string
        [TestCase(59L, "0m 59s")]
        [TestCase(3599L, "59m 59s")]       // just under the hour edge
        [TestCase(3600L, "1h 0m")]         // on the hour edge: branch switches
        [TestCase(86399L, "23h 59m")]      // just under the day edge
        [TestCase(86400L, "1d 0h")]        // on the day edge: branch switches
        [TestCase(604740L, "6d 23h")]      // one full weekly window
        [TestCase(3661L, "1h 1m")]         // leftover seconds floor away, never a fraction
        [TestCase(86401L, "1d 0h")]
        [TestCase(88200L, "1d 0h")]        // half an hour still reads as 0h
        public void Write_MatchesTheTable(long seconds, string expected)
        {
            CountdownFormatter.Write(seconds, sb);

            Assert.AreEqual(expected, sb.ToString());
        }

        [TestCase(0L, "0m")]
        [TestCase(59L, "0m 59s")]
        [TestCase(60L, "1m")]
        [TestCase(3600L, "1h")]
        [TestCase(3660L, "1h 1m")]
        [TestCase(86400L, "1d")]
        [TestCase(86401L, "1d")]           // a non-zero remainder below one hour still drops the unit
        [TestCase(90000L, "1d 1h")]
        [TestCase(172800L, "2d")]
        public void Write_ZeroSecondUnitDisallowed_DropsIt(long seconds, string expected)
        {
            CountdownFormatter.Write(seconds, sb, false);

            Assert.AreEqual(expected, sb.ToString());
        }

        // The caller reuses one builder every tick: the second call must not carry the first call's text.
        [Test]
        public void Write_TwiceOnTheSameBuilder_SecondCallStandsAlone()
        {
            CountdownFormatter.Write(604740L, sb);
            CountdownFormatter.Write(59L, sb);

            Assert.AreEqual("0m 59s", sb.ToString());
        }

        [Test]
        public void Write_NegativeSeconds_ClampsToZero()
        {
            CountdownFormatter.Write(-5L, sb);

            Assert.AreEqual("0m 0s", sb.ToString());
        }
    }
}

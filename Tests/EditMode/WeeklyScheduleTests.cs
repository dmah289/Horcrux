using System;
using Horcrux.Runtime.Abstractions.Composites.LiveOps;
using Horcrux.Runtime.Implementations.Composites.LiveOps;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers the marker table in LiveOpsHost.md §2: anchor Monday 08:01 UTC, duration 604740.</summary>
    public sealed class WeeklyScheduleTests
    {
        private const int AnchorDayOfWeek = 1;          // System.DayOfWeek: Sunday 0 … Monday 1
        private const int AnchorHourUtc = 8;
        private const int AnchorMinuteUtc = 1;
        private const long DurationSeconds = 604740;    // 7d − 60s

        // Every marker is derived, never typed in: a hand-typed Unix second hides its own arithmetic error.
        private static readonly long MondayMidnight =
            new DateTimeOffset(2026, 9, 7, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

        private static readonly long ThisAnchor = MondayMidnight + AnchorHourUtc * 3600L + AnchorMinuteUtc * 60L;
        private static readonly long PreviousAnchor = ThisAnchor - WeeklySchedule.WeekSeconds;
        private static readonly long PreviousEnd = PreviousAnchor + DurationSeconds;

        private static LiveOpsWindow Resolve(long nowUnix, long durationSeconds = DurationSeconds)
            => WeeklySchedule.Resolve(nowUnix, AnchorDayOfWeek, AnchorHourUtc, AnchorMinuteUtc, durationSeconds);

        [Test]
        public void Resolve_OneHourAfterAnchor_StartsAtThisAnchorAndContainsNow()
        {
            long now = MondayMidnight + 9 * 3600L;      // Mon 09:00:00

            LiveOpsWindow window = Resolve(now);

            Assert.AreEqual(ThisAnchor, window.StartUnix);
            Assert.IsTrue(window.Contains(now));
        }

        [Test]
        public void Resolve_ExactlyOnAnchor_StartsAtThatAnchorAndContainsNow()
        {
            LiveOpsWindow window = Resolve(ThisAnchor);   // Mon 08:01:00

            Assert.AreEqual(ThisAnchor, window.StartUnix);
            Assert.IsTrue(window.Contains(ThisAnchor));
        }

        [Test]
        public void Resolve_LastSecondOfDeadZone_BelongsToPreviousCycleAndIsClosed()
        {
            long now = ThisAnchor - 1;                  // Mon 08:00:59

            LiveOpsWindow window = Resolve(now);

            Assert.AreEqual(PreviousAnchor, window.StartUnix);
            Assert.AreEqual(PreviousEnd, window.EndUnix);
            Assert.IsFalse(window.Contains(now));
            Assert.AreEqual(0, window.SecondsLeft(now));
        }

        [Test]
        public void Resolve_FirstSecondOfDeadZone_BelongsToPreviousCycleAndIsClosed()
        {
            long now = PreviousEnd;                     // Mon 08:00:00 — the exclusive end of the previous cycle

            LiveOpsWindow window = Resolve(now);

            Assert.AreEqual(PreviousAnchor, window.StartUnix);
            Assert.AreEqual(PreviousEnd, window.EndUnix);
            Assert.IsFalse(window.Contains(now));
            Assert.AreEqual(0, window.SecondsLeft(now));
        }

        [Test]
        public void Resolve_LastMinuteBeforeAnchorDay_StillInsidePreviousCycle()
        {
            long now = MondayMidnight - 60;             // Sun 23:59:00

            LiveOpsWindow window = Resolve(now);

            Assert.AreEqual(PreviousAnchor, window.StartUnix);
            Assert.IsTrue(window.Contains(now));
            Assert.AreEqual(PreviousEnd - now, window.SecondsLeft(now));
        }

        [Test]
        public void Resolve_DurationLongerThanAWeek_ClampsToOneWeek()
        {
            LiveOpsWindow window = Resolve(ThisAnchor, 9_000_000_000L);

            Assert.AreEqual(WeeklySchedule.WeekSeconds, window.EndUnix - window.StartUnix);
        }

        [Test]
        public void Resolve_BeforeEpoch_KeepsStartBelowNow()
        {
            const long now = -5;                        // C# % keeps the dividend's sign; the formula must not

            LiveOpsWindow window = Resolve(now);

            Assert.LessOrEqual(window.StartUnix, now);
            Assert.Greater(window.StartUnix + WeeklySchedule.WeekSeconds, now);
        }
    }
}

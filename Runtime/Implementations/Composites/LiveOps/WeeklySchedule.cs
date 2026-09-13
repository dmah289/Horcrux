using System;
using Horcrux.Runtime.Abstractions.Composites.LiveOps;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Composites.LiveOps
{
    public static class WeeklySchedule
    {
        public const long WeekSeconds = 604800;
        private const long DaySeconds = 86400;
        private const int EpochDayOfWeek = 4;

        public static LiveOpsWindow Resolve(long nowUnix, int anchorDayOfWeek,
            int anchorHourUtc, int anchorMinuteUtc, long durationSeconds)
        {
            long firstAnchorUnix = ((anchorDayOfWeek - EpochDayOfWeek + 7) % 7) * DaySeconds
                + anchorHourUtc * 3600L + anchorMinuteUtc * 60L;

            long sinceFirstAnchor = ((nowUnix - firstAnchorUnix) % WeekSeconds + WeekSeconds) % WeekSeconds;

            long start = nowUnix - sinceFirstAnchor;
            long end = start + Math.Clamp(durationSeconds, 1, WeekSeconds);
            
            return new LiveOpsWindow(start, end);
        }
    }
}
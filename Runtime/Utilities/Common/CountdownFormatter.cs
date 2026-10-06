using System.Text;

namespace Horcrux.Runtime.Utilities.Common
{
    public static class CountdownFormatter
    {
        private const long SecondsPerMinute = 60;
        private const long SecondsPerHour = 3600;
        private const long SecondsPerDay = 86400;

        public static void Write(long seconds, StringBuilder sb, bool allowZeroInSecondUnit = true)
        {
            sb.Clear();
            if (seconds < 0)
                seconds = 0;

            if (seconds >= SecondsPerDay)
                WritePair(sb, seconds/SecondsPerDay, 'd', seconds % SecondsPerDay / SecondsPerHour, 
                    'h', allowZeroInSecondUnit);
            else if (seconds >= SecondsPerHour)
                WritePair(sb, seconds/SecondsPerHour, 'h', seconds % SecondsPerHour / SecondsPerMinute, 
                    'm', allowZeroInSecondUnit);
            else 
                WritePair(sb, seconds/SecondsPerMinute, 'm', seconds % SecondsPerMinute, 
                    's', allowZeroInSecondUnit);
        }

        private static void WritePair(StringBuilder sb, long first, char firstUnit, long second, char secondUnit,
            bool allowZeroInSecondUnit = true)
        {
            sb.Append(first).Append(firstUnit);
            if(allowZeroInSecondUnit || second != 0)
                sb.Append(' ').Append(second).Append(secondUnit);
        }
    }
}
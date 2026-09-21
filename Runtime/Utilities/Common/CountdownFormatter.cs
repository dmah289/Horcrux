using System.Text;

namespace Horcrux.Runtime.Utilities.Common
{
    public static class CountdownFormatter
    {
        private const long SecondsPerMinute = 60;
        private const long SecondsPerHour = 3600;
        private const long SecondsPerDay = 86400;

        public static void Write(long seconds, StringBuilder sb)
        {
            sb.Clear();
            if (seconds < 0)
                seconds = 0;

            if (seconds >= SecondsPerDay)
            {
                sb.Append(seconds / SecondsPerDay).Append("d ");
                sb.Append(seconds % SecondsPerDay / SecondsPerHour).Append('h');
                return;
            }
            
            if (seconds >= SecondsPerHour)
            {
                sb.Append(seconds / SecondsPerHour).Append("h ");
                sb.Append(seconds % SecondsPerHour / SecondsPerMinute).Append('m');
                return;
            }
            
            sb.Append(seconds / SecondsPerMinute).Append("m ");
            sb.Append(seconds % SecondsPerMinute). Append('s');
        }
    }
}
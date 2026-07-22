namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class BackEase
    {
        private const float C1 = 1.70158f;
        private const float C3 = C1 + 1f;          // 2.70158
        private const float C2 = C1 * 1.525f;      // 2.5949095

        public static float In(float t) => C3 * t * t * t - C1 * t * t;

        public static float Out(float t)
        {
            float f = t - 1f;
            return 1f + C3 * f * f * f + C1 * f * f;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float x = 2f * t;
                return x * x * ((C2 + 1f) * x - C2) * 0.5f;
            }
            float y = 2f * t - 2f;
            return (y * y * ((C2 + 1f) * y + C2) + 2f) * 0.5f;
        }
    }
}

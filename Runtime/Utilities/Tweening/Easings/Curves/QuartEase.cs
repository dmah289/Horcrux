namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class QuartEase
    {
        public static float In(float t)
        {
            float t2 = t * t;
            return t2 * t2;
        }

        public static float Out(float t)
        {
            float f = t - 1f;
            float f2 = f * f;
            return 1f - f2 * f2;
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float t2 = t * t;
                return 8f * t2 * t2;
            }
            float f = -2f * t + 2f;
            float f2 = f * f;
            return 1f - f2 * f2 * 0.5f;
        }
    }
}

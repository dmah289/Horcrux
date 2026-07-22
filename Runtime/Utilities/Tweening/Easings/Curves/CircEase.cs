using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class CircEase
    {
        public static float In(float t) => 1f - Mathf.Sqrt(1f - t * t);

        public static float Out(float t)
        {
            float f = t - 1f;
            return Mathf.Sqrt(1f - f * f);
        }

        public static float InOut(float t)
        {
            if (t < 0.5f)
            {
                float x = 2f * t;
                return (1f - Mathf.Sqrt(1f - x * x)) * 0.5f;
            }
            float y = -2f * t + 2f;
            return (Mathf.Sqrt(1f - y * y) + 1f) * 0.5f;
        }
    }
}

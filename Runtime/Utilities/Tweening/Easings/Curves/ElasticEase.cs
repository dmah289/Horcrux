using UnityEngine;

namespace Horcrux.Runtime.Tweening.Easing
{
    internal static class ElasticEase
    {
        private const float C4 = 2f * Mathf.PI / 3f;
        private const float C5 = 2f * Mathf.PI / 4.5f;

        public static float In(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return -Mathf.Pow(2f, 10f * t - 10f) * Mathf.Sin((10f * t - 10.75f) * C4);
        }

        public static float Out(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((10f * t - 0.75f) * C4) + 1f;
        }

        public static float InOut(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t < 0.5f
                ? -(Mathf.Pow(2f, 20f * t - 10f) * Mathf.Sin((20f * t - 11.125f) * C5)) * 0.5f
                : Mathf.Pow(2f, -20f * t + 10f) * Mathf.Sin((20f * t - 11.125f) * C5) * 0.5f + 1f;
        }
    }
}

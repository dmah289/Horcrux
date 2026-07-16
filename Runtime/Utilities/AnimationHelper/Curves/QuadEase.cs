namespace Horcrux.Runtime.Utilities.AnimationHelper
{
    internal static class QuadEase
    {
        public static float In(float t) => t * t;
        public static float Out(float t) => t * (2f - t);
        public static float InOut(float t)
            => t < 0.5f ? 2f * t * t : 
    }
}
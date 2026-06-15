using UnityEngine;

namespace manhnd_sdk.Scripts.ExtensionMethods
{
    public static class Vector2Extensions
    {
        #region Operations

        public static Vector2 With(this Vector2 v, float? x = null, float? y = null)
            => new (x ?? v.x, y ?? v.y);
        
        public static Vector2 Add(this Vector2 v, float x = 0, float y = 0)
            => new (v.x + x, v.y + y);

        public static Vector2 Multiply(this Vector2 v, float mX = 1, float mY = 1)
            => new (v.x * mX, v.y * mY);

        #endregion

        #region Helpers

        /// <summary>
        /// Get a random point within an annulus in range [minRadius,maxRadius] from the origin point. <br></br>
        /// Apply annulus area-based distribution.
        /// </summary>
        public static Vector2 RandomPointInAnnulus(this Vector2 origin, float minRadius, float maxRadius)
        {
            // Get vector direction by random angle
            float angle = 2f * Mathf.PI * Random.value;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            
            float minRadiusSq = minRadius * minRadius;
            float maxRadiusSq = maxRadius * maxRadius;
            // Get radius with annulus area-based distribution
            float radius = Mathf.Sqrt(Random.value * (maxRadiusSq - minRadiusSq) + minRadiusSq);
            
            Vector2 position = direction * radius;
            return origin + position;
        }
        
        /// <summary>
        /// Check if the target point is within range from this point
        /// </summary>
        public static bool InRangeOf(this Vector2 v, Vector2 target, float range)
            => (v - target).sqrMagnitude <= range * range;

        #endregion
        
    }
}
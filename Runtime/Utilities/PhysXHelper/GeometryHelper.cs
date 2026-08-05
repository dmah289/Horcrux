using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class GeometryHelper
    {
        /// <summary>
        /// Get a random point within an annulus in range [minRadius,maxRadius] from the origin point.<br/>
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
        /// Get a random point inside a sphere.<br/>
        /// Uses volume-based distribution for uniforming density
        /// </summary>
        public static Vector3 RandomPointIn3DAnnulus(this Vector3 origin, float minRadius, float maxRadius)
        {
            Vector3 direction = Random.onUnitSphere;

            minRadius = Mathf.Abs(minRadius);
            maxRadius = Mathf.Abs(maxRadius);
            
            float rMin3 = minRadius * minRadius * minRadius;
            float rMax3 = maxRadius * maxRadius * maxRadius;
            
            float radius = Mathf.Pow(Random.value * (rMax3 - rMin3) + rMin3, 1f / 3f);
            return origin + direction * radius;
        }
    }
}
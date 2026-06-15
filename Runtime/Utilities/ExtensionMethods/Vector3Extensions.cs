using manhnd_sdk.Scripts.ExtensionMethods.EnumTypes;
using UnityEngine;

namespace manhnd_sdk.Scripts.ExtensionMethods
{
    public static class Vector3Extensions
    {
        #region Operations

        public static Vector3 With(this Vector3 v, float? x = null, float? y = null, float? z = null)
            => new (x ?? v.x, y ?? v.y, z ?? v.z);

        public static Vector3 Add(this Vector3 v, float x = 0, float y = 0, float z = 0)
            => new (v.x + x, v.y + y, v.z + z);
        
        public static Vector3 Multiply(this Vector3 v, float mX = 1, float mY = 1, float mZ = 1)
            => new (v.x * mX, v.y * mY, v.z * mZ);

        #endregion

        #region Helpers

        /// <summary>
        /// Check if 2 points are within a certain range (inside a sphere)
        /// </summary>
        public static bool InRangeOf(this Vector3 v, Vector3 target, float range)
            => (v - target).sqrMagnitude < range * range;
        
        /// <summary>
        /// Check if 2 points are within a certain range on a specific 2D plane
        /// </summary>
        /// <param name="planeType">The type of plane that want to check</param>
        public static bool In2DRangeOf(this Vector3 v, Vector3 target, float range, PlaneType planeType = PlaneType.XZ)
        {
            Vector2 v2D, target2D;
            if (planeType == PlaneType.XZ)
            {
                v2D = new Vector2(v.x, v.z);
                target2D = new Vector2(target.x, target.z);
            }
            else if (planeType == PlaneType.YZ)
            {
                v2D = new Vector2(v.y, v.z);
                target2D = new Vector2(target.y, target.z);
            }
            else
            {
                v2D = new Vector2(v.x, v.y);
                target2D = new Vector2(target.x, target.y);
            }
            
            return (v2D - target2D).sqrMagnitude < range * range;
        }

        /// <summary>
        /// Get a random point inside a sphere<br></br>
        /// Uses valume-based distribution for uniforming density
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

        /// <summary>
        /// Get a random point inside an annulus on a specific plane. <br></br>
        /// </summary>
        /// <param name="planeType">The plane in 3D coord that need to get point</param>
        /// <returns></returns>
        public static Vector3 RandomPointIn2DAnnulus(this Vector3 origin, float minRadius, float maxRadius,  PlaneType planeType = PlaneType.XZ)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            
            float minRadiusSq = minRadius * minRadius;
            float maxRadiusSq = maxRadius * maxRadius;
            
            float distance = Mathf.Sqrt(Random.value * (maxRadiusSq - minRadiusSq) + minRadiusSq);
            Debug.Log(distance);

            Vector3 position;
            if(planeType == PlaneType.XZ) 
                position = new Vector3(direction.x, 0, direction.y) * distance;
            else if(planeType == PlaneType.YZ) 
                position = new Vector3(0, direction.x, direction.y) * distance;
            else 
                position = new Vector3(direction.x, direction.y, 0) * distance;
            
            return origin + position;
        }

        #endregion
    }
}
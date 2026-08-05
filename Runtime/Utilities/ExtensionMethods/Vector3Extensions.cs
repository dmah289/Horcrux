using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class Vector3Extensions
    {
        public static Vector3 With(this Vector3 v, float? x = null, float? y = null, float? z = null)
            => new (x ?? v.x, y ?? v.y, z ?? v.z);

        public static Vector3 Add(this Vector3 v, float x = 0, float y = 0, float z = 0)
            => new (v.x + x, v.y + y, v.z + z);
        
        public static Vector3 Multiply(this Vector3 v, float mX = 1, float mY = 1, float mZ = 1)
            => new (v.x * mX, v.y * mY, v.z * mZ);
    }
}
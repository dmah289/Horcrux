using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class Vector2Extensions
    {
        public static Vector2 With(this Vector2 v, float? x = null, float? y = null)
            => new (x ?? v.x, y ?? v.y);
        
        public static Vector2 Add(this Vector2 v, float x = 0, float y = 0)
            => new (v.x + x, v.y + y);

        public static Vector2 Multiply(this Vector2 v, float mX = 1, float mY = 1)
            => new (v.x * mX, v.y * mY);
    }
}
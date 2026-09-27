using UnityEngine;

namespace Horcrux.Runtime.Utilities.Common
{
    public enum Direction
    {
        TopLeft,
        TopRight,
        TopCenter,
        BottomLeft,
        BottomRight,
        BottomCenter,
    }

    public static class DirectionExtensions
    {
        private static readonly float Diagonal = 1f / Mathf.Sqrt(2f);

        public static Vector2 GetDirectionVector(this Direction direction)
        {
            switch (direction)
            {
                case Direction.TopLeft : return new Vector2(-Diagonal, Diagonal);
                case Direction.TopRight : return new Vector2(Diagonal, Diagonal);
                case Direction.TopCenter : return Vector2.up;
                case Direction.BottomLeft : return new Vector2(-Diagonal, -Diagonal);
                case Direction.BottomRight : return new Vector2(Diagonal, -Diagonal);
                default : return Vector2.down;
            }
        }
        
        public static float GetEulerAngleZ(this Direction direction)
        {
            Vector2 dir = direction.GetDirectionVector();
            return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }
    }
}
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class ColorExtensions
    {
        /// <summary>
        /// Lerp this color to target color by ratio
        /// </summary>
        /// <param name="target">Target color</param>
        /// <param name="ratio">Ratio to lerp</param>
        /// <returns>New lerped color</returns>
        public static Color Blend(this Color self, Color target, float ratio)
        {
            ratio = Mathf.Clamp01(ratio);
            float r = self.r + (target.r - self.r) * ratio;
            float g = self.g + (target.g - self.g) * ratio;
            float b = self.b + (target.b - self.b) * ratio;
            float a = self.a + (target.a - self.a) * ratio;
            return new Color(r, g, b, a);
        }
        
        public static Color ToColor(this string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color result))
                return result;
            
            throw new System.Exception($"Invalid hex color string: {hex}");
        }
        
        public static string ToHex(this Color color)
            => $"{ColorUtility.ToHtmlStringRGBA(color)}";

        /// <summary>
        /// Pack a <see cref="Color"/> into a 32-bit unit.
        /// </summary>
        /// <remarks>
        /// The color channels are packed in little-endian order: R is the lowest 8 bits (0-7), A is the highest 8 bits (24-31).<br/>
        /// Unpacking must strictly follow this order to avoid color shifting.<br/>
        /// Highly optimized for sending color data to GPU buffers.
        /// </remarks>
        /// <returns>A 32-bit uint containing the packed Color data</returns>
        public static uint PackColor(this Color self)
        {
            Color32 c = self;
            return (uint)(c.r | (c.g << 8) | (c.b << 16) | (c.a << 24));
        }
    }
}
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class ColorExtensions
    {
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
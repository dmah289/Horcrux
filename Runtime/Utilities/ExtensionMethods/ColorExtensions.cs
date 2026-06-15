using UnityEngine;

namespace manhnd_sdk.Scripts.ExtensionMethods
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

        /// <summary>
        /// Clamp each channel of this color to [0,1]
        /// </summary>
        public static Color Clamp01(this Color self)
            => new Color
            {
                r = Mathf.Clamp01(self.r),
                g = Mathf.Clamp01(self.g),
                b = Mathf.Clamp01(self.b),
                a = Mathf.Clamp01(self.a)
            };
        
        /// <summary>
        /// Add two colors and clamp each channel to [0,1]
        /// </summary>
        public static Color Add(this Color self, Color other)
            => (self + other).Clamp01();

        /// <summary>
        /// Subtract two colors and clamp each channel to [0,1]
        /// </summary>
        public static Color Subtract(this Color self, Color other)
            => (self - other).Clamp01();

        public static Color Invert(this Color self)
            => new(1 - self.r, 1 - self.g, 1 - self.b, self.a);

        /// <summary>
        /// Convert hex string to Color
        /// </summary>
        public static Color ToColor(this string hex)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color result))
                return result;
            
            throw new System.Exception($"Invalid hex color string: {hex}");
        }

        /// <summary>
        /// Convert Color to hex string
        /// </summary>
        public static string ToHex(this Color color)
            => $"{ColorUtility.ToHtmlStringRGBA(color)}";
    }
}
using System;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Utilities
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = true)]
    public sealed class SplitterAttribute : PropertyAttribute
    {
        public readonly string Title;
        public readonly Color AccentColor;

        public SplitterAttribute(string title = "")
        {
            Title = title;
            AccentColor = GenerateAccentColor(title);
        }

        private static Color GenerateAccentColor(string seed)
        {
            int hash = StableHash(seed);
            var rng = new System.Random(hash);
            float h = (float)rng.NextDouble();
            float s = 0.5f + (float)rng.NextDouble() * 0.3f;
            float v = 0.7f + (float)rng.NextDouble() * 0.3f;
            return Color.HSVToRGB(h, s, v);
        }

        private static int StableHash(string str)
        {
            if (string.IsNullOrEmpty(str)) return 0;

            unchecked
            {
                int hash = 17;
                for (int i = 0; i < str.Length; i++)
                    hash = hash * 31 + str[i];
                return hash;
            }
        }
    }
}
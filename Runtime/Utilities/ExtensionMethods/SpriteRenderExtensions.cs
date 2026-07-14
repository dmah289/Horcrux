using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class SpriteRenderExtensions
    {
        public static void SetAlpha(this SpriteRenderer renderer, float alpha)
        {
            if (renderer == null)
                return;

            Color color = renderer.color;
            color.a = alpha;
            renderer.color = color;
        }
    }
}
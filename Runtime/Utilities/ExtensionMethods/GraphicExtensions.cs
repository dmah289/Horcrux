using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class GraphicExtensions
    {
        public static void SetAlpha(this Graphic self, float alpha)
        {
            if (self == null)
                return;

            Color color = self.color;
            color.a = alpha;
            self.color = color;
        }
    }
}
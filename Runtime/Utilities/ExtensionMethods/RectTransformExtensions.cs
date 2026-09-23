using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class RectTransformExtensions
    {
        public static void GetLocalPosYIn(this RectTransform self, RectTransform parent, out float yMin, out float yMax)
        {
            Rect rect = self.rect;
            float a = parent.InverseTransformPoint(self.TransformPoint(new Vector3(rect.xMin, rect.yMin))).y;
            float b = parent.InverseTransformPoint(self.TransformPoint(new Vector3(rect.xMin, rect.yMax))).y;
            
            yMin = Mathf.Min(a, b);
            yMax = Mathf.Max(a, b);
        }
    }
}
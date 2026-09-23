using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class ScrollRectExtensions
    {
        public static void SnapVerticalToBottom(this ScrollRect self)
        {
            
        }

        public static void SnapVertical(this ScrollRect self, RectTransform item,
            float itemPivot, float viewPortPivot)
        {
            RectTransform contentRT = self.content;
            RectTransform viewportRT = self.viewport != null 
                ? self.viewport : (RectTransform)self.transform;
            
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRT);
            
            float scrollableDist = contentRT.rect.height - viewportRT.rect.height;
            if (scrollableDist <= 0f)
            {
                self.verticalNormalizedPosition = 1f;
                return;
            }
            
            item.GetLocalPosYIn(contentRT, out float yMin, out float yMax);
            float itemLocalPosYInContent = Mathf.Lerp(yMin, yMax, itemPivot);
            float itemToTopContentDist = contentRT.rect.yMax - itemLocalPosYInContent -
                                         (1f - viewPortPivot) * viewportRT.rect.height;

        }
    }
}
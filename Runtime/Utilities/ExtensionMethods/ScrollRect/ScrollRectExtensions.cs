using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class ScrollRectExtensions
    {
        /// <summary>
        /// Calculate how far below the top content the viewport must be positioned
        /// to align the itemPivot with the viewportPivot.
        /// </summary>
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
            float topContentToItemPivotDist = contentRT.rect.yMax - itemLocalPosYInContent;
            float pivotViewToTopViewDist = (1f - viewPortPivot) * viewportRT.rect.height;
            float topViewToTopContentDist = topContentToItemPivotDist - pivotViewToTopViewDist;

            self.verticalNormalizedPosition = 1 - Mathf.Clamp01(topViewToTopContentDist / scrollableDist);
        }
    }
}
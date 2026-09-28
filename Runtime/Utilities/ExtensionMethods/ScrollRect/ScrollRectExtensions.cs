using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Tweening.Easing;
using Horcrux.Runtime.Utilities.Tweening;
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
        public static void SnapVertical(this ScrollRect self, RectTransform target,
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
            
            target.GetLocalPosYIn(contentRT, out float yMin, out float yMax);
            float itemLocalPosYInContent = Mathf.Lerp(yMin, yMax, itemPivot);
            float topContentToItemPivotDist = contentRT.rect.yMax - itemLocalPosYInContent;
            float pivotViewToTopViewDist = (1f - viewPortPivot) * viewportRT.rect.height;
            float topViewToTopContentDist = topContentToItemPivotDist - pivotViewToTopViewDist;

            self.verticalNormalizedPosition = 1 - Mathf.Clamp01(topViewToTopContentDist / scrollableDist);
        }

        public static async UniTask ScrollToTarget(this ScrollRect self, RectTransform targetItem, float itemPivot,
            float viewPortPivot, float duration, CancellationToken ct, EaseType ease = EaseType.InCubic)
        {
            float from = self.verticalNormalizedPosition;
            self.SnapVertical(targetItem, itemPivot, viewPortPivot);
            float to = self.verticalNormalizedPosition;
            self.verticalNormalizedPosition = from;

            if (Mathf.Approximately(from, to))
                return;
            
            self.velocity = Vector2.zero;
            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);

            void spell(float t)
            {
                self.verticalNormalizedPosition = Mathf.Lerp(from, to, t);
            }
            
            void onComplete()
            {
                self.verticalNormalizedPosition = to;
            }
        }
        
        public static async UniTask ScrollToTarget(this ScrollRect self, RectTransform fromItem, RectTransform targetItem,
            float itemPivot, float viewPortPivot, float duration, CancellationToken ct,
            EaseType ease = EaseType.InCubic)
        {
            self.SnapVertical(targetItem, itemPivot, viewPortPivot);
            float to = self.verticalNormalizedPosition;
            self.SnapVertical(fromItem,  itemPivot, viewPortPivot);
            float from = self.verticalNormalizedPosition;

            if (Mathf.Approximately(from, to))
                return;
            
            self.velocity = Vector2.zero;
            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);

            void spell(float t)
            {
                self.verticalNormalizedPosition = Mathf.Lerp(from, to, t);
            }
            
            void onComplete()
            {
                self.verticalNormalizedPosition = to;
            }
        }
        
        public static async UniTask ScrollToTarget(this ScrollRect self, float fromNormPos, RectTransform targetItem,
            float itemPivot, float viewPortPivot, float duration, CancellationToken ct,
            EaseType ease = EaseType.InCubic)
        {
            self.SnapVertical(targetItem, itemPivot, viewPortPivot);
            float to = self.verticalNormalizedPosition;
            self.verticalNormalizedPosition = fromNormPos;

            if (Mathf.Approximately(fromNormPos, to))
                return;
            
            self.velocity = Vector2.zero;
            await CharmTween.CastAsync(duration, ease, spell, ct, onComplete);

            void spell(float t)
            {
                self.verticalNormalizedPosition = Mathf.Lerp(fromNormPos, to, t);
            }
            
            void onComplete()
            {
                self.verticalNormalizedPosition = to;
            }
        }
    }
}
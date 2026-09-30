using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Utilities.Common;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.LiveOps
{
    public readonly struct HighlightConfig
    {
        public readonly bool ShowHand;
        public readonly Direction HandDirection;
        public readonly float HandTargetOffset;
        public readonly float TargetPadding;

        public static readonly HighlightConfig NoHand = new(false, Direction.BottomCenter, 0, 0f);

        public HighlightConfig(bool showHand, Direction handDirection, float targetPadding, float handTargetOffset)
        {
            ShowHand = showHand;
            HandDirection = handDirection;
            HandTargetOffset = handTargetOffset;
            TargetPadding = targetPadding;
        }
        
        public HighlightConfig(Direction handDirection, float targetPadding, float handTargetOffset)
            : this(true, handDirection, targetPadding, handTargetOffset) { }
    }
    
    public interface ICanvasSpotlight
    {
        bool IsFocusing { get; }
        void Focus(Canvas target, in HighlightConfig config);
        /// <summary>
        /// Hand anchors to the FIRST one
        /// </summary>
        void Focus(Canvas target, Canvas extra, in HighlightConfig config);
        /// <summary>
        /// Taps stay swallowed until Release.
        /// </summary>
        UniTask FadeDimAsync(float toAlpha, float duration, CancellationToken ct);
        void Release();
    }
}
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
    
    public interface ICanvasHighlightTutorial
    {
        bool IsFocusing { get; }
        void Focus(Canvas target, in HighlightConfig config);
        public void Release();
    }
}
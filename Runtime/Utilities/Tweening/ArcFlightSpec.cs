using System;
using Horcrux.Runtime.Tweening.Easing;

namespace Horcrux.Runtime.Utilities.Tweening
{
    [Serializable]
    public struct ArcFlightSpec
    {
        public float controlOffset;
        public float duration;
        public EaseType progressEase;
        public float startScale;
        public float endScale;
        public EaseType scaleEase;
    }

    [Serializable]
    public struct SquashStretchFlightSpec
    {
        public float controlOffset;
        public float progressDuration;
        public EaseType progressEase;
        public float startScale;
        public float endScale;
        public bool recoverAfterLanding;
        public float recoverScale;
        public float recoverDuration;
    }
}
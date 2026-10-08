using System;
using PrimeTween;

namespace Horcrux.Runtime.Utilities.Tweening
{
    [Serializable]
    public struct SquashStretchSpec
    {
        public float startScale;
        public float endScale;
        public float duration;
        public Ease ease;
    }
}
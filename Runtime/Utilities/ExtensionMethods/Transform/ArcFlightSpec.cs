using System;
using PrimeTween;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.Tweening
{
    [Serializable]
    public struct ArcFlightSpec
    {
        public float controlOffset;
        [Min(0.01f)] public float speed;
        [Min(0.1f)] public float controlAtRatio;
        public Ease progressEase;
    }
}
using System;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Composites.LiveOps
{
    /// <summary>
    /// [StartUnix, EndUnix)
    /// </summary>
    public readonly struct LiveOpsWindow
    {
        public readonly long StartUnix;
        public readonly long EndUnix;

        public LiveOpsWindow(long startUnix, long endUnix)
        {
            StartUnix = startUnix;
            EndUnix = endUnix;
        }
        
        public bool Contains(long nowUnix)
            => nowUnix >= StartUnix && nowUnix <= EndUnix;

        public long SecondsLeft(long nowUnix)
            => Math.Max(0, EndUnix - nowUnix);
    }
}
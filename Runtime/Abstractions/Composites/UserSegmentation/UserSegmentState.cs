using System;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>
    /// Save model. Field names are the wire format; 0 in <see cref="CurrSegmentId"/> means undecided.
    /// </summary>
    [Serializable]
    public sealed class UserSegmentState
    {
        public int CurrSegmentId;
        /// <summary>
        /// Segment right before the last change; 0 until the first change after Initial.
        /// </summary>
        public int PrevSegmentId;
        /// <summary>Rule that set the current segment; None in saves older than this field.</summary>
        public UserSegmentChangeReason LastChangeReason;
        public int SessionCount;
        public string LastCampaign;
    }
}

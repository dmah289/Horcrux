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
        /// <summary>Segment the user was in right before the purchased segment; only written on purchase.</summary>
        public int PrevSegmentId;
        /// <summary>True once attribution answered, a purchase happened, or the resegment window closed.</summary>
        public bool IsCampaignLocked;
        public int SessionCount;
        public string LastCampaign;
    }
}
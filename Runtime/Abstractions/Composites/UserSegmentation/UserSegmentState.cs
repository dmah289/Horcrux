using System;
using Newtonsoft.Json;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Why the campaign window closed. Ints are the save wire format: append only.</summary>
    public enum UserSegmentLockReason
    {
        None = 0,
        Attribution = 1,
        Purchase = 2,
        /// <summary>The first session's attribution poll ran out. Was SessionWindow; same int, old saves read as this.</summary>
        NoAttribution = 3
    }

    /// <summary>
    /// Save model. Field names are the wire format; 0 in <see cref="CurrSegmentId"/> means undecided.
    /// </summary>
    [Serializable]
    public sealed class UserSegmentState
    {
        public int CurrSegmentId;
        /// <summary>Segment the user was in right before the purchased segment; only written on purchase.</summary>
        public int PrevSegmentId;
        /// <summary>First reason the campaign window closed; None while it is open.</summary>
        public UserSegmentLockReason LockReason;
        public int SessionCount;
        public string LastCampaign;

        /// <summary>Derived from <see cref="LockReason"/>; not saved.</summary>
        [JsonIgnore] public bool IsCampaignLocked => LockReason != UserSegmentLockReason.None;
    }
}

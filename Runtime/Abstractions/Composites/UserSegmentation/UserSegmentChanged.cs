using Horcrux.Runtime.Utilities.EventBus;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    public enum UserSegmentChangeReason
    {
        Initial = 1,
        CampaignResolved = 2,
        Purchase = 3
    }
    
    /// <summary>Published synchronously after the new state is written to the save entry.</summary>
    public readonly struct UserSegmentChanged : IEvent
    {
        public readonly int PrevSegmentId;
        public readonly int CurrentSegmentId;
        public readonly UserSegmentChangeReason Reason;
        /// <summary>Keyword that decided the segment; null for Initial and Purchase.</summary>
        public readonly string MatchedKeyword;
        /// <summary>True when a row of another segment also matched the campaign.</summary>
        public readonly bool IsAmbiguous;

        public UserSegmentChanged(int prevSegmentId, int currentSegmentId, UserSegmentChangeReason reason, 
            string matchedKeyword, bool isAmbiguous)
        {
            PrevSegmentId = prevSegmentId;
            CurrentSegmentId = currentSegmentId;
            Reason = reason;
            MatchedKeyword = matchedKeyword;
            IsAmbiguous = isAmbiguous;
        }
    }
}
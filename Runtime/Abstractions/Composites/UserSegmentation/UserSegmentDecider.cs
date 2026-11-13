namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Every business rule, as static functions that mutate the given state and return the change. Segment ids are ints; 0 means undecided.</summary>
    public static class UserSegmentDecider
    {
        /// <summary>What one rule did to the state. <see cref="None"/> (HasChange false) when the current segment stayed.</summary>
        public readonly struct Change
        {
            public readonly bool HasChange;
            public readonly int PrevSegmentId;
            public readonly int CurrSegmentId;
            public readonly UserSegmentChangeReason Reason;
            public readonly string MatchedKeyword;
            public readonly bool IsAmbiguous;

            public static readonly Change None = default;

            public Change(int prevSegmentId, int currSegmentId, UserSegmentChangeReason reason, string matchedKeyword, bool isAmbiguous)
            {
                HasChange = true;
                PrevSegmentId = prevSegmentId;
                CurrSegmentId = currSegmentId;
                Reason = reason;
                MatchedKeyword = matchedKeyword;
                IsAmbiguous = isAmbiguous;
            }
        }

        /// <summary>
        /// Cold boot: decide the default once, count the session.
        /// </summary>
        public static Change OnBoot(UserSegmentState state, int defaultSegmentId)
        {
            Change change = Change.None;

            if (state.CurrSegmentId == 0)
            {
                // Unity never serializes a null string: an asset-authored save default holds "". Undecided means not reported yet.
                state.LastCampaign = null;
                change = MoveTo(state, defaultSegmentId, UserSegmentChangeReason.Initial, null, false);
            }

            state.SessionCount++;
            return change;
        }

        /// <summary>
        /// True while the initial input still decides the segment.
        /// Any other reason, present or future, is behavior and wins over a campaign.
        /// </summary>
        public static bool AcceptsCampaign(UserSegmentState state) => state.LastChangeReason is 
            UserSegmentChangeReason.None or UserSegmentChangeReason.CampaignResolved 
            or UserSegmentChangeReason.Initial;

        /// <summary>
        /// Attribution answered. <paramref name="matchedSegmentId"/> is 0 when no row matched.
        /// Ignored (but recorded) once behavior set the segment.
        /// </summary>
        public static Change OnCampaign(UserSegmentState state, string campaign, int matchedSegmentId,
            string matchedKeyword, bool isAmbiguous, int defaultSegmentId, int purchasedSegmentId)
        {
            // Written even when ignored: tracking needs the user's real campaign.
            state.LastCampaign = campaign;

            if (!AcceptsCampaign(state))
                return Change.None;

            // A keyword row pointing at the purchased segment is a data error (the project's Simulator reports it; the core validator knows no roles): a campaign never grants that segment.
            if (matchedSegmentId == purchasedSegmentId)
                matchedSegmentId = 0;

            int target = matchedSegmentId != 0 ? matchedSegmentId : defaultSegmentId;

            return MoveTo(state, target, UserSegmentChangeReason.CampaignResolved, matchedKeyword, isAmbiguous);
        }

        /// <summary>
        /// A real purchase (not restore, not refund, not a remove-ads-only pack: the project filters those).
        /// </summary>
        public static Change OnPurchase(UserSegmentState state, int purchasedSegmentId)
            => MoveTo(state, purchasedSegmentId, UserSegmentChangeReason.Purchase, null, false);

        /// <summary>
        /// Debug only: the campaign sets the segment even after behavior, out of purchased too. No match lands on the default, like organic.
        /// </summary>
        public static Change OnForcedCampaign(UserSegmentState state, string campaign, int matchedSegmentId,
            string matchedKeyword, bool isAmbiguous, int defaultSegmentId)
        {
            state.LastCampaign = campaign;
            int target = matchedSegmentId != 0 ? matchedSegmentId : defaultSegmentId;
            return MoveTo(state, target, UserSegmentChangeReason.CampaignResolved, matchedKeyword, isAmbiguous);
        }

        /// <summary>The one write of the segment: prev, curr and reason move together. No-op when already there.</summary>
        private static Change MoveTo(UserSegmentState state, int target, UserSegmentChangeReason reason,
            string matchedKeyword, bool isAmbiguous)
        {
            if (target == state.CurrSegmentId)
                return Change.None;
            
            int prev = state.CurrSegmentId;
            state.PrevSegmentId = prev;
            state.CurrSegmentId = target;
            state.LastChangeReason = reason;
            return new Change(prev, target, reason, matchedKeyword, isAmbiguous);
        }
    }
}
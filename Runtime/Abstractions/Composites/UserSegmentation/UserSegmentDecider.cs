using System;

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

        /// <summary>Cold boot: decide the default once, close the resegment window after N sessions, count the session.</summary>
        public static Change OnBoot(UserSegmentState state, int defaultSegmentId, int resegmentMaxSessions)
        {
            Change change = Change.None;

            if (state.CurrSegmentId == 0)
            {
                state.CurrSegmentId = defaultSegmentId;
                change = new Change(0, defaultSegmentId, UserSegmentChangeReason.Initial, null, false);
            }

            // Checked before the increment: sessions 1..N can still resegment, the window closes at the start of N+1.
            int maxSessions = Math.Max(1, resegmentMaxSessions);

            if (!state.IsCampaignLocked && state.SessionCount >= maxSessions)
                Lock(state, UserSegmentLockReason.SessionWindow);

            state.SessionCount++;
            return change;
        }

        /// <summary>Attribution answered. <paramref name="matchedSegmentId"/> is 0 when no row matched.</summary>
        public static Change OnCampaign(UserSegmentState state, string campaign, int matchedSegmentId,
            string matchedKeyword, bool isAmbiguous, int defaultSegmentId, int purchasedSegmentId)
        {
            // Written even when locked: tracking needs the user's real campaign.
            state.LastCampaign = campaign;

            if (state.IsCampaignLocked)
                return Change.None;

            Lock(state, UserSegmentLockReason.Attribution);

            if (state.CurrSegmentId == purchasedSegmentId)
                return Change.None;

            // A keyword row pointing at the purchased segment is a data error (the project's Simulator reports it; the core validator knows no roles): a campaign never grants that segment.
            if (matchedSegmentId == purchasedSegmentId)
                matchedSegmentId = 0;

            int target = matchedSegmentId != 0 ? matchedSegmentId : defaultSegmentId;

            if (target == state.CurrSegmentId)
                return Change.None;

            int prev = state.CurrSegmentId;
            state.CurrSegmentId = target;
            return new Change(prev, target, UserSegmentChangeReason.CampaignResolved, matchedKeyword, isAmbiguous);
        }

        /// <summary>A real purchase (not restore, not refund, not a remove-ads-only pack: the project filters those).</summary>
        public static Change OnPurchase(UserSegmentState state, int defaultSegmentId, int purchasedSegmentId)
        {
            if (state.CurrSegmentId == purchasedSegmentId)
                return Change.None;

            int prev = state.CurrSegmentId != 0 ? state.CurrSegmentId : defaultSegmentId;
            state.PrevSegmentId = prev;
            state.CurrSegmentId = purchasedSegmentId;
            Lock(state, UserSegmentLockReason.Purchase);

            return new Change(prev, purchasedSegmentId, UserSegmentChangeReason.Purchase, null, false);
        }

        /// <summary>The first reason wins: a lock means the resegment window closed, later causes do not rewrite why.</summary>
        private static void Lock(UserSegmentState state, UserSegmentLockReason reason)
        {
            if (state.LockReason == UserSegmentLockReason.None)
                state.LockReason = reason;
        }
    }
}
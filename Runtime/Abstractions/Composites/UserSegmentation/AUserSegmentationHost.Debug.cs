using UnityEngine;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Debug commands that touch private state. Implemented explicitly: a stray call wipes the save silently.</summary>
    public interface IUserSegmentationDebug
    {
        /// <summary>
        /// Back to undecided and flushed. CurrConfig keeps the old row: the caller must quit the app.
        /// </summary>
        void ResetState();

        /// <summary>
        /// Segment from the campaign's keywords, even after behavior, out of purchased too. Config rebuilt and change published at once.
        /// </summary>
        void ForceCampaign(string campaign);
    }

    public abstract partial class AUserSegmentationHost : IUserSegmentationDebug
    {
        void IUserSegmentationDebug.ResetState()
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ResetState before InitializeAsync", this);
                return;
            }

            // Both, not only the entry: a late Commit writes _state back, and the old object would undo the reset.
            _state = new UserSegmentState();
            _entry.Value = _state;
            _entry.FlushNow();
            Debug.Log(LogPrefix + "state reset to undecided", this);
        }

        void IUserSegmentationDebug.ForceCampaign(string campaign)
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ForceCampaign before InitializeAsync", this);
                return;
            }

            UserSegmentKeywordMatcher.TryMatch(campaign, KeywordRules, out int segmentId, out string keyword, out bool isAmbiguous);
            UserSegmentDecider.Change change = UserSegmentDecider.OnForcedCampaign(_state, campaign, segmentId, keyword,
                isAmbiguous, DefaultSegmentId);

            if (!change.HasChange)
            {
                Debug.Log(LogPrefix + $"forced campaign '{campaign}' resolves to the current segment {_state.CurrSegmentId}", this);
            }
            
            Commit(change);
        }
    }
}

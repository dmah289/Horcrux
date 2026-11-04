using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Utilities;
using Horcrux.Runtime.Utilities.EventBus;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>
    /// Boot step owning the segment state. Two write gates: <see cref="ReportCampaign"/> and <see cref="ReportPurchase"/>.
    /// Must run after <c>SaveBootstep</c>. Not generic so vendor steps and Editor tools can reference it.
    /// Segment ids are ints from the project's data; the base only knows the default and the purchased roles.
    /// </summary>
    public abstract partial class AUserSegmentationHost : ABootStep, IInitializable<BasePersistenceDataCollection>
    {
        protected const string LogPrefix = "[UserSegmentation]: ";

        private PersistenceDataEntry<UserSegmentState> _entry;
        private UserSegmentState _state;

        #region Properties

        /// <summary>False until <see cref="InitializeAsync"/> ran with an initialized collection. While false, derived config is null: a missing setup must surface, not run at a default.</summary>
        public bool IsReady => _state != null;
        public int CurrSegmentId => _state?.CurrSegmentId ?? 0;
        public int PrevSegmentId => _state?.PrevSegmentId ?? 0;
        public bool IsCampaignLocked => _state != null && _state.IsCampaignLocked;
        public bool IsFirstSession => _state != null && _state.SessionCount == 1;
        public string LastCampaign => _state?.LastCampaign;
        /// <summary>Cold boots since install, this one included; 0 before <see cref="InitializeAsync"/>.</summary>
        public int SessionCount => _state?.SessionCount ?? 0;
        public UserSegmentLockReason LockReason => _state?.LockReason ?? UserSegmentLockReason.None;

        /// <summary>Segment a user lands in without a matching campaign. Must have a pool row.</summary>
        protected abstract int DefaultSegmentId { get; }
        /// <summary>Segment entered on purchase only; its pool row may inherit with -1. Must have a pool row.</summary>
        protected abstract int PurchasedSegmentId { get; }
        /// <summary>Row order is priority. Read at boot and on each campaign report, never cached here.</summary>
        protected abstract IReadOnlyList<IUserSegmentKeywordRule> KeywordRules { get; }
        /// <summary>Cold boots that may still resegment while attribution has not answered. Override only for a reason; values below 1 clamp to 1.</summary>
        protected virtual int ResegmentMaxSessions => 3;

        #endregion

        #region API

        public override UniTask InitializeAsync(CancellationToken ct)
        {
            Initialize();
            return UniTask.CompletedTask;
        }

        /// <summary>Attribution answered. Pass empty for organic. Locks the campaign; ignored (but recorded) once locked.</summary>
        public void ReportCampaign(string campaign)
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ReportCampaign before InitializeAsync; check step order", this);
                return;
            }

            bool wasLocked = _state.IsCampaignLocked;
            UserSegmentKeywordMatcher.TryMatch(campaign, KeywordRules, out int segmentId, out string keyword,
                out bool isAmbiguous);
            UserSegmentDecider.Change change = UserSegmentDecider.OnCampaign(_state, campaign, segmentId, keyword,
                isAmbiguous, DefaultSegmentId, PurchasedSegmentId);
            
            if(wasLocked)
                Debug.Log(LogPrefix + $"campaign '{campaign}' arrived after lock, segment stays {_state.CurrSegmentId}", this);
            
            Commit(change);
        }

        /// <summary>A real purchase. The project decides what counts (not restore, not refund, not remove-ads only).</summary>
        public void ReportPurchase()
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ReportPurchase before InitializeAsync; check step order", this);
                return;
            }

            Commit(UserSegmentDecider.OnPurchase(_state, DefaultSegmentId, PurchasedSegmentId));
        }

        #endregion

        #region Class Methods

        /// <summary>Typed side: LogError every invalid row once at boot (<see cref="UserSegmentConfigTable.Validate{TConfig}"/>). Never fix the data.</summary>
        protected abstract void ValidateConfigs();

        /// <summary>Called after boot and after every change of segment: rebuild the resolved config.</summary>
        protected abstract void OnStateApplied();

        private void Initialize()
        {
            if (saveCollection == null)
            {
                Debug.LogError(LogPrefix + "saveCollection was not injected; check the Init section of this step", this);
                return;
            }

            if (!saveCollection.IsInitialized)
            {
                Debug.LogError(LogPrefix + "save collection not initialized; SaveBootstep must have a lower order", this);
                return;
            }

            if (ResegmentMaxSessions < 1)
                Debug.LogWarning(LogPrefix + $"ResegmentMaxSessions = {ResegmentMaxSessions}, clamped to 1", this);

            _state = _entry.Value;
            
            ValidateConfigs();
            // Forced: a warm boot changes nothing, yet CurrConfig starts null every launch and must be built.
            Commit(UserSegmentDecider.OnBoot(_state, DefaultSegmentId, ResegmentMaxSessions), forceApply: true);
        }

        /// <summary>Saves the state, rebuilds the config when needed, logs and publishes <see cref="UserSegmentChanged"/> only when the segment changed.</summary>
        /// <param name="forceApply">Rebuild the config even without a change. Never publishes on its own.</param>
        private void Commit(in UserSegmentDecider.Change change, bool forceApply = false)
        {
            // Through the setter on every call: SessionCount or LastCampaign may have changed without a segment change.
            _entry.Value = _state;

            if (!change.HasChange && !forceApply)
                return;
            
            OnStateApplied();

            if (!change.HasChange)
                return;
            
            Debug.Log(LogPrefix + $"{change.Reason}: segment {change.PrevSegmentId} -> {change.CurrSegmentId}"
                + (change.MatchedKeyword != null ? $" by keyword '{change.MatchedKeyword}'" : string.Empty)
                + (change.IsAmbiguous ? ", another segment's keyword also matched" : string.Empty), this);
            
            EventBus<UserSegmentChanged>.Publish(new UserSegmentChanged(change.PrevSegmentId, change.CurrSegmentId,
                change.Reason, change.MatchedKeyword, change.IsAmbiguous));
        }

        #endregion

        #region DI

        private BasePersistenceDataCollection saveCollection;
        
        public void Init(BasePersistenceDataCollection argument)
        {
            saveCollection = argument;

            // Safe before the collection loads: loading writes into this same entry object, never replaces it.
            _entry = saveCollection.UserSegmentState;
        }

        #endregion
    }
}
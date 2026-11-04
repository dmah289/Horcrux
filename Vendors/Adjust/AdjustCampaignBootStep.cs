using System;
using System.Threading;
using AdjustSdk;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Abstractions.UserSegmentation;
using Horcrux.Runtime.Utilities;
using UnityEngine;

namespace Horcrux.Vendors.Adjust
{
    /// <summary>
    /// Feeds the Adjust campaign into the segmentation host. Polls <see cref="AdjustSdk.Adjust.GetAttribution"/> because
    /// the SDK offers no event once another system (the project's Adjust wrapper) owns the init config.
    /// First session waits up to <see cref="firstSessionWaitSeconds"/>; later sessions only poll in the background.
    /// </summary>
    public sealed class AdjustCampaignBootStep : ABootStep
    {
        private const string LogPrefix = "[AdjustCampaign]: ";
        private const float PollIntervalSeconds = 0.5f;

        [Splitter("References")]
        [SerializeField] private AUserSegmentationHost segmentation;

        [Splitter("Configs")]
        [SerializeField, Min(0f), Tooltip("First session: wait for attribution at most this long before entering the game")]
        private float firstSessionWaitSeconds = 3f;
        [SerializeField, Min(0f), Tooltip("Keep polling in the background at most this long per session while the campaign is not locked")]
        private float lateArrivalPollSeconds = 10f;

        private AdjustAttribution _attribution;
        private bool _hasAttribution;

        #region API

        public override async UniTask InitializeAsync(CancellationToken ct)
        {
            if (segmentation == null)
            {
                Debug.LogError(LogPrefix + "segmentation is empty; drag the segmentation host", this);
                return;
            }

            if (!segmentation.IsReady)
            {
                Debug.LogError(LogPrefix + "segmentation host not initialized; its order must be lower than this step", this);
                return;
            }

            if (segmentation.IsCampaignLocked)
                return;

            if (segmentation.IsFirstSession && await PollAsync(firstSessionWaitSeconds, ct))
                return;

            // Own lifetime, not the boot token: the boot phase ends when this step returns.
            PollInBackgroundAsync(destroyCancellationToken).Forget();
        }

        #endregion

        #region Class Methods

        /// <summary>True when the campaign got locked, by this poll or by another report, within <paramref name="maxSeconds"/>.</summary>
        private async UniTask<bool> PollAsync(float maxSeconds, CancellationToken ct)
        {
            float deadline = Time.realtimeSinceStartup + maxSeconds;

            while (Time.realtimeSinceStartup < deadline)
            {
                if (segmentation.IsCampaignLocked)
                    return true;

                // Fully qualified: inside Horcrux.Vendors.Adjust, "Adjust" resolves to the namespace.
                // Method group into a field, never awaited: the SDK may skip the callback while it has no attribution.
                AdjustSdk.Adjust.GetAttribution(OnAttribution);

                await UniTask.Delay(TimeSpan.FromSeconds(PollIntervalSeconds), DelayType.Realtime, cancellationToken: ct);

                if (!_hasAttribution)
                    continue;

                segmentation.ReportCampaign(_attribution.Campaign ?? string.Empty);

                return true;
            }

            return false;
        }

        private void OnAttribution(AdjustAttribution attribution)
        {
            _attribution = attribution;
            _hasAttribution = attribution != null;
        }

        /// <summary>Late attribution: polls up to <see cref="lateArrivalPollSeconds"/>; not locked by then means the next session retries.</summary>
        private async UniTaskVoid PollInBackgroundAsync(CancellationToken ct)
        {
            try
            {
                if (await PollAsync(lateArrivalPollSeconds, ct))
                    return;

                Debug.Log(LogPrefix + $"no attribution within {lateArrivalPollSeconds}s; next session retries", this);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        #endregion
    }
}
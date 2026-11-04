using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>
    /// The project subclasses this with its pool row DTO and hands over the typed pool it reads from its own remote config.
    /// </summary>
    public abstract class AUserSegmentationHost<TConfig> : AUserSegmentationHost
        where TConfig : class, IUserSegmentConfig<TConfig>
    {
        // Cached, not computed in the getter: consumers read it many times per level and InheritFrom allocates a new row.
        private TConfig _currConfig;

        #region Properties

        /// <summary>Resolved row of the current segment; for the purchased segment, -1 fields are taken from the previous segment's row. Null only while <see cref="AUserSegmentationHost.IsReady"/> is false.</summary>
        public TConfig CurrConfig => _currConfig;
        /// <summary>Segment pool: one row per segment id, ids from 1. Read at boot and on each change, never cached here.</summary>
        protected abstract IReadOnlyList<TConfig> Configs { get; }

        #endregion

        #region Class Methods

        protected sealed override void OnStateApplied()
        {
            TConfig target = FindConfigOrDefault(CurrSegmentId);

            if (CurrSegmentId != PurchasedSegmentId)
            {
                _currConfig = target;
                return;
            }
            
            int prevId = PrevSegmentId != 0 ? PrevSegmentId : DefaultSegmentId;
            _currConfig = target.InheritFrom(FindConfigOrDefault(prevId));
        }

        protected sealed override void ValidateConfigs()
        {
            List<string> errors = new();
            UserSegmentConfigTable.Validate(Configs, KeywordRules, DefaultSegmentId, errors);
            
            for (int i = 0; i < errors.Count; i++)
                Debug.LogError(LogPrefix + errors[i], this);
        }

        /// <summary>Row of <paramref name="id"/>, else the default segment's row with a LogError, else throws.</summary>
        private TConfig FindConfigOrDefault(int id)
        {
            TConfig row = UserSegmentConfigTable.Find(Configs, id);

            if (row != null)
                return row;
            
            Debug.LogError(LogPrefix + $"no pool row for segment {id}, using the default segment's row", this);
            row = UserSegmentConfigTable.Find(Configs, DefaultSegmentId);
            
            if (row != null)
                return row;

            // An empty row would be a zero economy (interstitial from level 1, free boosters) running silently: an authoring error must explode.
            throw new InvalidOperationException(LogPrefix + $"no pool row for segment {id} nor for the default segment " +
                                                $"{DefaultSegmentId}, the segment pool on the remote config asset is empty or wrong");
        }

        #endregion
        
    }
}
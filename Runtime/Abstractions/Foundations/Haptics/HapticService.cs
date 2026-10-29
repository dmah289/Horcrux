using System;
using System.Collections.Generic;
using Horcrux.Runtime.Utilities;
using Lofelt.NiceVibrations;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Haptics
{
    public abstract class HapticService<THaptic> : MonoBehaviour, IHapticService<THaptic>
        where THaptic : struct, Enum
    {
        [Splitter("Configs")]
        [SerializeField] private HapticEntry<THaptic>[] entries =  Array.Empty<HapticEntry<THaptic>>();
        
        private readonly Dictionary<THaptic, HapticPatterns.PresetType> _presetsById = new();

        public bool IsHapticEnabled { get; set; }

        #region Unity Callbacks

        private void Awake()
        {
            HapticController.Init();
            BuildTables();
        }

        #endregion

        #region API

        public void Play(THaptic haptic)
        {
            if (!IsHapticEnabled)
                return;

            if (!_presetsById.TryGetValue(haptic, out HapticPatterns.PresetType preset))
            {
                Debug.LogError($"[HapticService]: Haptic {haptic} is not in the entries.", this);
                return;
            }
            
            HapticPatterns.PlayPreset(preset);
        }

        #endregion

        #region Class Methods

        private void BuildTables()
        {
            _presetsById.Clear();
            for(int i = 0; i < entries.Length; i++)
                _presetsById.Add(entries[i].Id, entries[i].Preset);
        }

        #endregion
    }
}
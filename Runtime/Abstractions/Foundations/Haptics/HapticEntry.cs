using System;
using Lofelt.NiceVibrations;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Haptics
{
    [Serializable]
    public class HapticEntry<THaptic> where THaptic : struct, Enum
    {
        [SerializeField] private THaptic id;
        [SerializeField] private HapticPatterns.PresetType preset;
        
        public THaptic Id => id;
        public HapticPatterns.PresetType Preset => preset;
    }
}
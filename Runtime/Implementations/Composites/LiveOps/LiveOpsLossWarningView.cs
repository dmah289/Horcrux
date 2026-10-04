using System;
using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Utilities;
using Sisus.Init;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    [Serializable]
    public sealed class LiveOpsLossWarningSlot
    {
        public GameObject Root;
        public Image Icon;
        public GameObject BadgeRoot;
        public TextMeshProUGUI BadgeLabel;
    }

    public class LiveOpsLossWarningView : MonoBehaviour<ILiveOpsHost>
    {
        private const int MaxSlots = 6;

        [Splitter("References")] 
        [SerializeField] private GameObject content;
        [SerializeField] private GameObject[] rows;
        [SerializeField] private LiveOpsLossWarningSlot[] slots;
        [SerializeField] private TextMeshProUGUI warningDescTxt;

        [Splitter("Configs")]
        [SerializeField, TextArea]
        private string sharedMultipleWarningDesc = "You will lose all your\n<color=#FF4A4A>Achievement Progress!</color>";

        private readonly List<ILiveOpsModule> _atStake = new(MaxSlots);


        #region Properties

        public bool IsShown { get; private set; }

        #endregion

        #region Unity Callbacks

        private void OnEnable()
        {
            Refresh();
        }

        #endregion

        #region Class Methods

        private void Refresh()
        {
            LiveOpsLossHelper.CollectProgressAtStake(_liveOpsHost.Modules, _atStake);
            IsShown = _atStake.Count > 0;
            content.SetActive(IsShown);

            if (!IsShown)
                return;

            if (_atStake.Count > slots.Length)
                Debug.LogError($"[LiveOpsLossWarningView]: {_atStake.Count} live-ops are at stake but there are only " +
                               $"{slots.Length} slots, so the lowest priorities are left out. Add slots.", this);
            
            int columnCount = slots.Length / rows.Length;

            for (int i = 0; i < rows.Length; i++)
                rows[i].SetActive(_atStake.Count > i * columnCount);

            for (int i = 0; i < slots.Length; i++)
            {
                bool isUsed = i < _atStake.Count;
                slots[i].Root.SetActive(isUsed);

                if (isUsed)
                    ShowWarning(slots[i], _atStake[i].LoseFlow.Warning);
            }
            
            warningDescTxt.SetText(_atStake.Count == 1 ? _atStake[0].LoseFlow.Warning.SingleWarningDesc
                : sharedMultipleWarningDesc);
        }
        
        private static void ShowWarning(LiveOpsLossWarningSlot slot, LiveOpsLossWarning warning)
        {
            slot.Icon.sprite = warning.Icon;
            slot.BadgeRoot.SetActive(!string.IsNullOrEmpty(warning.Badge));
            slot.BadgeLabel.text = warning.Badge;
        }

        #endregion
        
        #region DI

        private ILiveOpsHost _liveOpsHost;
        protected override void Init(ILiveOpsHost argument)
        {
            _liveOpsHost = argument;
        }

        #endregion
    }
}
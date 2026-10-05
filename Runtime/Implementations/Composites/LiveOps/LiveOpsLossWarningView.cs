using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Utilities;
using Sisus.Init;
using TMPro;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.LiveOps
{
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
                if (isUsed)
                    slots[i].SetUp(isUsed, _atStake[i].LoseFlow.Warning);
                else slots[i].Root.SetActive(false);
            }
            
            warningDescTxt.SetText(_atStake.Count == 1 ? _atStake[0].LoseFlow.Warning.SingleWarningDesc
                : sharedMultipleWarningDesc);
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
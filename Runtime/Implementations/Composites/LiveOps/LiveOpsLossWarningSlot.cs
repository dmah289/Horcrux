using System;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Utilities.ExtensionMethods;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    public sealed class LiveOpsLossWarningSlot : MonoBehaviour
    { 
        [SerializeField] private GameObject root;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI badgeLabel;
        
        public GameObject Root => root;
        
        public void SetUp(bool isUsed, LiveOpsLossWarning warning)
        {
            root.SetActive(isUsed);
            
            icon.sprite = warning.Icon;
            badgeLabel.SetActive(!string.IsNullOrEmpty(warning.Badge));
            badgeLabel.text = warning.Badge;
        }
    }
}
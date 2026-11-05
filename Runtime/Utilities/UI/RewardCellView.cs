using LightSide;
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    public class RewardCellView : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] private Image icon;
        [SerializeField] private UniText amountLabel;

        #region API

        /// <summary>Draws one reward. A null amountText hides the label.</summary>
        public void Set(Sprite iconSprite, string amountText)
        {
            icon.sprite = iconSprite;
            amountLabel.gameObject.SetActive(amountText != null);

            if (amountText != null)
                amountLabel.SetText(amountText);
        }

        #endregion
    }
}
using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    public class Tooltip : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] private RectTransform bubble;
        [SerializeField] private Button dismissBtn;

        #region API

        /// <summary>Puts the bubble pivot on a world position and shows it until the dismiss button is tapped.</summary>
        public void Show(Vector3 anchorWorldPos, Transform parent = null)
        {
            bubble.gameObject.SetActive(true);
            bubble.position = anchorWorldPos;
            bubble.SetParent(parent);
            gameObject.SetActive(true);
        }

        public void Hide()
        {
            bubble.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        #endregion
    }
}
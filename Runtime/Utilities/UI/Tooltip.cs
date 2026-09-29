using UnityEngine;
using UnityEngine.UI;

namespace Horcrux.Runtime.Utilities.UI
{
    public class Tooltip : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] private RectTransform bubble;
        [SerializeField] private Button dismissBtn;

        #region Unity Callbacks

        private void Awake() => dismissBtn.onClick.AddListener(Hide);

        #endregion

        #region API

        /// <summary>Puts the bubble pivot on a world position and shows it until the dismiss button is tapped.</summary>
        public void Show(Vector3 anchorWorldPos)
        {
            bubble.position = anchorWorldPos;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        #endregion
    }
}
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    public class RewardListView : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] private RewardCellView[] cells;
        // One between each pair of cells: cells.Length - 1.
        [SerializeField] private GameObject[] separators;

        private int _shownCellsAmount;

        #region API

        public void Clear()
        {
            for (int i = 0; i < cells.Length; i++)
                cells[i].gameObject.SetActive(false);

            for (int i = 0; i < separators.Length; i++)
                separators[i].SetActive(false);

            _shownCellsAmount = 0;
        }

        /// <summary>Activates the next cell for the caller to draw. Null once every authored cell is shown.</summary>
        public RewardCellView AddCell()
        {
            if (_shownCellsAmount == cells.Length)
            {
                Debug.LogError($"[RewardListView]: Only {cells.Length} cells authored, cannot show more.", this);
                return null;
            }

            if (_shownCellsAmount > 0)
                separators[_shownCellsAmount - 1].SetActive(true);

            RewardCellView cell = cells[_shownCellsAmount++];
            cell.gameObject.SetActive(true);
            return cell;
        }

        #endregion
    }
}
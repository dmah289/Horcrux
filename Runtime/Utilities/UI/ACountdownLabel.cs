using System.Text;
using Horcrux.Runtime.Utilities.Common;
using TMPro;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.UI
{
    public abstract class ACountdownLabel : MonoBehaviour
    {
        [Splitter("References")]
        [SerializeField] protected TMP_Text label;
        
        protected readonly StringBuilder _sb = new(16);
        

        #region Unity Callbacks

        protected virtual void OnEnable()
        {
            WriteCountdown();
        }

        #endregion

        #region Class Methods
        
        protected abstract void WriteCountdown();

        protected void SetClockLabel(string text)
        {
            label.SetText(text);
        }

        protected void SetClockLabel(long seconds)
        {
            CountdownFormatter.Write(seconds, _sb);
            label.SetText(_sb);
        }

        #endregion
    }
}
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.UserSegmentation
{
    /// <summary>Debug commands that touch private state. Implemented explicitly: a stray call wipes the save silently.</summary>
    public interface IUserSegmentationDebug
    {
        /// <summary>Back to undecided and flushed. CurrConfig keeps the old row: the caller must quit the app.</summary>
        void ResetState();
    }

    public abstract partial class AUserSegmentationHost : IUserSegmentationDebug
    {
        void IUserSegmentationDebug.ResetState()
        {
            if (!IsReady)
            {
                Debug.LogError(LogPrefix + "ResetState before InitializeAsync", this);
                return;
            }

            // Both, not only the entry: a late Commit writes _state back, and the old object would undo the reset.
            _state = new UserSegmentState();
            _entry.Value = _state;
            _entry.FlushNow();
            Debug.Log(LogPrefix + "state reset to undecided", this);
        }
    }
}

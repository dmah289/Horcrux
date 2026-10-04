using UnityEngine;

namespace Horcrux.Runtime.Abstractions.LiveOps
{
    public readonly struct LiveOpsLossWarning
    {
        public readonly Sprite Icon;
        public readonly string Badge;
        /// <summary>
        /// Used if it is the only one at stake.
        /// </summary>
        public readonly string SingleWarning;
            
        public LiveOpsLossWarning(Sprite icon, string badge, string singleWarning)
        {
            Icon = icon;
            Badge = badge;
            SingleWarning = singleWarning;
        }
    }
    
    public abstract class ALiveOpsLoseFlow
    {
        public abstract bool IsProgressAtStake { get; }
        public abstract LiveOpsLossWarning Warning { get; }
        /// <summary>
        /// Lives in the module save and is written out at once. A new cycle clears it.
        /// </summary>
        protected abstract bool HasPendingLoss { get; set; }
        
        /// <summary>
        /// Take the progress away, must leave <see cref="IsProgressAtStake"/> false.
        /// </summary>
        protected abstract void ApplyLoss();

        public void BeginPendingLoss()
        {
            if (IsProgressAtStake && !HasPendingLoss)
                HasPendingLoss = true;
        }

        public void CancelPendingLoss()
        {
            if(HasPendingLoss)
                HasPendingLoss = false;
        }

        /// <summary>
        /// Clears the flag first: a loss with nothing at stake must not leave it for the next boot.
        /// </summary>
        public void CommitLoss()
        {
            CancelPendingLoss();
            
            if(IsProgressAtStake)
                ApplyLoss();
        }

        /// <summary>
        /// // No state check: at boot the module does not know the level yet, and a cycle roll already cleared a stale flag.
        /// </summary>
        public void RecoverPendingLoss()
        {
            if (!HasPendingLoss)
                return;

            HasPendingLoss = false;
            ApplyLoss();
        }
    }
}
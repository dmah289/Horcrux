using System.Threading;
using Cysharp.Threading.Tasks;

namespace Horcrux.Runtime.Abstractions.LiveOps
{
    public enum LiveOpsProgressChangePhase
    {
        /// <summary>
        /// Together with the other First flows, before anything else.
        /// </summary>
        First,
        /// <summary>
        /// Together with the other Second flows, once every First flow is done.
        /// </summary>
        Second,
        /// <summary>
        /// One after another by priority, alongside second.
        /// </summary>
        Parallel
    }
    
    /// <summary>
    /// 1 live-ops show 4 fixed stages on returning home page. Every stage runs on every pass.
    /// </summary>
    public abstract class ALiveOpsHomeFlow
    {
        public virtual LiveOpsProgressChangePhase ProgressChangePhase 
            => LiveOpsProgressChangePhase.Second;
        /// <summary>
        /// Play updates at home page, if any.
        /// </summary>
        public virtual UniTask PlayProgressChangeAsync(CancellationToken ct)
            => UniTask.CompletedTask;
        public virtual UniTask PlayTutorialAsync(CancellationToken ct)
            => UniTask.CompletedTask;
        /// <summary>
        /// Force review full progress view on a significant update.
        /// </summary>
        public virtual UniTask PlayForcedReviewAsync(CancellationToken ct)
            => UniTask.CompletedTask;
        /// <summary>
        /// Sale popups, once every live-ops has settled.
        /// </summary>
        public virtual UniTask PlayPromotionAsync(CancellationToken ct)
            => UniTask.CompletedTask;
    }
}
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Bootstrap
{
    /// <summary>One step of the game's init chain.</summary>
    public abstract class BootStep : MonoBehaviour
    {
        [SerializeField, Tooltip("Lower runs first. On a tie, the step listed earlier on the runner runs first.")]
        private int order;
        
        /// <summary>Lower runs first.</summary>
        public int Order => order;
        
        /// <summary>Runs once on cold start. Throwing is survivable.</summary>
        public abstract UniTask InitializeAsync(CancellationToken ct);

        /// <summary>Runs on every level load, after the previous level's token is already cancelled.</summary>
        public virtual UniTask ReinitializeAsync(CancellationToken ct) => UniTask.CompletedTask;

        /// <summary>Sync stage that runs once every step has reinitialized</summary>
        public virtual void AfterReinitialize(CancellationToken ct) {}

        /// <summary>Called on pause and resume. Pause walks the steps backwards, resume walks them forwards like init.</summary>
        /// <param name="isPaused">True on pause, false on resume.</param>
        public virtual void OnAppPause(bool isPaused) {}

        /// <summary>Called on app quit, walking the steps backwards.</summary>
        public virtual void OnAppQuit() {}
    }
}
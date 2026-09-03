using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap
{
    public abstract class BootStep : MonoBehaviour
    {
        // incremental order
        [SerializeField] private int order;
        
        public int Order => order;
        
        // cold start
        public abstract UniTask InitializeAsync(CancellationToken ct);
        // per-level load
        public virtual UniTask ReinitializeAsync(CancellationToken ct) => UniTask.CompletedTask;
        // after all steps are reinitialized
        public virtual void AfterReinitialize(CancellationToken ct) {}
        
        // pause calls steps in reverse order, resume calls steps in order
        public virtual void OnAppPause(bool isPaused) {}
        // quit calls steps in reverse order
        public virtual void OnAppQuit() {}
    }
}
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap
{
    public sealed class BoostrapRunner : MonoBehaviour, IBoostrapService
    {
        [SerializeField] private List<BoostStep> steps = new();
        
        private bool sorted;
        private readonly UniTaskCompletionSource initializedSource = new();
        private UniTask initializedTask;
        private CancellationTokenSource lifecycleCts;
        private bool isPhaseRunning;
        
        
        public bool IsInitialized { get; }
        
        public UniTask UntilInitializedAsync(CancellationToken ct = default)
        {
            if(IsInitialized) 
                return UniTask.CompletedTask;

            return ct.CanBeCanceled
                ? initializedTask.AttachExternalCancellation(ct)
                : initializedTask;
        }

        #region Unity callbacks

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            initializedTask = initializedSource.Task.Preserve();
            SortStepsStably();
        }

        private void OnDestroy()
        {
            // cancel the task to avoid consumer waiting forever
            initializedSource.TrySetCanceled();

            if (lifecycleCts == null)
                return;
            
            lifecycleCts.Cancel();
            lifecycleCts.Dispose();
            lifecycleCts = null;
        }

        #endregion

        #region API

        public async UniTask InitializeAsync()
        {
            CancellationToken token = await BeginRoundAsync("Initialize");
            
            try
            {
                
            }
        }

        #endregion

        #region Class Methods

        private void SortStepsStably()
        {
            sorted = false;
            
            List<(BoostStep step, int idx)> stepsWithIdx = new();
            int cnt = steps.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (steps[i] == null)
                {
                    Debug.LogError($"[Boostrap] Step at index {i} is null");
                    continue;
                }
                stepsWithIdx.Add((steps[i], i));
            }
            
            stepsWithIdx.Sort(static (a, b) => a.step.Order != b.step.Order ?
                a.step.Order.CompareTo(b.step.Order) : a.idx.CompareTo(b.idx));

            steps.Clear();
            for (int i = 0; i < stepsWithIdx.Count; i++)
                steps.Add(stepsWithIdx[i].step);

            sorted = true;
        }

        private async UniTask<CancellationToken> BeginRoundAsync(string roundName)
        {
            RefreshLifecycleTokenSource();
            
            // avoid overlapping 2 phases init and reinit.
            while(isPhaseRunning)
                await UniTask.Yield();

            isPhaseRunning = true;
            return lifecycleCts.Token;
        }

        private void RefreshLifecycleTokenSource()
        {
            if (lifecycleCts != null)
            {
                lifecycleCts.Cancel();
                lifecycleCts.Dispose();
            }
            
            lifecycleCts = new();
        }

        private async UniTask RunStepsAsync(Func<BoostStep, CancellationToken, UniTask> runSteps,
            string phaseName, CancellationToken ct)
        {
            int stepCount = steps.Count;
            for (int i = 0; i < stepCount; i++)
            {
                if (ct.IsCancellationRequested)
                    return;
                
                
            }
        }

        #endregion
    }
}
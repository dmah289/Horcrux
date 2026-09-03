using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap
{
    public sealed class BoostrapRunner : MonoBehaviour, IBoostrapService
    {
        private static readonly string initPhaseName = "Initialize";
        private static readonly string reinitPhaseName = "Reinitialize";
        private static readonly string afterReinitPhaseName = "AfterReinitialize";
        private static readonly string onAppPausePhaseName = "OnAppPause";
        private static readonly string onAppQuitPhaseName = "OnAppQuit";
        
        
        public event Action<BoostProgress> ProgressChanged; 
        
        [SerializeField] private List<BoostStep> steps = new();
        
        private bool sorted;
        private readonly UniTaskCompletionSource initializedSource = new();
        private UniTask initializedTask;
        private CancellationTokenSource lifecycleCts;
        private bool isPhaseRunning;

        private static readonly Func<BoostStep, CancellationToken, UniTask> InitializeStep =
            static (step, ct) => step.InitializeAsync(ct);
        private static readonly Func<BoostStep, CancellationToken, UniTask> ReinitializeStep =
            static (step, ct) => step.ReinitializeAsync(ct);
        
        public bool IsInitialized { get; private set; }

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

        private void OnApplicationPause(bool isPaused)
        {
            if (!IsInitialized)
                return;

            int stepCount = steps.Count;
            if (isPaused)
            {
                for(int i = stepCount-1 ; i >= 0; i--)
                    SafePause(steps[i], true);
            }
            else
            {
                for(int i = 0;  i < stepCount; i++)
                    SafePause(steps[i], false);
            }
        }

        private void OnApplicationQuit()
        {
            if (IsInitialized)
            {
                int stepCount = steps.Count;
                for (int i = stepCount - 1; i >= 0; i--)
                {
                    try
                    {
                        steps[i].OnAppQuit();
                    }
                    catch (Exception e)
                    {
                        LogStepFailure(steps[i], onAppQuitPhaseName, e);
                    }
                }
            }

            lifecycleCts?.Cancel();
            lifecycleCts = null;
        }

        #endregion

        #region API

        public async UniTask InitializeAsync()
        {
            CancellationToken ct = await BeginPhaseAsync(initPhaseName);

            try
            {
                await RunStepsAsync(InitializeStep, initPhaseName, ct);

                if (ct.IsCancellationRequested)
                    return;

                IsInitialized = true;
                initializedSource.TrySetResult();
            }
            finally
            {
                isPhaseRunning = false;
            }
        }

        public async UniTask ReinitializeAsync()
        {
            CancellationToken ct = await BeginPhaseAsync(reinitPhaseName);

            try
            {
                await RunStepsAsync(ReinitializeStep, reinitPhaseName, ct);

                if (ct.IsCancellationRequested)
                    return;

                int stepCount = steps.Count;
                for (int i = 0; i < stepCount; i++)
                {
                    BoostStep step = steps[i];
                    try
                    {
                        step.AfterReinitialize(ct);
                    }
                    catch (Exception e)
                    {
                        LogStepFailure(step, afterReinitPhaseName, e);
                    }
                }
            }
            finally
            {
                isPhaseRunning = false;
            }
        }
        
        public UniTask UntilInitializedAsync(CancellationToken ct = default)
        {
            if(IsInitialized) 
                return UniTask.CompletedTask;

            return ct.CanBeCanceled
                ? initializedTask.AttachExternalCancellation(ct)
                : initializedTask;
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

        private async UniTask<CancellationToken> BeginPhaseAsync(string phaseName)
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
                
                BoostStep step = steps[i];
                RaiseProgress(new BoostProgress(i, stepCount, step.name));
                try
                {
                    await runSteps(step, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception e)
                {
                    LogStepFailure(step, phaseName, e);
                }
            }
            
            RaiseProgress(new BoostProgress(stepCount, stepCount, string.Empty));
        }

        private void RaiseProgress(in BoostProgress progress)
        {
            Action<BoostProgress> handlers = ProgressChanged;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                Action<BoostProgress> action = (Action<BoostProgress>)invocationList[i];
                try
                {
                    action.Invoke(progress);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private void LogStepFailure(BoostStep step, string phaseName, Exception e)
        {
            Debug.LogError($"[BoostStrap] : {step.name} failed at {phaseName}. Skip (fail-open)", step);
            Debug.LogException(e, step);
        }

        private void SafePause(BoostStep step, bool isPaused)
        {
            try
            {
                step.OnAppPause(isPaused);
            }
            catch (Exception e)
            {
                LogStepFailure(step, onAppPausePhaseName, e);
            }
        }

        #endregion
    }
}
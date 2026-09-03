using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Sisus.Init;
using UnityEngine;

namespace Horcrux.Runtime.Implementations.Bootstrap
{
    /// <summary>The game's single init path: sorts the steps, awaits them in order, survives a failing step, owns the lifecycle token.</summary>
    [Service(typeof(IBootstrapService), FindFromScene = true)]
    public sealed class BootstrapRunner : MonoBehaviour, IBootstrapService
    {
        private const string InitPhaseName = "Initialize";
        private const string ReinitPhaseName = "Reinitialize";
        private const string AfterReinitPhaseName = "AfterReinitialize";
        private const string OnAppPausePhaseName = "OnAppPause";
        private const string OnAppQuitPhaseName = "OnAppQuit";
        
        /// <summary>Fires before each step.</summary>
        public event Action<BootProgress> ProgressChanged;
        
        [SerializeField, Tooltip("Every BootStep of the game. On an Order tie, the step listed earlier runs first.")]
        private List<BootStep> steps = new();
        
        private static readonly Func<BootStep, CancellationToken, UniTask> InitializeStep =
            static (step, ct) => step.InitializeAsync(ct);
        private static readonly Func<BootStep, CancellationToken, UniTask> ReinitializeStep =
            static (step, ct) => step.ReinitializeAsync(ct);
        
        private readonly UniTaskCompletionSource initializedSource = new();
        private UniTask initializedTask;
        private CancellationTokenSource lifecycleCts;
        private bool isPhaseRunning;
        
        /// <inheritdoc />
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
            // Release consumers waiting on UntilInitializedAsync.
            initializedSource.TrySetCanceled();

            if (lifecycleCts == null)
                return;
            
            lifecycleCts.Cancel();
            lifecycleCts.Dispose();
            lifecycleCts = null;
        }

        // Pause walks backwards, resume forwards.
        private void OnApplicationPause(bool isPaused)
        {
            // Android fires resume as the app opens; a half-initialized step must not take the hook.
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
                        LogStepFailure(steps[i], OnAppQuitPhaseName, e);
                    }
                }
            }

            // Hooks run first, the token dies after.
            lifecycleCts?.Cancel();
            lifecycleCts?.Dispose();
            lifecycleCts = null;
        }

        #endregion

        #region API

        /// <summary>Cold start. The game calls this exactly once, before the first <see cref="ReinitializeAsync"/>.</summary>
        public async UniTask InitializeAsync()
        {
            CancellationToken ct = await BeginPhaseAsync();

            try
            {
                await RunStepsAsync(InitializeStep, InitPhaseName, ct);

                if (ct.IsCancellationRequested)
                    return;

                // One-way latch: a level reinit never resets it.
                IsInitialized = true;
                initializedSource.TrySetResult();
            }
            finally
            {
                isPhaseRunning = false;
            }
        }
        
        /// <summary>One level load. The previous phase's token is cancelled before this phase starts.</summary>
        public async UniTask ReinitializeAsync()
        {
            CancellationToken ct = await BeginPhaseAsync();

            try
            {
                await RunStepsAsync(ReinitializeStep, ReinitPhaseName, ct);

                if (ct.IsCancellationRequested)
                    return;

                // Cross-step reads are safe now.
                int stepCount = steps.Count;
                for (int i = 0; i < stepCount; i++)
                {
                    BootStep step = steps[i];
                    try
                    {
                        step.AfterReinitialize(ct);
                    }
                    catch (Exception e)
                    {
                        LogStepFailure(step, AfterReinitPhaseName, e);
                    }
                }
            }
            finally
            {
                isPhaseRunning = false;
            }
        }
        
        /// <inheritdoc />
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
            List<(BootStep step, int idx)> stepsWithIdx = new();
            int cnt = steps.Count;
            for (int i = 0; i < cnt; i++)
            {
                if (steps[i] == null)
                {
                    Debug.LogError($"[Bootstrap] : Step at index {i} is null");
                    continue;
                }
                stepsWithIdx.Add((steps[i], i));
            }
            
            stepsWithIdx.Sort(static (a, b) => a.step.Order != b.step.Order ?
                a.step.Order.CompareTo(b.step.Order) : a.idx.CompareTo(b.idx));

            steps.Clear();
            for (int i = 0; i < stepsWithIdx.Count; i++)
                steps.Add(stepsWithIdx[i].step);
        }

        private async UniTask<CancellationToken> BeginPhaseAsync()
        {
            RefreshLifecycleTokenSource();
            
            // Keeps two phases from overlapping.
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

        private async UniTask RunStepsAsync(Func<BootStep, CancellationToken, UniTask> runSteps,
            string phaseName, CancellationToken ct)
        {
            int stepCount = steps.Count;
            for (int i = 0; i < stepCount; i++)
            {
                if (ct.IsCancellationRequested)
                    return;
                
                BootStep step = steps[i];
                RaiseProgress(new BootProgress(i, stepCount, step.name));
                try
                {
                    await runSteps(step, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // A new phase took over. Stop quietly; this is not a step failure.
                    return;
                }
                catch (Exception e)
                {
                    // Fail-open
                    LogStepFailure(step, phaseName, e);
                }
            }
            
            // The phase is done.
            RaiseProgress(new BootProgress(stepCount, stepCount, string.Empty));
        }

        private void RaiseProgress(in BootProgress progress)
        {
            Action<BootProgress> handlers = ProgressChanged;
            if (handlers == null)
                return;

            Delegate[] invocationList = handlers.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                Action<BootProgress> handler = (Action<BootProgress>)invocationList[i];
                try
                {
                    handler.Invoke(progress);
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private void SafePause(BootStep step, bool isPaused)
        {
            try
            {
                step.OnAppPause(isPaused);
            }
            catch (Exception e)
            {
                LogStepFailure(step, OnAppPausePhaseName, e);
            }
        }
        
        private void LogStepFailure(BootStep step, string phaseName, Exception e)
        {
            Debug.LogError($"[Bootstrap] : {step.name} failed at {phaseName}. Skip (fail-open)", step);
            Debug.LogException(e, step);
        }

        #endregion
    }
}
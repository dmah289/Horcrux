using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Composites.LiveOps;

namespace Horcrux.Runtime.Implementations.LiveOps
{
    /// <summary>
    /// Runs the Home flows of every module in four stages: progress change, tutorial, forced review, promotion.
    /// A request during a pass adds one more pass after it; leaving Home cancels the pass in flight.
    /// </summary>
    public sealed class LiveOpsHomeFlowRunner
    {
        private const int MaxPassesPerBurst = 3;
        
        private const string ProgressChangeStage = "progress_change_stage";
        private const string TutorialStage = "tutorial_stage";
        private const string ForcedReviewStage = "forced_review_stage";
        private const string PromotionStage = "promotion_stage";
        
        private static readonly Func<ALiveOpsHomeFlow, CancellationToken, UniTask> PlayProgressChange = 
            static (flow, ct) => flow.PlayProgressChangeAsync(ct);
        private static readonly Func<ALiveOpsHomeFlow, CancellationToken, UniTask> PlayTutorial = 
            static (flow, ct) => flow.PlayTutorialAsync(ct);
        private static readonly Func<ALiveOpsHomeFlow, CancellationToken, UniTask> PlayForcedReview = 
            static (flow, ct) => flow.PlayForcedReviewAsync(ct);
        private static readonly Func<ALiveOpsHomeFlow, CancellationToken, UniTask> PlayPromotion = 
            static (flow, ct) => flow.PlayPromotionAsync(ct);
        
        private readonly IReadOnlyList<ILiveOpsModule> _modules;
        private readonly Action<Exception> _onError;
        
        private readonly List<ILiveOpsModule> _orderedModules = new(5);
        private readonly List<ILiveOpsModule> _firstPhaseModules = new(5);
        private readonly List<ILiveOpsModule> _secondPhaseModules = new(5);
        private readonly List<ILiveOpsModule> _parallelPhaseModules = new(5);

        private static readonly IComparer<ILiveOpsModule> ByPriority = Comparer<ILiveOpsModule>.Create(CompareByPriority);

        private bool _isOnHome;
        private bool _hasPendingPassRequest;
        private bool _isRunningPasses;
        private CancellationTokenSource _cts;

        public LiveOpsHomeFlowRunner(IReadOnlyList<ILiveOpsModule> modules, Action<Exception> onError)
        {
            _modules = modules;
            _onError = onError;
        }

        #region API

        public void OnHomeEnter()
        {
            _isOnHome = true;
            Request();
        }

        public void OnHomeExit()
        {
            _isOnHome = false;
            _hasPendingPassRequest = false;
            _cts?.Cancel();
        }

        public void Request()
        {
            if (!_isOnHome)
                return;

            _hasPendingPassRequest = true;
            if(!_isRunningPasses)
                RunPassesAsync().Forget();
        }

        #endregion

        #region Class Methods

        private async UniTaskVoid RunPassesAsync()
        {
            _isRunningPasses = true;
            int passAmount = 0;

            try
            {
                while (_isOnHome && _hasPendingPassRequest && passAmount < MaxPassesPerBurst)
                {
                    passAmount++;
                    _hasPendingPassRequest = false;
                    _cts = new CancellationTokenSource();

                    try
                    {
                        await RunPassAsync(_cts.Token);
                    }
                    finally
                    {
                        _cts.Dispose();
                        _cts = null;
                    }
                }

                if (_isOnHome && _hasPendingPassRequest)
                {
                    _hasPendingPassRequest = false;
                    _onError(new InvalidOperationException(
                        $"[LiveOpsHomeFlowRunner]: Flows kept asking for another pass; stopped after max pass amount = {MaxPassesPerBurst}."));
                }
            }
            finally
            {
                _isRunningPasses = false;
            }
        }

        private async UniTask RunPassAsync(CancellationToken ct)
        {
            SortModulesFlow();

            await RunSimultaneousAsync(_firstPhaseModules, PlayProgressChange, ProgressChangeStage, ct);
            
            if (ct.IsCancellationRequested)
                return;

            await UniTask.WhenAll(
                RunSimultaneousAsync(_secondPhaseModules, PlayProgressChange, ProgressChangeStage, ct),
                RunSequentialAsync(_parallelPhaseModules, PlayProgressChange, ProgressChangeStage, ct));

            await RunSequentialAsync(_orderedModules, PlayTutorial, TutorialStage, ct);
            await RunSequentialAsync(_orderedModules, PlayForcedReview, ForcedReviewStage, ct);
            await RunSequentialAsync(_orderedModules, PlayPromotion, PromotionStage, ct);

        }

        private void SortModulesFlow()
        {
            _orderedModules.Clear();
            _firstPhaseModules.Clear();
            _secondPhaseModules.Clear();
            _parallelPhaseModules.Clear();

            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].HomeFlow != null) 
                    _orderedModules.Add(_modules[i]);
            }
            _orderedModules.Sort(ByPriority);

            for (int i = 0; i < _orderedModules.Count; i++)
            {
                ILiveOpsModule module = _orderedModules[i];

                switch (module.HomeFlow.ProgressChangePhase)
                {
                    case LiveOpsProgressChangePhase.First:
                        _firstPhaseModules.Add(module);
                        break;
                    case LiveOpsProgressChangePhase.Second:
                        _secondPhaseModules.Add(module);
                        break;
                    case LiveOpsProgressChangePhase.Parallel:
                        _parallelPhaseModules.Add(module);
                        break;
                }
            }
        }
        
        private static int CompareByPriority(ILiveOpsModule x, ILiveOpsModule y)
        {
            if (x.Priority.Equals(y.Priority))
                return String.CompareOrdinal(x.ModuleId, y.ModuleId);
            
            return y.Priority.CompareTo(x.Priority);
        }

        private UniTask RunSimultaneousAsync(List<ILiveOpsModule> modules, 
            Func<ALiveOpsHomeFlow, CancellationToken, UniTask> play, string stageName, CancellationToken ct)
        {
            if(modules.Count == 0)
                return UniTask.CompletedTask;
            
            UniTask[] runs = new UniTask[modules.Count];
            for(int i = 0; i < modules.Count; i++)
                runs[i] = RunIsolatedAsync(modules[i], play, stageName, ct);
            
            return UniTask.WhenAll(runs);
        }
        
        private async UniTask RunSequentialAsync(List<ILiveOpsModule> modules, 
            Func<ALiveOpsHomeFlow, CancellationToken, UniTask> play, string stageName, CancellationToken ct)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if(ct.IsCancellationRequested)
                    return;
                
                await RunIsolatedAsync(modules[i], play, stageName, ct);
            }
        }

        private async UniTask RunIsolatedAsync(ILiveOpsModule module,
            Func<ALiveOpsHomeFlow, CancellationToken, UniTask> play, string stageName, CancellationToken ct)
        {
            try
            {
                await play(module.HomeFlow, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception e)
            {
                _onError(new InvalidOperationException(
                    $"[LiveOpsHomeFlowRunner]: {module.ModuleId} threw in {stageName}; the other flows still run.", e));
            }
        }

        #endregion
    }
}
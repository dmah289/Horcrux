using System;
using System.Collections.Generic;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.LiveOps
{
    public static class LiveOpsLossHelper
    {
        private static readonly Action<ALiveOpsLoseFlow> BeginPending = static flow => flow.BeginPendingLoss();
        private static readonly Action<ALiveOpsLoseFlow> CancelPending = static flow => flow.CancelPendingLoss();
        private static readonly Action<ALiveOpsLoseFlow> Commit = static flow => flow.CommitLoss();
        private static readonly Action<ALiveOpsLoseFlow> Recover = static flow => flow.RecoverPendingLoss();
        

        #region API

        public static void BeginPendingLoss(IReadOnlyList<ILiveOpsModule> modules)
        {
            RunOnEveryModule(modules, BeginPending, nameof(BeginPendingLoss));
        }
        
        public static void CancelPendingLoss(IReadOnlyList<ILiveOpsModule> modules)
        {
            RunOnEveryModule(modules, CancelPending, nameof(CancelPendingLoss));
        }
        
        public static void CommitLoss(IReadOnlyList<ILiveOpsModule> modules)
        {
            RunOnEveryModule(modules, Commit, nameof(CommitLoss));
        }
        
        public static void RecoverPendingLoss(ILiveOpsModule module)
        {
            RunStep(module, Recover, nameof(RecoverPendingLoss));
        }

        public static bool IsAnyProgressAtStake(IReadOnlyList<ILiveOpsModule> modules)
        {
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].LoseFlow is { IsProgressAtStake: true })
                    return true;
            }

            return false;
        }

        public static void CollectProgressAtStake(IReadOnlyList<ILiveOpsModule> modules, List<ILiveOpsModule> atStake)
        {
            atStake.Clear();
            
            for(int i = 0; i < modules.Count; i++)
            {
                if (modules[i].LoseFlow is { IsProgressAtStake: true })
                    atStake.Add(modules[i]);
            }
            
            atStake.Sort(LiveOpsModulePriority.ByPriority);
        }

        #endregion

        #region Class Methods

        private static void RunOnEveryModule(IReadOnlyList<ILiveOpsModule> modules, Action<ALiveOpsLoseFlow> step,
            string stepName)
        {
            for(int i = 0; i < modules.Count; i++)
                RunStep(modules[i], step, stepName);
        }

        private static void RunStep(ILiveOpsModule module, Action<ALiveOpsLoseFlow> step, string stepName)
        {
            ALiveOpsLoseFlow flow = module.LoseFlow;

            if (flow == null)
                return;

            try
            {
                step(flow);
            }
            catch (Exception e)
            {
                Debug.LogException(new InvalidOperationException(
                    $"[LiveOpsLossHelper]: {module.ModuleId} threw in {stepName}; the other modules still run.", e));
            }
        }

        #endregion
    }
}
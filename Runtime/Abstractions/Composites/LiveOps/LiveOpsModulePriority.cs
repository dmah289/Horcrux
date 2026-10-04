using System;
using System.Collections.Generic;

namespace Horcrux.Runtime.Abstractions.LiveOps
{
    public static class LiveOpsModulePriority
    {
        /// <summary>
        /// Higher priority first, ties by ModuleId.
        /// </summary>
        public static readonly IComparer<ILiveOpsModule> ByPriority = Comparer<ILiveOpsModule>.Create(CompareByPriority);
        
        private static int CompareByPriority(ILiveOpsModule x, ILiveOpsModule y)
        {
            if (x.Priority.Equals(y.Priority))
                return String.CompareOrdinal(x.ModuleId, y.ModuleId);
            
            return y.Priority.CompareTo(x.Priority);
        }
    }
}
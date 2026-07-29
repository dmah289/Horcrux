using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class ParticleSystemExtensions
    {
        /// <summary>
        /// Stop emitting particles and wait until all remaining particles are dissolved.
        /// </summary>
        /// <param name="onComplete">Called when remaining particles are dissolved</param>
        public static async UniTask StopAndAwaitCompletion(this ParticleSystem self, 
            Action onComplete, CancellationToken ct = default)
        {
            if (!self) return;
            
            self.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            
            while (self && self.IsAlive(true))
                await UniTask.Yield(ct);

            onComplete?.Invoke();
        }
    }
}
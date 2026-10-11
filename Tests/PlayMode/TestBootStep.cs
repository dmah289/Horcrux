using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;

namespace Horcrux.Tests
{
    /// <summary>A step that appends its GameObject name to a shared log, optionally after waiting on a gate the test releases.</summary>
    public sealed class TestBootStep : ABootStep
    {
        private List<string> _log;
        private UniTaskCompletionSource _gate;

        public void Setup(List<string> log) => _log = log;

        /// <summary>The next <see cref="InitializeAsync"/> waits until the returned source completes.</summary>
        public UniTaskCompletionSource Block() => _gate = new UniTaskCompletionSource();

        public override async UniTask InitializeAsync(CancellationToken ct)
        {
            if (_gate != null)
                await _gate.Task;

            _log.Add(name);
        }
    }
}

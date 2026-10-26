using System;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.FeelTrace
{
    /// <summary>
    /// Số đo mỗi frame, ghi thành entity "M0": byte GC cấp phát, batches, set pass, số SpriteRenderer bật mà không vẽ gì.
    /// Số là của frame trước vì profiler chốt khi frame xong. Không bật cho lần quay để diff: GC đổi giữa các lần chạy.
    /// </summary>
    public sealed class FeelTraceMetrics : IDisposable
    {
        private ProfilerRecorder _gcAllocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
        private SpriteRenderer[] _renderers = Array.Empty<SpriteRenderer>();

        /// <summary>Chụp danh sách SpriteRenderer để đếm renderer vẽ suông. Cấp phát nên gọi lúc boot và sau khi object mới sinh, không mỗi frame.</summary>
        public void CollectRenderers() =>
            _renderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);

        public void Dispose() => _gcAllocated.Dispose();

        internal void Write(FeelTraceWriter writer) =>
            writer.Entity("M", 0).Value((int)Math.Min(_gcAllocated.LastValue, int.MaxValue))
                .Value(UnityStats.batches).Value(UnityStats.setPassCalls).Value(CountIdleRenderers());

        private int CountIdleRenderers()
        {
            var count = 0;
            foreach (var r in _renderers)
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && (r.sprite == null || r.color.a <= 0f))
                    count++;
            return count;
        }
    }
}

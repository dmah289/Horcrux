using System.Collections.Generic;
using Horcrux.Editor.Common;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Tab "Asset Usages" — trả lời "asset nào tham chiếu target?" qua <see cref="AssetReferenceIndex"/>.
    /// Chỉ ở mức asset (hard dependency). Không bắt AssetReference (Addressables) — đó là Tab riêng.
    /// Static, không phụ thuộc UI.
    /// </summary>
    public static class AssetUsageScanner
    {
        /// <summary>
        /// Tìm mọi asset đang tham chiếu <paramref name="target"/>.
        /// Trả list rỗng nếu không ai dùng (an toàn để xóa/sửa mà không ảnh hưởng asset khác).
        /// </summary>
        public static List<UsageEntry> Scan(Object target)
        {
            var results = new List<UsageEntry>();
            if (target == null)
                return results;

            string path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path))
                return results; // không phải asset trong project (vd scene instance)

            string guid = AssetDatabase.AssetPathToGUID(path);
            if (string.IsNullOrEmpty(guid))
                return results;

            IReadOnlyList<string> referencers = AssetReferenceIndex.GetReferencers(guid);
            for (int i = 0; i < referencers.Count; i++)
            {
                string refPath = referencers[i];
                Object refAsset = AssetDatabase.LoadAssetAtPath<Object>(refPath);
                results.Add(new UsageEntry(refPath, refAsset, null));
            }

            return results;
        }
    }
}

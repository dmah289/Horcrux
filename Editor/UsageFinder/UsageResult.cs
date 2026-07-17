using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>Shared StringBuilder — dùng chung khi build cached display strings tại scan time.</summary>
    internal static class UsageStringHelper
    {
        internal static readonly StringBuilder SB = new(96);
    }

    /// <summary>
    /// Một referencer — asset đang tham chiếu target. Dùng chung cho cả Tab "Asset Usages"
    /// (referencer từ dependency index) và Tab "Addressable Usages" (referencer chứa AssetReference).
    ///
    /// Immutable + cache mọi display string tại scan time → zero allocation trong OnGUI.
    /// </summary>
    public sealed class UsageEntry
    {
        public readonly string       assetPath;    // "Assets/.../Foo.prefab"
        public readonly Object       asset;         // loaded lazily tại scan time (để ping)
        public readonly List<string> detailLabels;  // field path chi tiết (Addressable tab); null nếu không có

        // ── Cached display data ──
        public readonly string    displayLabel;   // "Foo.prefab" (icon + tên file)
        public readonly string    folderLabel;    // thư mục chứa (mờ bên phải) — ngắn gọn, không lặp tên file
        public readonly string    pathLabel;      // full path (dùng cho tooltip + filter)
        public readonly string    typeName;       // "GameObject", "Material", "SceneAsset",...
        public readonly string    foldoutKey;     // SessionState key ổn định theo path
        public readonly Texture   icon;           // asset preview icon (mini thumbnail)

        public UsageEntry(string assetPath, Object asset, List<string> detailLabels)
        {
            this.assetPath   = assetPath;
            this.asset       = asset;
            this.detailLabels = detailLabels;

            typeName = asset != null ? asset.GetType().Name : "Object";
            icon     = AssetDatabase.GetCachedIcon(assetPath);

            displayLabel = System.IO.Path.GetFileName(assetPath);

            // Thư mục chứa, bỏ tiền tố "Assets/" cho gọn (dòng nào cũng có nên vô nghĩa khi lặp).
            const string assetsPrefix = "Assets/";
            string dir = (System.IO.Path.GetDirectoryName(assetPath) ?? "").Replace('\\', '/');
            folderLabel = dir.StartsWith(assetsPrefix, System.StringComparison.Ordinal)
                ? dir.Substring(assetsPrefix.Length)
                : dir;

            pathLabel = assetPath;

            var sb = UsageStringHelper.SB;
            sb.Clear();
            sb.Append("HorcruxUsageFinder_").Append(assetPath);
            foldoutKey = sb.ToString();
        }

        /// <summary>Số dòng chi tiết (field usage) — 0 nếu chỉ ở mức asset.</summary>
        public int DetailCount => detailLabels?.Count ?? 0;
    }
}

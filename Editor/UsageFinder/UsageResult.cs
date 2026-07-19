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
    /// Cách điều hướng khi click 1 field hit — quyết định NavigationHelper làm gì.
    /// </summary>
    public enum UsageNavKind
    {
        /// <summary>Field nằm trong asset đã load được (prefab/.asset/.mat/SO). Select owner + expand.</summary>
        AssetField,

        /// <summary>Field nằm trong GameObject của scene đang mở. Select GO + expand component.</summary>
        OpenSceneField,

        /// <summary>Reference nằm trong scene file trên disk (chưa mở). Click → mở scene rồi định vị.</summary>
        DiskScene,
    }

    /// <summary>
    /// Một field cụ thể đang trỏ tới target — đơn vị "click để điều hướng".
    /// Immutable, cache display string tại scan time → zero alloc trong OnGUI.
    ///
    /// Navigation context lưu đủ để <see cref="Common.NavigationHelper"/> nhảy tới nơi chứa reference:
    ///  • AssetField / OpenSceneField: <see cref="navObject"/> (+ <see cref="navComponent"/> nếu là component).
    ///  • DiskScene: <see cref="scenePath"/> + <see cref="targetGuid"/> — mở scene rồi re-walk định vị.
    /// </summary>
    public sealed class UsageFieldHit
    {
        public readonly UsageNavKind navKind;
        public readonly string       propertyPath;   // raw — để expand đúng property
        public readonly string       displayLabel;   // "Owner > Field > Path" (cached)

        // ── Navigation context ──
        public readonly Object    navObject;     // GameObject (scene/prefab) hoặc asset object (SO/material). Null cho DiskScene.
        public readonly Component navComponent;  // component chứa field (null nếu owner không phải component)
        public readonly string    scenePath;     // chỉ DiskScene
        public readonly string    targetGuid;    // chỉ DiskScene — để re-walk sau khi mở

        /// <summary>Field hit trong asset/scene đang mở (có object để select ngay).</summary>
        public UsageFieldHit(UsageNavKind navKind, string ownerLabel, string fieldDisplayPath,
                             string propertyPath, Object navObject, Component navComponent)
        {
            this.navKind      = navKind;
            this.propertyPath = propertyPath;
            this.navObject    = navObject;
            this.navComponent = navComponent;
            scenePath  = null;
            targetGuid = null;

            displayLabel = BuildLabel(ownerLabel, fieldDisplayPath);
        }

        /// <summary>Hit mức scene-file trên disk — chưa có field detail, mở scene khi click.</summary>
        public UsageFieldHit(string scenePath, string targetGuid, string label)
        {
            navKind          = UsageNavKind.DiskScene;
            this.scenePath   = scenePath;
            this.targetGuid  = targetGuid;
            propertyPath     = null;
            navObject        = null;
            navComponent     = null;
            displayLabel     = label;
        }

        private static string BuildLabel(string ownerLabel, string fieldDisplayPath)
        {
            var sb = UsageStringHelper.SB;
            sb.Clear();
            if (!string.IsNullOrEmpty(ownerLabel))
                sb.Append(ownerLabel).Append(" > ");
            sb.Append(fieldDisplayPath);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Một referencer — asset/scene đang tham chiếu target, kèm danh sách field cụ thể trỏ tới nó.
    /// Immutable + cache mọi display string tại scan time → zero allocation trong OnGUI.
    /// </summary>
    public sealed class UsageEntry
    {
        public readonly string             assetPath;    // "Assets/.../Foo.prefab"
        public readonly Object             asset;         // main asset (để ping fallback)
        public readonly List<UsageFieldHit> hits;         // field cụ thể; luôn ≥1 (fallback file-level nếu không map được)

        // ── Cached display data ──
        public readonly string  displayLabel;   // "Foo.prefab"
        public readonly string  folderLabel;    // thư mục chứa (bỏ tiền tố Assets/)
        public readonly string  pathLabel;      // full path (tooltip + filter)
        public readonly string  typeName;       // "GameObject", "Material", "SceneAsset",...
        public readonly string  foldoutKey;     // SessionState key ổn định theo path
        public readonly Texture icon;           // asset preview icon

        public UsageEntry(string assetPath, Object asset, List<UsageFieldHit> hits)
        {
            this.assetPath = assetPath;
            this.asset     = asset;
            this.hits      = hits;

            typeName = asset != null ? asset.GetType().Name : "Object";
            icon     = AssetDatabase.GetCachedIcon(assetPath);

            displayLabel = System.IO.Path.GetFileName(assetPath);

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

        /// <summary>
        /// Entry cho referencer là GameObject trong scene đang mở (không có asset path).
        /// Dùng khi target là scene object — kết quả từ SceneReferenceScanner được gộp về cùng model.
        /// </summary>
        private UsageEntry(GameObject sceneGo, string sceneName, List<UsageFieldHit> hits)
        {
            assetPath = sceneName;
            asset     = sceneGo;
            this.hits = hits;

            typeName     = "GameObject";
            icon         = EditorGUIUtility.IconContent("GameObject Icon").image;
            displayLabel = sceneGo != null ? sceneGo.name : "<null>";
            folderLabel  = sceneName;                 // hiện tên scene bên phải cho biết GO ở scene nào
            pathLabel    = sceneName;

            var sb = UsageStringHelper.SB;
            sb.Clear();
            sb.Append("HorcruxUsageFinder_SceneGO_")
              .Append(sceneGo != null ? sceneGo.GetInstanceID() : 0);
            foldoutKey = sb.ToString();
        }

        /// <summary>Factory cho scene-object referencer.</summary>
        public static UsageEntry ForSceneObject(GameObject sceneGo, string sceneName, List<UsageFieldHit> hits)
            => new(sceneGo, sceneName, hits);

        /// <summary>Số field trỏ tới target trong referencer này.</summary>
        public int HitCount => hits?.Count ?? 0;
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.AddressablesLayout
{
    /// <summary>
    /// Soát mọi <c>AssetReference</c> đã gán trong prefab, file <c>.asset</c> và scene: GUID còn asset không, asset đó có
    /// là entry Addressables (kể cả nằm trong thư mục đã đăng ký) không. Hai lỗi này chỉ lộ lúc chạy, thành
    /// <c>InvalidKeyException</c> ở lần nạp đầu. Chỉ báo, không sửa.
    /// <para>Asset lưu dạng text: mọi <c>AssetReference</c> đã serialize đều để lại chuỗi <c>m_AssetGUID</c> trong file (cả
    /// override trên prefab lồng), nên file không có chuỗi đó thì không load. Scene đang đóng đọc GUID thẳng từ YAML, không mở.
    /// Asset lưu binary: load mọi file như cũ, scene đang đóng không quét được (cảnh báo).</para>
    /// </summary>
    public static class AddressableReferenceAudit
    {
        public enum IssueKind
        {
            /// <summary>GUID không còn file nào trong project.</summary>
            MissingAsset,

            /// <summary>Asset còn nhưng không là entry Addressables, cả trực tiếp lẫn qua thư mục cha.</summary>
            NotAddressable,
        }

        public readonly struct Issue
        {
            public readonly IssueKind Kind;
            public readonly string OwnerPath;
            public readonly string Location;
            public readonly string Guid;
            public readonly string TargetPath;

            public Issue(IssueKind kind, string ownerPath, string location, string guid, string targetPath)
            {
                Kind = kind;
                OwnerPath = ownerPath;
                Location = location;
                Guid = guid;
                TargetPath = targetPath;
            }

            public override string ToString() => Kind == IssueKind.MissingAsset
                ? $"{OwnerPath} › {Location}: GUID {Guid} không còn asset"
                : $"{OwnerPath} › {Location}: '{TargetPath}' chưa là Addressable";
        }

        [MenuItem("Horcrux/Addressables/Reference Audit")]
        private static void RunFromMenu()
        {
            var issues = Run(out bool cancelled);
            foreach (var issue in issues)
                Debug.LogError($"[AddressableReferenceAudit]: {issue}", AssetDatabase.LoadMainAssetAtPath(issue.OwnerPath));

            string tail = cancelled ? " (đã huỷ giữa chừng, kết quả chưa đủ)" : string.Empty;
            if (issues.Count == 0)
                Debug.Log($"[AddressableReferenceAudit]: không có AssetReference gãy{tail}.");
            else
                Debug.LogError($"[AddressableReferenceAudit]: {issues.Count} AssetReference gãy{tail}. Bấm từng dòng để chọn asset chứa nó.");
        }

        /// <summary>
        /// Quét và trả mọi reference gãy. <paramref name="cancelled"/> = true khi người dùng huỷ ở thanh tiến độ: danh sách
        /// chỉ là một phần, không được kết luận "sạch". Gọi được từ eval hay <c>-executeMethod</c> qua một hàm bọc của dự án.
        /// </summary>
        public static List<Issue> Run(out bool cancelled)
        {
            cancelled = false;
            var issues = new List<Issue>();
            var settings = AddressableAssetSettingsDefaultObject.GetSettings(false);
            if (settings == null)
            {
                Debug.LogWarning("[AddressableReferenceAudit]: project chưa có Addressables settings.");
                return issues;
            }

            var visitor = new Visitor { Settings = settings, Issues = issues };
            bool textMode = EditorSettings.serializationMode == SerializationMode.ForceText;
            try
            {
                cancelled = ScanPrefabs(ref visitor, textMode) || ScanAssetFiles(ref visitor, textMode);
                if (!cancelled) ScanScenes(ref visitor, textMode);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
            return issues;
        }

        private struct Visitor : IReferencePropertyVisitor
        {
            public AddressableAssetSettings Settings;
            public List<Issue> Issues;
            public string OwnerPath;
            public string OwnerLabel;

            public void Visit(SerializedObject so, SerializedProperty property, RefPropertyKind kind)
            {
                if (kind != RefPropertyKind.AssetReference) return;

                string guid = property.FindPropertyRelative(GuidKey)?.stringValue;
                if (!TryClassify(Settings, guid, out var issueKind, out string targetPath)) return;

                string location = $"{OwnerLabel} > {SerializedPropertyWalker.BuildDisplayPath(so, property.propertyPath)}";
                Issues.Add(new Issue(issueKind, OwnerPath, location, guid, targetPath));
            }
        }

        /// <summary>true khi GUID là reference gãy. Ô chưa gán là lựa chọn của người author, không phải reference gãy.</summary>
        private static bool TryClassify(AddressableAssetSettings settings, string guid, out IssueKind kind, out string targetPath)
        {
            kind = default;
            targetPath = null;
            if (string.IsNullOrEmpty(guid)) return false;

            targetPath = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(targetPath) || AssetDatabase.LoadMainAssetAtPath(targetPath) == null)
                kind = IssueKind.MissingAsset;
            else if (settings.FindAssetEntry(guid, true) == null)
                kind = IssueKind.NotAddressable;
            else
                return false;
            return true;
        }

        private const string GuidKey = "m_AssetGUID";

        /// <summary>Không có chuỗi khoá thì file không chứa AssetReference nào đã gán; binary thì không biết, phải load.</summary>
        private static bool MayHoldReference(string path, bool textMode)
            => !textMode || File.ReadAllText(path).Contains(GuidKey, StringComparison.Ordinal);

        private static readonly List<MonoBehaviour> BehaviourBuffer = new(64);

        private static bool ScanPrefabs(ref Visitor visitor, bool textMode)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ReportProgress("Prefabs", path, i, guids.Length)) return true;
                if (!MayHoldReference(path, textMode)) continue;

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) ScanHierarchy(root, path, ref visitor);
            }
            return false;
        }

        /// <summary>
        /// Mọi file <c>.asset</c> trong <c>Assets/</c>, cả sub-asset: collection ScriptableObject hay gom AssetReference vào SO
        /// con. <c>AssetReference</c> chỉ khai được trong class C#, nên chỉ walk ScriptableObject và MonoBehaviour.
        /// </summary>
        private static bool ScanAssetFiles(ref Visitor visitor, bool textMode)
        {
            var paths = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (ReportProgress("Assets", path, i, paths.Count)) return true;
                if (!MayHoldReference(path, textMode)) continue;

                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
                {
                    if (obj is not (ScriptableObject or MonoBehaviour)) continue;
                    visitor.OwnerPath = path;
                    visitor.OwnerLabel = $"{obj.name} ({obj.GetType().Name})";
                    Walk(obj, ref visitor);
                }
            }
            return false;
        }

        /// <summary>
        /// Scene đang mở: walk object (thấy cả sửa chưa lưu, vị trí là đường dẫn hierarchy). Scene đang đóng: mở là đổi trạng
        /// thái Editor, nên đọc GUID từ YAML, vị trí là số dòng.
        /// </summary>
        private static void ScanScenes(ref Visitor visitor, bool textMode)
        {
            var open = new HashSet<string>(StringComparer.Ordinal);
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                string path = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
                open.Add(path);
                foreach (var root in scene.GetRootGameObjects())
                    ScanHierarchy(root, path, ref visitor);
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (open.Contains(path)) continue;
                if (!textMode)
                {
                    Debug.LogWarning($"[AddressableReferenceAudit]: asset lưu binary, không quét được scene đang đóng '{path}'.");
                    continue;
                }
                ScanSceneText(path, ref visitor);
            }
        }

        // Field gán trực tiếp: "m_AssetGUID: <guid>". Override trên prefab lồng: "propertyPath: a.b.m_AssetGUID" rồi "value: <guid>".
        private static readonly Regex SceneGuidPattern = new(
            @"m_AssetGUID:[ \t]*(?<g>[0-9a-fA-F]{32})|propertyPath:[ \t]*(?<p>\S*m_AssetGUID)[ \t]*\r?\n[ \t]*value:[ \t]*(?<g>[0-9a-fA-F]{32})",
            RegexOptions.Compiled);

        private static void ScanSceneText(string path, ref Visitor visitor)
        {
            string text = File.ReadAllText(path);
            if (!text.Contains(GuidKey, StringComparison.Ordinal)) return;

            foreach (Match match in SceneGuidPattern.Matches(text))
            {
                string guid = match.Groups["g"].Value;
                if (!TryClassify(visitor.Settings, guid, out var kind, out string targetPath)) continue;

                int line = 1;
                for (int c = 0; c < match.Index; c++)
                    if (text[c] == '\n') line++;
                string field = match.Groups["p"].Success ? match.Groups["p"].Value : GuidKey;
                visitor.Issues.Add(new Issue(kind, path, $"dòng {line} ({field}, scene đang đóng)", guid, targetPath));
            }
        }

        private static void ScanHierarchy(GameObject root, string ownerPath, ref Visitor visitor)
        {
            BehaviourBuffer.Clear();
            root.GetComponentsInChildren(true, BehaviourBuffer);
            foreach (var behaviour in BehaviourBuffer)
            {
                if (behaviour == null) continue; // missing script
                visitor.OwnerPath = ownerPath;
                visitor.OwnerLabel = $"{HierarchyPath(behaviour.transform, root.transform)} ({behaviour.GetType().Name})";
                Walk(behaviour, ref visitor);
            }
        }

        private static void Walk(Object obj, ref Visitor visitor)
        {
            using var so = new SerializedObject(obj);
            SerializedPropertyWalker.Walk(so, ref visitor);
        }

        private static readonly StringBuilder PathBuilder = new(128);

        private static string HierarchyPath(Transform node, Transform root)
        {
            PathBuilder.Clear();
            for (var t = node; t != null; t = t == root ? null : t.parent)
                PathBuilder.Insert(0, PathBuilder.Length == 0 ? t.name : t.name + "/");
            return PathBuilder.ToString();
        }

        private static bool ReportProgress(string phase, string info, int i, int total)
        {
            if ((i & 0x1F) != 0) return false;
            return EditorUtility.DisplayCancelableProgressBar($"Addressable Reference Audit · {phase}", info,
                total == 0 ? 1f : (float)i / total);
        }
    }
}

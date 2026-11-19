using System;
using System.Collections.Generic;
using System.Text;
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
    /// Soát mọi <c>AssetReference</c> đã gán trong prefab, file <c>.asset</c> và scene đang mở: GUID còn asset không, asset đó có
    /// là entry Addressables (kể cả nằm trong thư mục đã đăng ký) không. Hai lỗi này chỉ lộ lúc chạy, thành
    /// <c>InvalidKeyException</c> ở lần nạp đầu. Chỉ báo, không sửa.
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
            try
            {
                cancelled = ScanPrefabs(ref visitor) || ScanAssetFiles(ref visitor);
                if (!cancelled) ScanOpenScenes(ref visitor);
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

                string guid = property.FindPropertyRelative("m_AssetGUID")?.stringValue;
                // Ô chưa gán là lựa chọn của người author, không phải reference gãy.
                if (string.IsNullOrEmpty(guid)) return;

                string targetPath = AssetDatabase.GUIDToAssetPath(guid);
                IssueKind issueKind;
                if (string.IsNullOrEmpty(targetPath) || AssetDatabase.LoadMainAssetAtPath(targetPath) == null)
                    issueKind = IssueKind.MissingAsset;
                else if (Settings.FindAssetEntry(guid, true) == null)
                    issueKind = IssueKind.NotAddressable;
                else
                    return;

                string location = $"{OwnerLabel} > {SerializedPropertyWalker.BuildDisplayPath(so, property.propertyPath)}";
                Issues.Add(new Issue(issueKind, OwnerPath, location, guid, targetPath));
            }
        }

        private static readonly List<MonoBehaviour> BehaviourBuffer = new(64);

        private static bool ScanPrefabs(ref Visitor visitor)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (ReportProgress("Prefabs", path, i, guids.Length)) return true;

                var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root != null) ScanHierarchy(root, path, ref visitor);
            }
            return false;
        }

        /// <summary>
        /// Mọi file <c>.asset</c> trong <c>Assets/</c>, cả sub-asset: collection ScriptableObject hay gom AssetReference vào SO
        /// con. <c>AssetReference</c> chỉ khai được trong class C#, nên chỉ walk ScriptableObject và MonoBehaviour.
        /// </summary>
        private static bool ScanAssetFiles(ref Visitor visitor)
        {
            var paths = new List<string>();
            foreach (string path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith("Assets/", StringComparison.Ordinal) && path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
                    paths.Add(path);

            for (int i = 0; i < paths.Count; i++)
            {
                string path = paths[i];
                if (ReportProgress("Assets", path, i, paths.Count)) return true;

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

        /// <summary>Scene trên đĩa phải mở mới quét được, việc đó đổi trạng thái Editor nên để ngoài; chỉ quét scene đang mở.</summary>
        private static void ScanOpenScenes(ref Visitor visitor)
        {
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.isLoaded) continue;
                string path = string.IsNullOrEmpty(scene.path) ? scene.name : scene.path;
                foreach (var root in scene.GetRootGameObjects())
                    ScanHierarchy(root, path, ref visitor);
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

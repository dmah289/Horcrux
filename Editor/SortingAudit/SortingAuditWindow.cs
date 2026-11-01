using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Horcrux.Editor.SortingAudit
{
    /// <summary>Một nút Scan: mỗi dòng là một object vi phạm một luật, bấm là chọn object. Chạy khi được hỏi, không chạy nền.</summary>
    public sealed class SortingAuditWindow : EditorWindow
    {
        private static readonly GUIContent ScanLabel = new("Scan", "Quét mọi prefab dưới thư mục và scene đang mở");
        private static readonly GUIContent FolderLabel = new("Folder", "Thư mục gốc chứa prefab, ví dụ Assets/_MyGame");
        private static readonly GUIContent ShowRootsLabel = new("Show roots (R4)", "Liệt kê gốc entity: layer và order thật của dự án");

        private string _folder = "Assets";
        private bool _showRoots;
        private Vector2 _scroll;
        private readonly List<SortingFinding> _findings = new();
        private int _violations;

        [MenuItem("Horcrux/Sorting Audit")]
        private static void ShowWindow() => GetWindow<SortingAuditWindow>("Sorting Audit").Show();

        private void OnGUI()
        {
            _folder = EditorGUILayout.TextField(FolderLabel, _folder);
            _showRoots = EditorGUILayout.Toggle(ShowRootsLabel, _showRoots);

            if (GUILayout.Button(ScanLabel))
                Scan();

            EditorGUILayout.LabelField($"{_violations} vi phạm, {_findings.Count - _violations} gốc");

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var finding in _findings)
            {
                if (finding.Rule == SortingRule.Root && !_showRoots) continue;
                if (GUILayout.Button($"[{finding.Rule}] {finding.Asset.name} › {finding.Target.name}: {finding.Message}", EditorStyles.linkLabel))
                {
                    Selection.activeObject = finding.Target;
                    EditorGUIUtility.PingObject(finding.Asset);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Scan()
        {
            _findings.Clear();

            // Prefab asset đã nạp có nested instance dựng đầy đủ, nên R2 thấy cả override nằm trong prefab cha.
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { _folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                    SortingAuditCore.Scan(prefab, prefab, _findings);
            }

            var scene = SceneManager.GetActiveScene();
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path);
            foreach (var root in scene.GetRootGameObjects())
                SortingAuditCore.Scan(root, sceneAsset != null ? sceneAsset : root, _findings);

            _violations = 0;
            foreach (var finding in _findings)
                if (finding.Rule != SortingRule.Root) _violations++;
        }
    }
}

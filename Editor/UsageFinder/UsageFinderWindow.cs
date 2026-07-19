using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using Horcrux.Editor.SceneReferenceFinder;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.UsageFinder
{
    /// <summary>
    /// Usage Finder — 1 lệnh "Find All Usages": chọn 1 GameObject/asset → tìm MỌI field trỏ tới nó,
    /// không sót case nào (list, nested, AssetReference, ObjectReference,...). Click field → điều hướng
    /// tới nơi chứa reference + highlight.
    ///
    /// Tự nhận diện loại target:
    ///  • Asset (có GUID)     → <see cref="AssetReferenceScanner"/> (grep-GUID 2 pha, toàn project + scene mở).
    ///  • Scene object (no GUID) → <see cref="SceneReferenceScanner"/> (match instanceID trong scene mở).
    /// </summary>
    public sealed class UsageFinderWindow : EditorWindow
    {
        // ──────────────── Static eager cache ────────────────

        private static readonly GUIContent ClearBtnLabel = new("✕");
        private static readonly GUIContent TargetLabel   = new("Target", "GameObject hoặc asset cần tìm usage — kéo vào đây, hoặc dùng menu chuột phải \"Find Usages (Horcrux)\"");
        private static readonly GUIContent ScanBtnLabel  = new("🔍 Find All Usages");

        private static readonly StringBuilder SharedSB = new(64);

        // ──────────────── Per-instance state ────────────────

        private Object   _target;
        private string   _filterText = "";
        private Vector2  _scroll;

        private List<UsageEntry> _results;         // null cho tới scan đầu tiên
        private List<UsageEntry> _filteredResults;
        private bool _filterDirty = true;
        private bool _resultsComplete = true;      // false nếu scan bị hủy → KHÔNG khẳng định "không ai dùng"

        private UsageResultDrawer _drawer;

        private readonly GUIContent _statusContent = new();
        private int  _lastStatusCount = -1;
        private bool _lastStatusHasFilter;

        // ──────────────── Menu ────────────────

        [MenuItem("Horcrux/Usage Finder")]
        private static void ShowWindow()
        {
            var window = GetWindow<UsageFinderWindow>();
            window.titleContent = new GUIContent("Usage Finder");
            window.Show();
        }

        /// <summary>Context menu Project window: right-click asset → Find Usages.</summary>
        [MenuItem("Assets/Find Usages (Horcrux)", false, 30)]
        private static void FindUsagesFromContext()
        {
            var window = GetWindow<UsageFinderWindow>();
            window.titleContent = new GUIContent("Usage Finder");
            window._target = Selection.activeObject;
            window.ExecuteScan();
            window.Show();
        }

        [MenuItem("Assets/Find Usages (Horcrux)", true)]
        private static bool FindUsagesFromContextValidate()
        {
            return Selection.activeObject != null
                   && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(Selection.activeObject));
        }

        // ──────────────── OnGUI ────────────────

        private void OnGUI()
        {
            StaticStyles.Ensure();
            _drawer ??= new UsageResultDrawer();

            HandleScrollWheelBoost();

            if (Event.current.type == EventType.Layout && _filterDirty && _results != null)
            {
                RebuildFilteredResults();
                _filterDirty = false;
            }

            DrawTargetRow();
            DrawFilterBar();
            DrawSeparator();

            _scroll = GUILayout.BeginScrollView(_scroll);
            _drawer.Draw(_results, _filteredResults ?? _results, _resultsComplete);
            GUILayout.EndScrollView();

            DrawStatusBar();
        }

        // ──────────────── Target row ────────────────

        private void DrawTargetRow()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _target = EditorGUILayout.ObjectField(TargetLabel, _target, typeof(Object), true);
            EditorGUILayout.EndHorizontal();

            Color saved = GUI.backgroundColor;
            GUI.backgroundColor = StaticColor.ScanBtnColor;
            if (GUILayout.Button(ScanBtnLabel, StaticGUILayout.ScanFullWidth))
                ExecuteScan();
            GUI.backgroundColor = saved;
        }

        // ──────────────── Filter bar ────────────────

        private void DrawFilterBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            EditorGUI.BeginChangeCheck();
            _filterText = GUILayout.TextField(_filterText, EditorStyles.toolbarSearchField, GUILayout.ExpandWidth(true));
            if (EditorGUI.EndChangeCheck())
                _filterDirty = true;

            if (!string.IsNullOrEmpty(_filterText))
            {
                if (GUILayout.Button(ClearBtnLabel, EditorStyles.toolbarButton, StaticGUILayout.Mini24))
                {
                    _filterText = "";
                    _filterDirty = true;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // ──────────────── Status bar ────────────────

        private void DrawStatusBar()
        {
            if (_results == null) return;

            List<UsageEntry> list = _filteredResults ?? _results;
            int refCount = CountHits(list);
            bool hasFilter = !string.IsNullOrEmpty(_filterText);

            if (_lastStatusCount != refCount || _lastStatusHasFilter != hasFilter)
            {
                _lastStatusCount = refCount;
                _lastStatusHasFilter = hasFilter;

                SharedSB.Clear();
                SharedSB.Append(refCount).Append(refCount == 1 ? " field" : " fields")
                        .Append(" in ").Append(list.Count).Append(list.Count == 1 ? " referencer" : " referencers");
                if (hasFilter)
                    SharedSB.Append(" (filtered)");
                _statusContent.text = SharedSB.ToString();
            }

            DrawSeparator();
            GUILayout.Label(_statusContent, EditorStyles.centeredGreyMiniLabel);
        }

        // ──────────────── Scan dispatch ────────────────

        private void ExecuteScan()
        {
            if (_target == null)
            {
                _results = null;
                _filteredResults = null;
                _resultsComplete = true;
                return;
            }

            bool isAsset = !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(_target))
                           && !IsSceneInstance(_target);

            if (isAsset)
            {
                _results = AssetReferenceScanner.Scan(_target, out bool cancelled);
                _resultsComplete = !cancelled;
            }
            else
            {
                _results = ScanSceneObject(_target);
                _resultsComplete = true; // scene scan không có cancel (nhanh)
            }

            _scroll = Vector2.zero;
            _filterDirty = true;
            Repaint();
        }

        /// <summary>Target là instance trong scene (GameObject/Component đang sống trong scene mở)?</summary>
        private static bool IsSceneInstance(Object target)
        {
            GameObject go = target as GameObject;
            if (go == null && target is Component c) go = c.gameObject;
            return go != null && go.scene.IsValid();
        }

        /// <summary>
        /// Quét scene object qua SceneReferenceScanner rồi map về List&lt;UsageEntry&gt; để dùng chung drawer.
        /// Quét mọi scene đang loaded (không chỉ scene của target).
        /// </summary>
        private static List<UsageEntry> ScanSceneObject(Object target)
        {
            var roots = new List<GameObject>(64);
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (scene.IsValid() && scene.isLoaded)
                    roots.AddRange(scene.GetRootGameObjects());
            }

            List<SceneRefObjectResult> sceneResults = SceneReferenceScanner.Scan(target, roots);

            var results = new List<UsageEntry>(sceneResults.Count);
            for (int i = 0; i < sceneResults.Count; i++)
            {
                SceneRefObjectResult go = sceneResults[i];
                string sceneName = go.gameObject != null ? go.gameObject.scene.name : "";

                var hits = new List<UsageFieldHit>(go.totalRefCount);
                for (int c = 0; c < go.components.Count; c++)
                {
                    SceneRefComponentResult comp = go.components[c];
                    for (int f = 0; f < comp.fields.Count; f++)
                    {
                        SceneRefFieldResult field = comp.fields[f];
                        hits.Add(new UsageFieldHit(
                            UsageNavKind.OpenSceneField,
                            "",                      // ownerLabel để trống — field.displayLabel đã là "ComponentName > path"
                            field.displayLabel,
                            field.propertyPath,
                            go.gameObject,
                            comp.component));
                    }
                }

                results.Add(UsageEntry.ForSceneObject(go.gameObject, sceneName, hits));
            }

            return results;
        }

        // ──────────────── Filter ────────────────

        private void RebuildFilteredResults()
        {
            if (string.IsNullOrEmpty(_filterText))
            {
                _filteredResults = _results;
                return;
            }

            if (_filteredResults == null || _filteredResults == _results)
                _filteredResults = new List<UsageEntry>();
            else
                _filteredResults.Clear();

            string filter = _filterText;
            for (int i = 0; i < _results.Count; i++)
            {
                if (MatchesFilter(_results[i], filter))
                    _filteredResults.Add(_results[i]);
            }
        }

        private static bool MatchesFilter(UsageEntry e, string filter)
        {
            if (e.displayLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || e.pathLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0
                || e.typeName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            for (int h = 0; h < e.hits.Count; h++)
            {
                if (e.hits[h].displayLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        // ──────────────── Helpers ────────────────

        private static int CountHits(List<UsageEntry> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
                count += list[i].HitCount;
            return count;
        }

        private static void DrawSeparator()
        {
            Rect r = GUILayoutUtility.GetRect(GUIContent.none, GUIStyle.none, GUILayout.Height(1));
            EditorGUI.DrawRect(r, StaticColor.SeparatorColor);
        }

        private void HandleScrollWheelBoost()
        {
            if (Event.current.type != EventType.ScrollWheel) return;
            _scroll += Event.current.delta * 20f;
            Event.current.Use();
        }
    }
}

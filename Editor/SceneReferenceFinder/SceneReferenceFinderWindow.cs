using System;
using System.Collections.Generic;
using System.Text;
using Horcrux.Editor.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.SceneReferenceFinder
{
    /// <summary>
    /// Scene Reference Finder — "xóa GameObject/Component này có gây null reference ở đâu không?".
    /// Quét scope (Active Scene hoặc Prefab Stage đang mở) tìm mọi component trỏ tới target.
    /// </summary>
    public sealed class SceneReferenceFinderWindow : EditorWindow
    {
        private enum Scope { LoadedScenes, PrefabStage }

        // ──────────────── Static eager cache ────────────────

        private static readonly GUIContent ClearBtnLabel = new("✕");
        private static readonly GUIContent ScopeScene     = new("Loaded Scenes", "Scan every loaded scene (including additively-loaded ones), children included");
        private static readonly GUIContent ScopePrefab    = new("Prefab Stage", "Scan the prefab currently open in Prefab Mode");
        private static readonly GUIContent TargetLabel    = new("Target", "GameObject hoặc Component cần kiểm tra — ai đang trỏ tới nó trong scope này. Kéo vào đây hoặc bấm \"Use Selected\"");
        private static readonly GUIContent UseSelected    = new("Use Selected", "Set target from the current Hierarchy selection");
        private static readonly GUIContent ScanBtnLabel   = new("🔍 Find References");

        private static readonly StringBuilder SharedSB = new(64);

        // ──────────────── Per-instance state ────────────────

        private Scope    _scope = Scope.LoadedScenes;
        private Object   _target;
        private string   _filterText = "";
        private Vector2  _scroll;

        private List<SceneRefObjectResult> _results;
        private List<SceneRefObjectResult> _filteredResults;
        private bool _filterDirty = true;

        private SceneReferenceDrawer _drawer;

        private readonly GUIContent _statusContent = new();
        private int  _lastStatusCount = -1;
        private bool _lastStatusHasFilter;

        // ──────────────── Menu ────────────────

        [MenuItem("Horcrux/Scene Reference Finder")]
        private static void ShowWindow()
        {
            var window = GetWindow<SceneReferenceFinderWindow>();
            window.titleContent = new GUIContent("Scene Reference Finder");
            window.Show();
        }

        /// <summary>Context menu Hierarchy: right-click GameObject → Find References In Scene.</summary>
        [MenuItem("GameObject/Find References In Scene (Horcrux)", false, 30)]
        private static void FindFromHierarchy()
        {
            var window = GetWindow<SceneReferenceFinderWindow>();
            window.titleContent = new GUIContent("Scene Reference Finder");
            window._target = Selection.activeGameObject;
            window.SyncScopeToTarget();
            window.ExecuteScan();
            window.Show();
        }

        // Chỉ hiện khi chọn đúng 1 GameObject scene (không phải asset)
        [MenuItem("GameObject/Find References In Scene (Horcrux)", true)]
        private static bool FindFromHierarchyValidate()
        {
            return Selection.activeGameObject != null
                   && Selection.activeGameObject.scene.IsValid();
        }

        // ──────────────── OnGUI ────────────────

        private void OnGUI()
        {
            StaticStyles.Ensure();
            _drawer ??= new SceneReferenceDrawer();

            HandleScrollWheelBoost();

            if (Event.current.type == EventType.Layout && _filterDirty && _results != null)
            {
                RebuildFilteredResults();
                _filterDirty = false;
            }

            DrawScopeBar();
            DrawTargetRow();
            DrawFilterBar();
            DrawSeparator();

            _scroll = GUILayout.BeginScrollView(_scroll);
            _drawer.Draw(_results, _filteredResults ?? _results);
            GUILayout.EndScrollView();

            DrawStatusBar();
        }

        // ──────────────── Scope bar ────────────────

        private void DrawScopeBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            DrawScopeToggle(Scope.LoadedScenes, ScopeScene);
            DrawScopeToggle(Scope.PrefabStage, ScopePrefab);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawScopeToggle(Scope scope, GUIContent content)
        {
            bool active = _scope == scope;
            Color saved = GUI.backgroundColor;
            if (active) GUI.backgroundColor = StaticColor.ScopeActiveColor;

            bool toggled = GUILayout.Toggle(active, content, EditorStyles.toolbarButton, GUILayout.ExpandWidth(true));

            GUI.backgroundColor = saved;
            if (toggled && !active)
                _scope = scope;
        }

        // ──────────────── Target row ────────────────

        private void DrawTargetRow()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            _target = EditorGUILayout.ObjectField(TargetLabel, _target, typeof(Object), true);

            if (GUILayout.Button(UseSelected, EditorStyles.toolbarButton, GUILayout.Width(90)))
            {
                if (Selection.activeGameObject != null)
                {
                    _target = Selection.activeGameObject;
                    SyncScopeToTarget();
                }
            }

            EditorGUILayout.EndHorizontal();

            Color savedBg = GUI.backgroundColor;
            GUI.backgroundColor = StaticColor.ScanBtnColor;
            if (GUILayout.Button(ScanBtnLabel, StaticGUILayout.ScanFullWidth))
                ExecuteScan();
            GUI.backgroundColor = savedBg;
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

            int count = CountRefs(_filteredResults ?? _results);
            bool hasFilter = !string.IsNullOrEmpty(_filterText);

            if (_lastStatusCount != count || _lastStatusHasFilter != hasFilter)
            {
                _lastStatusCount = count;
                _lastStatusHasFilter = hasFilter;

                SharedSB.Clear();
                int goCount = (_filteredResults ?? _results).Count;
                SharedSB.Append(count).Append(count == 1 ? " reference" : " references")
                        .Append(" in ").Append(goCount).Append(goCount == 1 ? " GameObject" : " GameObjects");
                if (hasFilter)
                    SharedSB.Append(" (filtered)");
                _statusContent.text = SharedSB.ToString();
            }

            DrawSeparator();
            GUILayout.Label(_statusContent, EditorStyles.centeredGreyMiniLabel);
        }

        // ──────────────── Scan ────────────────

        private void ExecuteScan()
        {
            if (_target == null)
            {
                EditorUtility.DisplayDialog("Scene Reference Finder",
                    "No target set.\nSelect a GameObject/Component and click \"Use Selected\".", "OK");
                return;
            }

            IList<GameObject> roots = GetScopeRoots();
            if (roots == null || roots.Count == 0)
            {
                EditorUtility.DisplayDialog("Scene Reference Finder",
                    _scope == Scope.PrefabStage
                        ? "No Prefab Stage open.\nOpen a prefab in Prefab Mode first."
                        : "No loaded scene has root GameObjects.", "OK");
                return;
            }

            _results = SceneReferenceScanner.Scan(_target, roots);
            _scroll = Vector2.zero;
            _filterDirty = true;
            Repaint();
        }

        // Reusable buffer — gom root của mọi scene loaded (grow-only, tránh alloc mỗi lần scan).
        private readonly List<GameObject> _scopeRoots = new(64);

        /// <summary>Root GameObjects theo scope hiện tại.</summary>
        private IList<GameObject> GetScopeRoots()
        {
            if (_scope == Scope.PrefabStage)
            {
                var stage = PrefabStageUtility.GetCurrentPrefabStage();
                return stage != null ? new[] { stage.prefabContentsRoot } : null;
            }

            // Mọi scene đang loaded (kể cả additive) — không chỉ active scene, để không bỏ sót
            // referencer ở scene phụ rồi báo nhầm "safe to delete".
            _scopeRoots.Clear();
            for (int s = 0; s < SceneManager.sceneCount; s++)
            {
                Scene scene = SceneManager.GetSceneAt(s);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                _scopeRoots.AddRange(scene.GetRootGameObjects());
            }
            return _scopeRoots;
        }

        /// <summary>Đặt scope khớp nơi target đang sống (prefab stage vs scene).</summary>
        private void SyncScopeToTarget()
        {
            GameObject go = _target as GameObject;
            if (go == null && _target is Component c) go = c.gameObject;
            if (go == null) return;

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            _scope = stage != null && stage.IsPartOfPrefabContents(go)
                ? Scope.PrefabStage
                : Scope.LoadedScenes;
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
                _filteredResults = new List<SceneRefObjectResult>();
            else
                _filteredResults.Clear();

            // Zero-alloc: thêm thẳng GO result gốc khi bất kỳ phần nào match (name / component / field).
            // Không tạo SceneRefObjectResult/List mới cho mỗi ký tự gõ — filter chỉ thu hẹp danh sách GO,
            // trong 1 GO đã match vẫn hiện đủ component (đồng nhất với cách filter của UsageFinder).
            string filter = _filterText;
            for (int i = 0; i < _results.Count; i++)
            {
                if (MatchesFilter(_results[i], filter))
                    _filteredResults.Add(_results[i]);
            }
        }

        /// <summary>True nếu GO name, tên component, hoặc field label bất kỳ chứa <paramref name="filter"/>.</summary>
        private static bool MatchesFilter(SceneRefObjectResult go, string filter)
        {
            if (go.gameObject != null
                && go.gameObject.name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            for (int c = 0; c < go.components.Count; c++)
            {
                SceneRefComponentResult comp = go.components[c];
                if (comp.componentName.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;

                for (int f = 0; f < comp.fields.Count; f++)
                {
                    if (comp.fields[f].displayLabel.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                        return true;
                }
            }
            return false;
        }

        // ──────────────── Helpers ────────────────

        private static int CountRefs(List<SceneRefObjectResult> list)
        {
            int count = 0;
            for (int i = 0; i < list.Count; i++)
                count += list[i].totalRefCount;
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

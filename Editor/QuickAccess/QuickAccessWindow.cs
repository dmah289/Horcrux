using System;
using System.Collections.Generic;
using Horcrux.Editor.Serializables;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
using Horcrux.Editor.Common;

namespace Horcrux.Editor.QuickAccess
{
    public class QuickAccessWindow : EditorWindow
    {
        // ──────────────── Types ────────────────

        private enum ItemSource { Loaded, Pinned }
        
        // Theme-aware separator (translucent line under section titles).
        private static Color SeparatorColor =>
            EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f, 0.10f) : new Color(0f, 0f, 0f, 0.15f);

        // ──────────────── Build-scenes cache (shared across windows) ────────────────

        private static string[] _buildSceneNames  = Array.Empty<string>();
        private static string[] _buildScenePaths  = Array.Empty<string>();
        private static bool     _buildScenesDirty = true;

        // ──────────────── Per-instance state ────────────────

        private QuickAccessConfig _config;
        private Vector2           _scroll;
        private readonly List<Rect> _groupRects = new();
        private int  _hoveredGroup = -1;
        private bool _hoverPinMode;
        private int  _pendingRemoveGroup = -1;

        // ──────────────── Lifecycle ────────────────

        [MenuItem("Horcrux/Quick Access")]
        private static void ShowWindow()
        {
            var window = GetWindow<QuickAccessWindow>();
            window.titleContent = new GUIContent("Quick Access");
            window.Show();
        }

        private void OnEnable()
        {
            MarkBuildScenesDirty();
        }

        private void OnGUI()
        {
            StaticStyles.Ensure();
            StaticGUIContent.EnsureIcons();
            _config = QuickAccessConfig.instance;
            EnsureSceneNames();

            if (Event.current.type == EventType.Repaint)
                _groupRects.Clear();

            HandleScrollWheelBoost();

            DrawToolbar();

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawFavouriteSection();
            DrawGroupsSection();
            HandleDragAndDrop();
            GUILayout.EndScrollView();

            ApplyPendingRemovals();
        }

        // ──────────────── Scene-name cache (for loading-scene picker) ────────────────

        private static void EnsureSceneNames()
        {
            if (!_buildScenesDirty) return;

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            int n = scenes.Length;
            if (_buildSceneNames.Length != n) _buildSceneNames = new string[n];
            if (_buildScenePaths.Length != n) _buildScenePaths = new string[n];

            for (int i = 0; i < n; i++)
            {
                string path  = scenes[i].path;
                Object asset = AssetDatabase.LoadAssetAtPath<Object>(path);
                _buildScenePaths[i] = path;
                _buildSceneNames[i] = asset != null ? asset.name : "<missing>";
            }

            _buildScenesDirty = false;
        }

        private static void MarkBuildScenesDirty() => _buildScenesDirty = true;

        // ──────────────── Toolbar ────────────────

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            if (GUILayout.Button(StaticGUIContent.AddGroup, EditorStyles.toolbarButton, StaticGUILayout.TbAddGroup))
                _config.AddGroup("New Group", true, true);

            GUILayout.FlexibleSpace();

            GUIContent dropdownLabel = StaticGUIContent.LoadingButton(_config.loadingSceneName);
            if (EditorGUILayout.DropdownButton(dropdownLabel, FocusType.Keyboard, EditorStyles.toolbarDropDown, StaticGUILayout.TbScene))
                ShowLoadingSceneMenu();

            EditorGUILayout.EndHorizontal();
        }

        private void ShowLoadingSceneMenu()
        {
            var menu = new GenericMenu();
            if (_buildSceneNames.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("No scenes in Build Settings"));
                menu.ShowAsContext();
                return;
            }

            string current = _config.loadingSceneName;
            for (int i = 0; i < _buildSceneNames.Length; i++)
            {
                string name = _buildSceneNames[i]; // captured per-iteration by the closure below
                menu.AddItem(new GUIContent(name), name == current, () =>
                {
                    _config.loadingSceneName = name;
                    _config.Persist();
                });
            }
            menu.ShowAsContext();
        }

        private void RefreshGroup(QuickAccessGroup group)
        {
            group.RebuildLoaded();
            Repaint();
        }

        // ──────────────── Top-level sections ────────────────

        private void DrawFavouriteSection()
        {
            GUILayout.Space(10);

            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(_config.loadingSceneName)))
            {
                Color savedBg = GUI.backgroundColor;
                GUI.backgroundColor = StaticColor.PlayTintColor;
                if (GUILayout.Button(StaticGUIContent.PlayGame, StaticStyles.PlayButton, StaticGUILayout.PlayBtn))
                    PlayLoadingScene();
                GUI.backgroundColor = savedBg;
            }

            GUILayout.Space(8);
        }

        private void DrawGroupsSection()
        {
            if (_config.groups == null) return;
            for (int i = 0; i < _config.groups.Count; i++)
                DrawGroup(i, _config.groups[i]);
        }

        // ──────────────── Group rendering ────────────────

        private void DrawGroup(int index, QuickAccessGroup group)
        {
            bool hovered = _hoveredGroup == index;
            Color savedBg = default;

            if (hovered)
            {
                savedBg = GUI.backgroundColor;
                GUI.backgroundColor = _hoverPinMode ? StaticColor.PinHoverColor : StaticColor.RootHoverColor;
            }

            EditorGUILayout.BeginVertical(StaticStyles.GroupBox);
            if (hovered) GUI.backgroundColor = savedBg;

            DrawGroupHeader(index, group);
            if (group.editExpanded)    DrawGroupEditor(index, group);
            if (group.foldoutExpanded) DrawGroupItems(index, group);

            EditorGUILayout.EndVertical();

            if (Event.current.type == EventType.Repaint)
                _groupRects.Add(GUILayoutUtility.GetLastRect());
        }

        private void DrawGroupHeader(int index, QuickAccessGroup group)
        {
            EditorGUILayout.BeginHorizontal();

            string title = string.IsNullOrEmpty(group.title) ? "unnamed" : group.title;
            string sessionKey = group.FoldoutSessionKey;
            group.foldoutExpanded = SessionState.GetBool(sessionKey, false);
            group.foldoutExpanded = EditorGUILayout.Foldout(group.foldoutExpanded, title, true, StaticStyles.GroupTitle);
            SessionState.SetBool(sessionKey, group.foldoutExpanded);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button(StaticGUIContent.Edit, EditorStyles.miniButton, StaticGUILayout.Mini40))
                group.editExpanded = !group.editExpanded;
            if (GUILayout.Button(StaticGUIContent.Refresh, EditorStyles.miniButton, StaticGUILayout.Mini24))
                RefreshGroup(group);

            int last = _config.groups.Count - 1;
            using (new EditorGUI.DisabledScope(index == 0))
                if (GUILayout.Button(StaticGUIContent.MoveUp,   EditorStyles.miniButton, StaticGUILayout.Mini24)) _config.MoveGroup(index, -1);
            using (new EditorGUI.DisabledScope(index >= last))
                if (GUILayout.Button(StaticGUIContent.MoveDown, EditorStyles.miniButton, StaticGUILayout.Mini24)) _config.MoveGroup(index, +1);

            if (DangerButton(StaticGUIContent.Trash, EditorStyles.miniButton, StaticGUILayout.Mini24))
                ConfirmRemoveGroup(index, group);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawGroupEditor(int index, QuickAccessGroup group)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            float savedLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;

            EditorGUI.BeginChangeCheck();
            string title             = EditorGUILayout.TextField(StaticGUIContent.TitleField, group.title);
            bool   loadFiles         = EditorGUILayout.Toggle(StaticGUIContent.LoadFilesFromRoots, group.enableLoadingFilesFromRootFolders);
            bool   loadSubfolders    = EditorGUILayout.Toggle(StaticGUIContent.LoadSubfoldersFromRoots, group.enableLoadingSubfoldersFromRootFolders);
            bool   loadRecursively   = EditorGUILayout.Toggle(StaticGUIContent.LoadRecursively, group.enableLoadingRecursively);
            if (EditorGUI.EndChangeCheck())
            {
                group.title          = title;
                group.enableLoadingFilesFromRootFolders = loadFiles;
                group.enableLoadingSubfoldersFromRootFolders = loadSubfolders;
                group.enableLoadingRecursively = loadRecursively;
                group.RebuildLoaded();
                _config.Persist();
            }

            GUILayout.Space(4);
            EditorGUILayout.LabelField(StaticGUIContent.RootFolders, EditorStyles.boldLabel);
            DrawRootsEditor(index, group);

            EditorGUIUtility.labelWidth = savedLabelWidth;
            EditorGUILayout.EndVertical();
        }

        private void DrawRootsEditor(int index, QuickAccessGroup group)
        {
            List<FolderReference> roots = group.roots;
            if (roots == null || roots.Count == 0)
            {
                GUILayout.Label(StaticGUIContent.EmptyRoots, StaticStyles.DropHint);
                return;
            }

            int removeAt = -1;
            for (int j = 0; j < roots.Count; j++)
            {
                EditorGUILayout.BeginHorizontal();

                FolderReference fr = roots[j];
                // Single GUIDToAssetPath call — IsValid would call Path again under the hood.
                string path  = fr != null ? fr.Path : null;
                bool   valid = !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);

                using (new EditorGUI.DisabledScope(!valid))
                {
                    if (GUILayout.Button(valid ? path : "<invalid>", EditorStyles.objectField))
                    {
                        Object folder = AssetDatabase.LoadAssetAtPath<Object>(path);
                        if (folder != null) EditorGUIUtility.PingObject(folder);
                    }
                }

                if (DangerButton(StaticGUIContent.TrashSmall, StaticGUILayout.Mini24)) removeAt = j;

                EditorGUILayout.EndHorizontal();
            }

            if (removeAt >= 0) _config.RemoveRootFolderFromGroup(index, removeAt);
        }

        private void DrawGroupItems(int index, QuickAccessGroup group)
        {
            bool hasPinned = HasItems(group.pinnedAssets) || HasItems(group.pinnedFolders);
            bool hasLoaded = (group.enableLoadingFilesFromRootFolders      && HasItems(group.loadedAssets))
                          || (group.enableLoadingSubfoldersFromRootFolders && HasItems(group.loadedSubfolders));

            if (hasPinned)
            {
                DrawSubsectionLabel(StaticGUIContent.PinnedSection);
                DrawAssetRows(group.pinnedAssets,  ItemSource.Pinned, index);
                DrawFolderRows(group.pinnedFolders, ItemSource.Pinned, index);
            }

            if (hasLoaded)
            {
                if (hasPinned) GUILayout.Space(4);
                DrawSubsectionLabel(StaticGUIContent.LoadedSection);
                if (group.enableLoadingFilesFromRootFolders)      DrawAssetRows(group.loadedAssets,     ItemSource.Loaded, index);
                if (group.enableLoadingSubfoldersFromRootFolders) DrawFolderRows(group.loadedSubfolders, ItemSource.Loaded, index);
            }

            if (!hasPinned && !hasLoaded)
                GUILayout.Label(StaticGUIContent.EmptyGroup, StaticStyles.DropHint);
        }

        private void ConfirmRemoveGroup(int index, QuickAccessGroup group)
        {
            if (EditorUtility.DisplayDialog("Remove group",
                    $"Remove group '{group.title}'? This cannot be undone.", "Remove", "Cancel"))
                _pendingRemoveGroup = index;
        }

        // ──────────────── Item rendering ────────────────

        private void DrawAssetRows(List<Object> list, ItemSource source, int groupIndex)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                DrawAssetRow(list[i], source, groupIndex);
        }

        private void DrawFolderRows(List<Object> list, ItemSource source, int groupIndex)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
                DrawFolderRow(list[i], source, groupIndex);
        }

        private void DrawAssetRow(Object asset, ItemSource source = ItemSource.Loaded, int groupIndex = -1)
        {
            if (asset == null) return;

            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(StaticGUIContent.Ping, StaticGUILayout.Ping))
                EditorGUIUtility.PingObject(asset);

            if (GUILayout.Button(asset.name, StaticStyles.AssetButton, StaticGUILayout.AssetName)
                && AssetDatabase.OpenAsset(asset))
                EditorApplication.ExecuteMenuItem("Window/General/Inspector");
            
            if (DangerButton(StaticGUIContent.Remove, StaticGUILayout.Remove))
                RemoveAsset(asset, source, groupIndex);
            
            GUILayout.EndHorizontal();
        }

        private void DrawFolderRow(Object folder, ItemSource source = ItemSource.Loaded, int groupIndex = -1)
        {
            if (folder == null) return;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(folder.name, StaticStyles.AssetButton, StaticGUILayout.FolderName)) EditorGUIUtility.PingObject(folder);
            if (DangerButton(StaticGUIContent.Remove, StaticGUILayout.Remove))                          RemoveFolder(folder, source, groupIndex);
            GUILayout.EndHorizontal();
        }

        private void RemoveAsset(Object asset, ItemSource source, int groupIndex)
        {
            if (source == ItemSource.Pinned) _config.RemovePinnedFromGroup(groupIndex, asset);
            else                             _config.RemoveLoadedAsset(asset);
        }

        private void RemoveFolder(Object folder, ItemSource source, int groupIndex)
        {
            if (source == ItemSource.Pinned) _config.RemovePinnedFromGroup(groupIndex, folder);
            else                             _config.RemoveLoadedFolder(folder);
        }

        // ──────────────── Drag-drop ────────────────

        private void HandleDragAndDrop()
        {
            Event evt = Event.current;
            EventType et = evt.type;

            if (et == EventType.DragExited)
            {
                ClearHover();
                return;
            }
            if (et != EventType.DragUpdated && et != EventType.DragPerform) return;

            Object[] dragged = DragAndDrop.objectReferences;
            if (dragged.Length == 0) return;

            int targetGroup = FindGroupAt(evt.mousePosition);
            if (targetGroup < 0)
            {
                ClearHover();
                DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                evt.Use();
                return;
            }

            // Default is pin mode. Shift + all-folders → root (files cannot be roots).
            bool pinMode = !evt.shift || !AreAllFolders(dragged);

            UpdateHover(targetGroup, pinMode);
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

            if (et == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (pinMode) _config.AddPinnedToGroup(targetGroup, dragged);
                else         _config.AddRootFoldersToGroup(targetGroup, dragged);
                ClearHover();
            }
            evt.Use();
        }

        private static bool AreAllFolders(Object[] items)
        {
            for (int i = 0; i < items.Length; i++)
            {
                string p = AssetDatabase.GetAssetPath(items[i]);
                if (string.IsNullOrEmpty(p) || !AssetDatabase.IsValidFolder(p)) return false;
            }
            return true;
        }

        private int FindGroupAt(Vector2 pos)
        {
            for (int i = 0; i < _groupRects.Count; i++)
                if (_groupRects[i].Contains(pos)) return i;
            return -1;
        }

        private void UpdateHover(int group, bool pinMode)
        {
            if (_hoveredGroup == group && _hoverPinMode == pinMode) return;
            _hoveredGroup = group;
            _hoverPinMode = pinMode;
            Repaint();
        }

        private void ClearHover()
        {
            if (_hoveredGroup == -1 && !_hoverPinMode) return;
            _hoveredGroup = -1;
            _hoverPinMode = false;
            Repaint();
        }

        // ──────────────── Misc ────────────────

        private void HandleScrollWheelBoost()
        {
            if (Event.current.type != EventType.ScrollWheel) return;
            _scroll += Event.current.delta * 20f;
            Event.current.Use();
        }

        private void ApplyPendingRemovals()
        {
            if (_pendingRemoveGroup < 0) return;
            _config.RemoveGroup(_pendingRemoveGroup);
            _pendingRemoveGroup = -1;
            Repaint();
        }

        private void PlayLoadingScene()
        {
            string sceneName = _config.loadingSceneName;
            int idx = Array.IndexOf(_buildSceneNames, sceneName);
            if (idx < 0)
                throw new Exception($"Scene '{sceneName}' set in QuickAccessConfig is not in Build Settings.");

            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            EditorSceneManager.OpenScene(_buildScenePaths[idx]);
            EditorApplication.isPlaying = true;
        }

        // ──────────────── Layout helpers ────────────────

        private static void DrawSectionTitle(GUIContent label)
        {
            GUILayout.Space(4);
            GUILayout.Label(label, StaticStyles.SectionTitle);
            DrawSeparator();
            GUILayout.Space(2);
        }

        private static void DrawSubsectionLabel(GUIContent label)
        {
            GUILayout.Label(label, EditorStyles.miniBoldLabel);
        }

        private static void DrawSeparator()
        {
            Rect r = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(r, SeparatorColor);
        }

        private static bool HasItems(List<Object> list) => list != null && list.Count > 0;

        private static bool DangerButton(GUIContent content, GUIStyle style, GUILayoutOption[] options)
        {
            Color bg = GUI.backgroundColor;
            GUI.backgroundColor = StaticColor.DangerColor;
            bool clicked = GUILayout.Button(content, style, options);
            GUI.backgroundColor = bg;
            return clicked;
        }

        private static bool DangerButton(GUIContent content, GUILayoutOption[] options)
        {
            Color bg = GUI.backgroundColor;
            GUI.backgroundColor = StaticColor.DangerColor;
            bool clicked = GUILayout.Button(content, options);
            GUI.backgroundColor = bg;
            return clicked;
        }
    }
}

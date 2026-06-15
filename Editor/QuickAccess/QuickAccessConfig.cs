using System.Collections.Generic;
using Horcrux.Editor.Utilities;
using Horcrux.Editor.Serializables;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.QuickAccess
{
    /// <summary>
    /// Persistent state for the Quick Access window. Editor-only — the asset lives under
    /// <c>ProjectSettings/</c> so Unity excludes it from player builds. All mutations go through
    /// methods here so the file is saved exactly once per logical change.
    /// </summary>
    [FilePath("ProjectSettings/manhnd_sdk_QuickAccessConfig.asset", FilePathAttribute.Location.ProjectFolder)]
    public class QuickAccessConfig : ScriptableSingleton<QuickAccessConfig>
    {
        public string loadingSceneName;
        public List<QuickAccessGroup> groups;

        private void OnEnable()
        {
            groups ??= new List<QuickAccessGroup>();
        }

        // ──────────────── Group lifecycle ────────────────

        public void AddGroup(string title, bool enableLoadingFiles, bool enableLoadingSubfolders)
        {
            groups ??= new List<QuickAccessGroup>();
            groups.Add(new QuickAccessGroup
            {
                title = title,
                enableLoadingFilesFromRootFolders = enableLoadingFiles,
                enableLoadingSubfoldersFromRootFolders = enableLoadingSubfolders,
                editExpanded = true
            });
            Persist();
        }

        public void RemoveGroup(int index)
        {
            if (!IsValidGroupIndex(index)) return;
            groups.RemoveAt(index);
            Persist();
        }

        public void MoveGroup(int index, int delta)
        {
            int target = index + delta;
            if (!IsValidGroupIndex(index) || !IsValidGroupIndex(target)) return;
            (groups[index], groups[target]) = (groups[target], groups[index]);
            Persist();
        }

        // ──────────────── Root folders ────────────────

        public void AddRootFoldersToGroup(int groupIndex, IList<Object> folders)
        {
            if (folders == null || folders.Count == 0) return;
            if (!TryGetGroup(groupIndex, out QuickAccessGroup group)) return;

            bool anyAdded = false;
            for (int i = 0; i < folders.Count; i++)
                anyAdded |= TryAppendRoot(group, folders[i]);

            if (!anyAdded) return;
            group.RebuildLoaded();
            Persist();
        }

        public void RemoveRootFolderFromGroup(int groupIndex, int folderIndex)
        {
            if (!TryGetGroup(groupIndex, out QuickAccessGroup group)) return;
            if (group.roots == null) return;
            if (folderIndex < 0 || folderIndex >= group.roots.Count) return;

            group.roots.RemoveAt(folderIndex);
            group.RebuildLoaded();
            Persist();
        }

        // ──────────────── Pinned (per-group shortcuts) ────────────────

        public void AddPinnedToGroup(int groupIndex, IList<Object> items)
        {
            if (items == null || items.Count == 0) return;
            if (!TryGetGroup(groupIndex, out QuickAccessGroup group)) return;

            bool anyAdded = false;
            for (int i = 0; i < items.Count; i++)
                anyAdded |= TryAppendPinned(group, items[i]);

            if (anyAdded) Persist();
        }

        // GUID is unique across assets and folders, so scanning both buckets is unambiguous.
        public void RemovePinnedFromGroup(int groupIndex, Object target)
        {
            if (!TryGetGroup(groupIndex, out QuickAccessGroup group)) return;

            string targetGuid = AssetDatabaseUtility.GetGuid(target);
            bool removed = RemoveAllByGuid(group.pinnedAssets,  targetGuid)
                         | RemoveAllByGuid(group.pinnedFolders, targetGuid);

            if (removed) Persist();
        }

        // ──────────────── Loaded cache (rebuilt by Refresh) ────────────────

        // Removes the asset from its group's loaded cache. Refresh will repopulate from root folders;
        // use this to hide noise temporarily, not as a permanent delete.
        public void RemoveLoadedAsset(Object target)  => RemoveFromAllGroupCaches(target, isFolder: false);
        public void RemoveLoadedFolder(Object target) => RemoveFromAllGroupCaches(target, isFolder: true);

        private void RemoveFromAllGroupCaches(Object target, bool isFolder)
        {
            if (groups == null) return;

            string targetGuid = AssetDatabaseUtility.GetGuid(target);
            bool removed = false;
            for (int i = 0; i < groups.Count; i++)
            {
                List<Object> list = isFolder ? groups[i].loadedSubfolders : groups[i].loadedAssets;
                removed |= RemoveAllByGuid(list, targetGuid);
            }
            if (removed) Persist();
        }

        // ──────────────── Helpers ────────────────

        // Writes the singleton to its FilePath as text (yaml). Call after every logical mutation
        // so the file survives crashes — ScriptableSingleton has no AssetDatabase auto-save cycle.
        public void Persist() => Save(saveAsText: true);

        private bool IsValidGroupIndex(int index) =>
            groups != null && index >= 0 && index < groups.Count;

        private bool TryGetGroup(int index, out QuickAccessGroup group)
        {
            if (IsValidGroupIndex(index)) { group = groups[index]; return true; }
            group = null;
            return false;
        }

        private static bool TryAppendRoot(QuickAccessGroup group, Object folder)
        {
            if (folder == null) return false;

            string path = AssetDatabase.GetAssetPath(folder);
            if (string.IsNullOrEmpty(path) || !AssetDatabase.IsValidFolder(path)) return false;

            group.roots ??= new List<FolderReference>();

            for (int i = 0; i < group.roots.Count; i++)
                if (group.roots[i] != null && group.roots[i].Path == path) return false;

            group.roots.Add(new FolderReference(path));
            return true;
        }

        private static bool TryAppendPinned(QuickAccessGroup group, Object item)
        {
            if (item == null) return false;

            string path = AssetDatabase.GetAssetPath(item);
            if (string.IsNullOrEmpty(path)) return false;

            List<Object> bucket;
            if (AssetDatabase.IsValidFolder(path))
            {
                group.pinnedFolders ??= new List<Object>();
                bucket = group.pinnedFolders;
            }
            else
            {
                group.pinnedAssets ??= new List<Object>();
                bucket = group.pinnedAssets;
            }

            if (bucket.Contains(item)) return false;
            bucket.Add(item);
            return true;
        }

        private static bool RemoveAllByGuid(List<Object> list, string targetGuid)
        {
            if (list == null) return false;

            bool removed = false;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (AssetDatabaseUtility.GetGuid(list[i]).Equals(targetGuid))
                {
                    list.RemoveAt(i);
                    removed = true;
                }
            }
            return removed;
        }
    }
}

using System;
using System.Collections.Generic;
using Horcrux.Editor.Serializables;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.QuickAccess
{
    /// <summary>
    /// One section in the Quick Access window.
    ///   • <c>roots</c> — folders scanned on Refresh to populate the loaded cache.
    ///   • <c>pinnedAssets</c> / <c>pinnedFolders</c> — direct shortcuts; folders here are reference-only.
    ///   • <c>loadedAssets</c> / <c>loadedSubfolders</c> — derived cache rebuilt by <see cref="RebuildLoaded"/>.
    /// </summary>
    [Serializable]
    public class QuickAccessGroup
    {
        public string title;
        public bool enableLoadingFilesFromRootFolders;
        public bool enableLoadingSubfoldersFromRootFolders;
        public bool enableLoadingRecursively;
        public List<FolderReference> roots;

        public List<Object> pinnedAssets;
        public List<Object> pinnedFolders;

        [HideInInspector] public List<Object> loadedAssets;
        [HideInInspector] public List<Object> loadedSubfolders;

        [NonSerialized] public bool foldoutExpanded;
        [NonSerialized] public bool editExpanded;

        public void RebuildLoaded()
        {
            loadedAssets ??= new List<Object>();
            loadedSubfolders ??= new List<Object>();

            loadedAssets.Clear();
            loadedSubfolders.Clear();

            if (roots == null) return;

            for (int i = 0; i < roots.Count; i++)
            {
                FolderReference root = roots[i];
                if (root == null || !root.IsValid) continue;

                if (enableLoadingFilesFromRootFolders) 
                    loadedAssets.AddRange(root.LoadAssets<Object>(enableLoadingRecursively));
                
                if (enableLoadingSubfoldersFromRootFolders)
                    loadedSubfolders.AddRange(root.GetSubFolders());
            }
        }
    }
}

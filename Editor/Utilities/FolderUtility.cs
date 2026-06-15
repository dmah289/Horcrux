using UnityEditor;

namespace Horcrux.Editor.Utilities
{
    public static class FolderUtility
    {
        public static void CreateFolderRecursively(string path)
        {
            if (path == null)
                return;
            
            path = path.Replace("\\", "/").Trim('/');

            string[] folderNames = path.Split('/');
            
            string currPath = folderNames[0];
            for (int i = 1; i < folderNames.Length; i++)
            {
                string fullPath = currPath + "/" + folderNames[i];
                if (!AssetDatabase.IsValidFolder(fullPath))
                    AssetDatabase.CreateFolder(currPath, folderNames[i]);
                currPath = fullPath;
            }
        }
    }
}
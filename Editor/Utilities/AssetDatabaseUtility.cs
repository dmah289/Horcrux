using UnityEngine;

namespace Horcrux.Editor.Utilities
{
    public static class AssetDatabaseUtility
    {
        public static string GetGuid(Object target)
        {
            string path = UnityEditor.AssetDatabase.GetAssetPath(target);
            return UnityEditor.AssetDatabase.AssetPathToGUID(path);
        }
    }
}
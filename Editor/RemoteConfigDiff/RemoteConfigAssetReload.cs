using Horcrux.Runtime.Implementations.RemoteConfigSystem;
using UnityEditor;
using UnityEngine;

namespace Horcrux.Editor.RemoteConfigDiff
{
    /// <summary>Thoát Play thì nạp lại asset remote config từ đĩa: giá trị remote ghi vào bản trong bộ nhớ lúc chạy không sống sang Edit mode.</summary>
    [InitializeOnLoad]
    public static class RemoteConfigAssetReload
    {
        static RemoteConfigAssetReload() => EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredEditMode)
                ReloadAll();
        }

        /// <summary>Nạp lại mọi collection đang nằm trong bộ nhớ; trả số asset đã nạp lại. Asset có thay đổi chưa lưu thì giữ nguyên và cảnh báo.</summary>
        public static int ReloadAll()
        {
            var reloaded = 0;
            foreach (var collection in Resources.FindObjectsOfTypeAll<BaseRemoteConfigCollection>())
            {
                var path = AssetDatabase.GetAssetPath(collection);
                if (string.IsNullOrEmpty(path)) continue;

                // Nạp lại sẽ bỏ thay đổi developer chưa lưu: để họ quyết.
                if (EditorUtility.IsDirty(collection))
                {
                    Debug.LogWarning($"{path} có thay đổi chưa lưu nên không nạp lại sau Play: Value có thể đang là giá trị remote, soát diff trước khi lưu.", collection);
                    continue;
                }

                Resources.UnloadAsset(collection);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                reloaded++;
            }

            return reloaded;
        }
    }
}

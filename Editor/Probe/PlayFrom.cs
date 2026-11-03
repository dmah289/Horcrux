using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Horcrux.Editor.Probe
{
    /// <summary>
    /// Vào Play từ scene đầu của Build Settings dù đang mở scene hay prefab nào, như build thật boot. Scene khởi động cũ của
    /// Editor (<c>EditorSceneManager.playModeStartScene</c>) được nhớ qua domain reload bằng SessionState và trả lại khi về Edit
    /// mode, kể cả khi Play bị dừng tay. Trong Play bật Application.runInBackground: Editor mất focus thì frame không đứng.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFrom
    {
        private const string ArmedKey = "Horcrux.PlayFrom.Armed";
        private const string PreviousKey = "Horcrux.PlayFrom.Previous";

        static PlayFrom() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        /// <summary>Đặt scene đầu của Build Settings làm scene khởi động cho lần Play tới rồi vào Play.</summary>
        public static string FirstBuildScene()
        {
            var armed = Arm();
            EditorApplication.EnterPlaymode();
            return armed;
        }

        /// <summary>Chỉ đặt scene khởi động, không vào Play: cho runner tự vào Play (playrun.sh).</summary>
        public static string Arm()
        {
            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0)
                return "Build Settings không có scene";
            var first = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenes[0].path);
            if (!SessionState.GetBool(ArmedKey, false))
            {
                var previous = EditorSceneManager.playModeStartScene;
                SessionState.SetString(PreviousKey, previous != null ? AssetDatabase.GetAssetPath(previous) : "");
                SessionState.SetBool(ArmedKey, true);
            }
            EditorSceneManager.playModeStartScene = first;
            return scenes[0].path;
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (!SessionState.GetBool(ArmedKey, false))
                return;
            if (change == PlayModeStateChange.EnteredPlayMode)
                Application.runInBackground = true;
            if (change != PlayModeStateChange.EnteredEditMode)
                return;
            var previous = SessionState.GetString(PreviousKey, "");
            EditorSceneManager.playModeStartScene = previous.Length > 0 ? AssetDatabase.LoadAssetAtPath<SceneAsset>(previous) : null;
            SessionState.EraseBool(ArmedKey);
            SessionState.EraseString(PreviousKey);
        }
    }
}

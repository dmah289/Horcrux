using UnityEngine;
using UnityEditor;

namespace Horcrux.Editor.Utilities
{
    public class MissingScriptCleaner : UnityEditor.Editor
    {
        [MenuItem("Horcrux/Utility/Clean Missing Scripts")]
        public static void CleanMissingScripts()
        {
            GameObject[] selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("Thông báo", "Vui lòng chọn ít nhất 1 GameObject hoặc Prefab để dọn dẹp!", "OK");
                return;
            }

            int totalRemovedCount = 0;

            foreach (GameObject go in selectedObjects)
            {
                string assetPath = AssetDatabase.GetAssetPath(go);
                bool isPrefabAsset = !string.IsNullOrEmpty(assetPath);

                if (isPrefabAsset)
                {
                    // XỬ LÝ PREFAB TRONG THƯ MỤC PROJECT
                    // Load nội dung prefab vào một môi trường tạm thời để chỉnh sửa an toàn
                    GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);
                    
                    int removedCount = RemoveMissingScriptsRecursively(contentsRoot);
                    
                    if (removedCount > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);
                        Debug.Log($"<color=green>[Thành công]</color> Đã xóa {removedCount} missing scripts khỏi Prefab: <b>{go.name}</b>");
                        totalRemovedCount += removedCount;
                    }
                    
                    PrefabUtility.UnloadPrefabContents(contentsRoot);
                }
                else
                {
                    // XỬ LÝ GAMEOBJECT TRÊN SCENE
                    Undo.RegisterFullObjectHierarchyUndo(go, "Clean Missing Scripts");
                    int removedCount = RemoveMissingScriptsRecursively(go);
                    
                    if (removedCount > 0)
                    {
                        EditorUtility.SetDirty(go);
                        Debug.Log($"<color=green>[Thành công]</color> Đã xóa {removedCount} missing scripts khỏi Object: <b>{go.name}</b>");
                        totalRemovedCount += removedCount;
                    }
                }
            }

            if (totalRemovedCount > 0)
            {
                EditorUtility.DisplayDialog("Hoàn tất", $"Đã dọn dẹp tổng cộng {totalRemovedCount} Missing Scripts!", "Tuyệt vời");
            }
            else
            {
                Debug.Log("Không tìm thấy Missing Script nào trong các đối tượng đã chọn.");
            }
        }

        private static int RemoveMissingScriptsRecursively(GameObject go)
        {
            int count = 0;
            
            count += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            foreach (Transform child in go.transform)
                count += RemoveMissingScriptsRecursively(child.gameObject);

            return count;
        }
    }
}
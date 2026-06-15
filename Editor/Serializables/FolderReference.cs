using System;
using System.Collections.Generic;
using System.IO;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Editor.Serializables
{
    [Serializable, DrawWithUnity]
    public class FolderReference
    {
        [SerializeField] private string GUID;
        
        public string Path
        {
            get => AssetDatabase.GUIDToAssetPath(GUID);
            set => GUID = AssetDatabase.AssetPathToGUID(value);
        }

        public bool IsValid => !string.IsNullOrEmpty(GUID) && AssetDatabase.IsValidFolder(Path);

        public FolderReference(string path) => Path = path;

        public T[] LoadAssets<T>(bool loadRecursively = false) where T : Object
        {
            if (!IsValid)
                return Array.Empty<T>();

            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { Path });
            int rootDepth = loadRecursively ? -1 : Path.Split('/').Length;

            List<T> results = new();
            for (int i = 0; i < guids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (AssetDatabase.IsValidFolder(assetPath)) continue;
                if (!loadRecursively && assetPath.Split('/').Length != rootDepth + 1) continue;
                results.Add(AssetDatabase.LoadAssetAtPath<T>(assetPath));
            }

            return results.ToArray();
        }

        public Object[] GetSubFolders()
        {
            string[] paths = AssetDatabase.GetSubFolders(Path);
            var folders = new Object[paths.Length];
            for (int i = 0; i < paths.Length; i++)
                folders[i] = AssetDatabase.LoadAssetAtPath<Object>(paths[i]);
            return folders;
        }
    }

    [CustomPropertyDrawer(typeof(FolderReference))]
    public class FolderReferencePropertyDrawer : PropertyDrawer
    {
        private static GUIStyle _dragRefStyle;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SerializedProperty guid = property.FindPropertyRelative("GUID");
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guid.stringValue));
            GUIContent objContent = EditorGUIUtility.ObjectContent(obj, typeof(DefaultAsset));

            EditorGUI.BeginProperty(position, label, property);

            // rect after the label
            Rect remainingRect = EditorGUI.PrefixLabel(position, label);

            HandleDragRefRect(property, remainingRect, obj, objContent, guid, out Rect dragRefRect);

            HandleFolderSelectionRect(property, remainingRect, dragRefRect, guid);

            EditorGUI.EndProperty();
        }

        private void HandleDragRefRect(SerializedProperty property, Rect remainingRect, Object obj, GUIContent objContent,
            SerializedProperty guid, out Rect dragRefRect)
        {
            dragRefRect = remainingRect;
            dragRefRect.width -= 19f;

            if (_dragRefStyle == null)
                _dragRefStyle = new GUIStyle("TextField");
            _dragRefStyle.imagePosition = obj ? ImagePosition.ImageLeft : ImagePosition.TextOnly;

            if (GUI.Button(dragRefRect, objContent, _dragRefStyle) && obj)
                EditorGUIUtility.PingObject(obj);

            if (dragRefRect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.DragUpdated)
                {
                    Object reference = DragAndDrop.objectReferences[0];
                    string path = AssetDatabase.GetAssetPath(reference);
                    DragAndDrop.visualMode = Directory.Exists(path) ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
                    Event.current.Use();
                }
                else if (Event.current.type == EventType.DragPerform)
                {
                    Object reference = DragAndDrop.objectReferences[0];
                    string path = AssetDatabase.GetAssetPath(reference);
                    if (Directory.Exists(path))
                    {
                        guid.stringValue = AssetDatabase.AssetPathToGUID(path);
                        property.serializedObject.ApplyModifiedProperties();
                    }
                    Event.current.Use();
                }
            }
        }
        
        private void HandleFolderSelectionRect(SerializedProperty property, Rect remainingRect, Rect dragRefRect,
            SerializedProperty guid)
        {
            Object obj;
            Rect folderSelectionRect = remainingRect;
            folderSelectionRect.x = dragRefRect.xMax + 1;
            folderSelectionRect.width = 19f;

            if (GUI.Button(folderSelectionRect, "", GUI.skin.GetStyle("IN ObjectField")))
            {
                string path = EditorUtility.OpenFolderPanel("Select a folder", "Assets", "");
                if (path.Contains(Application.dataPath))
                {
                    path = "Assets" + path.Substring(Application.dataPath.Length);
                    obj = AssetDatabase.LoadAssetAtPath(path, typeof(DefaultAsset));
                    guid.stringValue = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(obj));
                    property.serializedObject.ApplyModifiedProperties();
                }
                else
                    Debug.LogError("The path must be in the Assets folder");
            }
        }
    }
}

using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Horcrux.Runtime.Abstractions.Singleton
{
    public abstract class SingletonSO<T> : ScriptableObject where T : ScriptableObject
    {
        private static T _instance;
        public static T Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to find an existing instance in the project
                    _instance = Resources.Load<T>(typeof(T).Name);
                    if (_instance == null)
                    {
                        Debug.LogError($"No instance of {typeof(T).Name} found in the project");
#if UNITY_EDITOR
                        if(!AssetDatabase.IsValidFolder("Assets/Resources"))
                            AssetDatabase.CreateFolder("Assets", "Resources");

                        _instance = CreateInstance<T>();
                        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"Assets/Resources/{typeof(T).Name}.asset");
                        AssetDatabase.CreateAsset(_instance, assetPath);

                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
#endif
                    }
                }
                return _instance;
            }
        }

#if UNITY_EDITOR
        protected static void PingSO()
        {
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = Instance;
            EditorGUIUtility.PingObject(Instance);
        }
#endif
    }
}

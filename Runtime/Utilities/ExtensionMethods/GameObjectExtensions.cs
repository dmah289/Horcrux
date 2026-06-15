using System;
using System.Linq;
using UnityEngine;

namespace manhnd_sdk.Scripts.ExtensionMethods
{
    public static class GameObjectExtensions
    {
        /// <summary>
        /// Get full path of a GameObject in hierarchy
        /// </summary>
        public static string Path(this GameObject go)
        {
            return string.Join("/",
                go.GetComponentsInParent<Transform>().Select(t => t.name).Reverse().ToArray());
        }
        
        /// <summary>
        /// Return existing component T on GameObject or add it if not found
        /// </summary>
        public static T GetOrAdd<T>(this GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if(!component) component = go.AddComponent<T>();
            return component;
        }
        
        /// <summary>
        /// Traverse all children of a GameObject and perform an action on each child
        /// </summary>
        /// <param name="reverseOrder">Control application action order</param>
        public static void PerformActionOnChildren(this GameObject parent, Action<GameObject> action, bool reverseOrder = false)
        {
            if (!reverseOrder)
            {
                for (int i = 0; i < parent.transform.childCount; i++)
                    action(parent.transform.GetChild(i).gameObject);
            }
            else
            {
                for (int i = parent.transform.childCount - 1; i >= 0; i--)
                    action(parent.transform.GetChild(i).gameObject);
            }
        }
        
        /// <summary>
        /// Recursively sets the provided layer for this GameObject and all of its descendants in the Unity scene hierarchy.
        /// </summary>
        public static void SetLayerRecursively(this GameObject go, int layer)
            => go.PerformActionOnChildren(child => child.layer = layer);
        
        /// <summary>
        /// Recursively sets the provided layer for this GameObject and all of its descendants in the Unity scene hierarchy.
        /// </summary>
        public static void SetLayerRecursively(this GameObject go, LayerMask layer)
            => go.PerformActionOnChildren(child => child.layer = layer.value);
    }
}
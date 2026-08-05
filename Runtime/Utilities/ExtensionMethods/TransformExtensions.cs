using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class TransformExtensions
    {
        public static T GetOrAdd<T>(this Transform t) where T : Component
        {
            T component = t.GetComponent<T>();
            if(!component) component = t.gameObject.AddComponent<T>();
            return component;
        }

        /// <summary>
        /// Traverse all children of a Transform and perform an action on each child
        /// </summary>
        public static void PerformActionOnChildren(this Transform parent, Action<Transform> action, bool reverseOrder = false)
        {
            if (!reverseOrder)
            {
                for (int i = 0; i < parent.childCount; i++)
                    action(parent.GetChild(i));
            }
            else
            {
                for (int i = parent.childCount - 1; i >= 0; i--)
                    action(parent.GetChild(i));
            }
        }
    }
}
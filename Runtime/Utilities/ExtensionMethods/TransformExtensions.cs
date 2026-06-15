using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace manhnd_sdk.Scripts.ExtensionMethods
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Return existing component T on Transform or add it if not found
        /// </summary>
        public static T GetOrAdd<T>(this Transform t) where T : Component
        {
            T component = t.GetComponent<T>();
            if(!component) component = t.gameObject.AddComponent<T>();
            return component;
        }
        
        /// <summary>
        /// Define own GetChildren method because Transform implements IEnumerable <br></br>
        /// NOTES : Should be used in Linq queries
        /// </summary>
        public static IEnumerable<Transform> Children(this Transform parent)
        {
            foreach (Transform child in parent)
            {
                yield return child;
            }
        }

        /// <summary>
        /// Traverse all children of a Transform and perform an action on each child
        /// </summary>
        /// <param name="reverseOrder">Control application action order</param>
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
        
        public static void EnableChildren(this Transform parent)
            => parent.PerformActionOnChildren(child => child.gameObject.SetActive(true));
        
        public static void DisableChildren(this Transform parent)
            => parent.PerformActionOnChildren(child => child.gameObject.SetActive(false));

        public static void Reset(this Transform t)
        {
            t.position = Vector3.zero;
            t.rotation = Quaternion.identity;
            t.localScale = Vector3.one;
        }
        
        /// <summary>
        /// Check if the transform is within a certain distance and optionally within a certain angle (FOV) from the target transform. <br></br>
        /// Check is done on the XZ plane only (Y axis is ignored).
        /// </summary>
        /// <param name="maxAngle">The maximum allowed angle between the transform's forward vector and the direction to the target</param>
        public static bool InRangeOf(this Transform source, Transform target, float range, float maxAngle = 360f)
        { 
            Vector3 directionToTarget = (target.position - source.position).With(y: 0);
            return directionToTarget.magnitude <= range && Vector3.Angle(source.forward, directionToTarget) <= maxAngle / 2f;
            
            
        }
    }
}
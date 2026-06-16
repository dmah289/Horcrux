using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class GenericsExtensions
    {
        /// <summary>
        /// Return real null in C# (not fake null of UnityEngine.Object)
        /// </summary>
        public static T OrNull<T>(this T obj) where T : Object => obj ? obj : null;
        
        public static void SetActive<T>(this T obj, bool active) where T : Component 
            => obj.gameObject.SetActive(active);
    }
}
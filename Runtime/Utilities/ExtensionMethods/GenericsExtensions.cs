using UnityEngine;

namespace Horcrux.Runtime.Utilities.ExtensionMethods
{
    public static class GenericsExtensions
    {
        public static void SetActive<T>(this T obj, bool active) where T : Component 
            => obj.gameObject.SetActive(active);
    }
}
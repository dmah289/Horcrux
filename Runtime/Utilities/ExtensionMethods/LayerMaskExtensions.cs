using UnityEngine;

namespace manhnd_sdk.Scripts.ExtensionMethods
{
    public static class LayerMaskExtensions
    {
        /// <summary>
        /// Check a layer in mask by value
        /// </summary>
        public static bool Overlaps(this LayerMask mask, int layerVal)
            => (mask.value & (1 << layerVal)) != 0;
        
        /// <summary>
        /// check if 2 layer masks have common layers
        /// </summary>
        public static bool Overlaps(this LayerMask mask, LayerMask other)
            => (mask.value & other.value) != 0;
    }
}
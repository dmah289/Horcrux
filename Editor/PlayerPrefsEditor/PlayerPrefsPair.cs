using UnityEngine;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public struct PlayerPrefsPair
    {
        private static readonly string AliasInt = "int";
        private static readonly string AliasFloat = "float";
        private static readonly string AliasString = "string";

        public string Key;
        public object Value;

        public Color TypeColor =>
            Value switch
            {
                int => Color.cyan,
                float => Color.magenta,
                string => Color.green,
                _ => Color.white
            };

        public string AliasType =>
            Value switch
            {
                int => AliasInt,
                float => AliasFloat,
                string => AliasString,
                _ => string.Empty
            };
    }
}

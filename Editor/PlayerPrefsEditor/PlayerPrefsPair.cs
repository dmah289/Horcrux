using System;
using UnityEngine;

namespace Horcrux.Editor.PlayerPrefsEditor
{
    public struct PlayerPrefsPair
    {
        private static string AliasInt = "int";
        private static string AliasFloat = "float";
        private static string AliasString = "string";
        
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

        public Type ValueType => Value.GetType();
    }
}
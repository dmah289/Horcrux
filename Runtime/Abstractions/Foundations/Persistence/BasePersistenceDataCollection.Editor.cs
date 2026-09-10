using System;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Horcrux.Runtime.Abstractions.Persistence
{
    public abstract partial class BasePersistenceDataCollection
    {
#if UNITY_EDITOR
        [Button]
        public void ValidateKeys()
        {
            List<IPersistenceDataEntry> scratch = new();
            int rejected = ScanEntries(scratch);
            int enginePayloads = ValidatePayloadTypes(scratch);

            if (rejected == 0 && enginePayloads == 0)
            {
                Debug.Log($"[PersistenceDataCollection]: {scratch.Count} entries — no duplicate key, no empty key," +
                          " no unassigned field, no engine type in any payload.", this);
                return;
            }

            if (rejected > 0)
                Debug.LogError($"[PersistenceDataCollection]: {rejected}/{scratch.Count + rejected} declared entries were rejected.", this);

            if (enginePayloads > 0)
                Debug.LogError($"[PersistenceDataCollection]: {enginePayloads}/{scratch.Count} entries carry an engine type.", this);
        }

        /// <summary>
        /// Reports entries whose payload type reaches a UnityEngine type. Checks the type, not the value,
        /// so an empty collection of them is caught too.
        /// </summary>
        private int ValidatePayloadTypes(List<IPersistenceDataEntry> scanned)
        {
            int offenders = 0;
            HashSet<Type> visited = new();

            for (int i = 0; i < scanned.Count; i++)
            {
                Type payloadType = GetPayloadType(scanned[i].GetType());

                if (payloadType == null)
                    continue;

                visited.Clear();
                string path = FindEngineType(payloadType, visited);

                if (path == null)
                    continue;

                offenders++;
                Debug.LogError($"[PersistenceDataCollection]: Entry '{scanned[i].Key}' stores {path}." +
                               " Engine types break the JSON round-trip — declare your own serializable type" +
                               " (Persistence.md, \"Hợp đồng payload\").", this);
            }

            return offenders;
        }

        /// <summary>Returns the T of PersistenceDataEntry&lt;T&gt;, or null when the entry is not one.</summary>
        private static Type GetPayloadType(Type entryType)
        {
            for (Type type = entryType; type != null; type = type.BaseType)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PersistenceDataEntry<>))
                    return type.GetGenericArguments()[0];
            }

            return null;
        }

        /// <summary>Walks the type graph the way Newtonsoft does. Returns the path to the first
        /// UnityEngine type, or null when the graph is clean.</summary>
        private static string FindEngineType(Type type, HashSet<Type> visited)
        {
            // Already cleared on an earlier branch, or a cycle. Either way nothing new to find.
            if (type == null || !visited.Add(type))
                return null;

            if (IsEngineType(type))
                return PrettyName(type);

            if (type.IsPrimitive || type.IsEnum || type == typeof(string) || type == typeof(decimal))
                return null;

            if (type.IsArray)
            {
                string inElement = FindEngineType(type.GetElementType(), visited);
                return inElement == null ? null : $"{inElement}[]";
            }

            if (type.IsGenericType)
            {
                Type[] arguments = type.GetGenericArguments();

                for (int i = 0; i < arguments.Length; i++)
                {
                    string inArgument = FindEngineType(arguments[i], visited);

                    if (inArgument == null)
                        continue;

                    string[] shown = new string[arguments.Length];

                    for (int j = 0; j < arguments.Length; j++)
                        shown[j] = j == i ? inArgument : PrettyName(arguments[j]);

                    return $"{PrettyName(type)}<{string.Join(", ", shown)}>";
                }
            }

            // BCL leaf: never walk into its internals, Newtonsoft has its own converters for these.
            if (type.Namespace != null && type.Namespace.StartsWith("System", StringComparison.Ordinal))
                return null;

            FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsNotSerialized || fields[i].IsDefined(typeof(JsonIgnoreAttribute), false))
                    continue;

                string inField = FindEngineType(fields[i].FieldType, visited);

                if (inField != null)
                    return $"{PrettyName(type)}.{fields[i].Name} → {inField}";
            }

            PropertyInfo[] properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            for (int i = 0; i < properties.Length; i++)
            {
                PropertyInfo property = properties[i];

                // Newtonsoft writes every readable property, including get-only ones. Indexers it skips.
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    continue;

                if (property.IsDefined(typeof(JsonIgnoreAttribute), false))
                    continue;

                string inProperty = FindEngineType(property.PropertyType, visited);

                if (inProperty != null)
                    return $"{PrettyName(type)}.{property.Name} → {inProperty}";
            }

            return null;
        }

        private static bool IsEngineType(Type type)
            => type.Namespace != null && type.Namespace.StartsWith("UnityEngine", StringComparison.Ordinal);

        private static string PrettyName(Type type)
        {
            int tick = type.Name.IndexOf('`');
            return tick < 0 ? type.Name : type.Name.Substring(0, tick);
        }
#endif
    }
}

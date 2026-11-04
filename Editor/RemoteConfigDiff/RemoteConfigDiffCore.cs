using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Horcrux.Runtime.Abstractions.RemoteConfigSystem;
using Horcrux.Runtime.Implementations.RemoteConfigSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Horcrux.Editor.RemoteConfigDiff
{
    public enum RemoteConfigDiffStatus
    {
        Match = 1,
        Mismatch = 2,
        AssetOnly = 3,
        ConsoleOnly = 4,
        /// <summary>Runtime would reject the console value and keep its cache.</summary>
        ConsoleUnparsable = 5,
        /// <summary>Console sends no value (useInAppDefault): the app keeps the asset value.</summary>
        ConsoleInAppDefault = 6,
        /// <summary>Field is marked but holds no RemoteConfig&lt;T&gt;; runtime skips it with a LogError.</summary>
        InvalidField = 7,
    }

    public sealed class RemoteConfigDiffRow
    {
        public string FieldName;
        public string Key;
        public RemoteConfigDiffStatus Status;
        /// <summary>Compact JSON of the asset value, as CopyJson would serialize it; null for console-only rows.</summary>
        public string AssetJson;
        /// <summary>Console value re-serialized after the runtime parse; raw string when unparsable.</summary>
        public string ConsoleJson;
        public readonly List<string> Warnings = new();
    }

    /// <summary>
    /// Compares a remote config collection asset with a Firebase console template. Reads the variables and parses console
    /// values through the runtime's own private members, so the tool cannot drift from what a device fetches.
    /// </summary>
    public static class RemoteConfigDiffCore
    {
        private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

        private static readonly MethodInfo GetFieldsMethod =
            typeof(BaseRemoteConfigCollection).GetMethod("GetRemoteConfigFields", PrivateInstance);

        private sealed class ConsoleParam
        {
            public string Value;
            public bool HasConditionalValues;
        }

        /// <summary>Fills <paramref name="rows"/>: asset variables in declaration order, then console-only keys sorted. False with <paramref name="error"/> when the template is unreadable.</summary>
        public static bool TryCompare(BaseRemoteConfigCollection collection, string templateJson,
            List<RemoteConfigDiffRow> rows, out string error)
        {
            rows.Clear();

            if (!TryReadTemplate(templateJson, out Dictionary<string, ConsoleParam> console, out error))
                return false;

            var fields = (List<FieldInfo>)GetFieldsMethod.Invoke(collection, null);
            var keyOwners = new Dictionary<string, string>();

            for (int i = 0; i < fields.Count; i++)
                rows.Add(CompareField(collection, fields[i], console, keyOwners));

            var consoleOnly = new List<string>();

            foreach (string key in console.Keys)
            {
                if (!keyOwners.ContainsKey(key))
                    consoleOnly.Add(key);
            }

            consoleOnly.Sort(StringComparer.Ordinal);

            for (int i = 0; i < consoleOnly.Count; i++)
            {
                ConsoleParam param = console[consoleOnly[i]];
                var row = new RemoteConfigDiffRow
                {
                    Key = consoleOnly[i],
                    Status = RemoteConfigDiffStatus.ConsoleOnly,
                    ConsoleJson = param.Value,
                };
                AddConditionalWarning(row, param);
                rows.Add(row);
            }

            return true;
        }

        /// <summary>Markdown table, one line per row, warnings joined in the last column.</summary>
        public static string FormatReport(string collectionName, IReadOnlyList<RemoteConfigDiffRow> rows)
        {
            var sb = new StringBuilder();
            sb.Append("## Remote config diff: ").Append(collectionName).Append('\n').Append('\n');
            sb.Append("| Field | Key | Status | Asset | Console | Warnings |\n|---|---|---|---|---|---|\n");

            for (int i = 0; i < rows.Count; i++)
            {
                RemoteConfigDiffRow row = rows[i];
                sb.Append("| ").Append(Cell(row.FieldName))
                    .Append(" | ").Append(Cell(row.Key))
                    .Append(" | ").Append(row.Status)
                    .Append(" | ").Append(Cell(row.AssetJson))
                    .Append(" | ").Append(Cell(row.ConsoleJson))
                    .Append(" | ").Append(Cell(string.Join("; ", row.Warnings)))
                    .Append(" |\n");
            }

            return sb.ToString();
        }

        private static RemoteConfigDiffRow CompareField(BaseRemoteConfigCollection collection, FieldInfo field,
            Dictionary<string, ConsoleParam> console, Dictionary<string, string> keyOwners)
        {
            var row = new RemoteConfigDiffRow { FieldName = field.Name };
            object variable = field.GetValue(collection);

            if (variable is not IRemoteConfig remoteConfig || !IsRemoteConfigOfT(variable.GetType()))
            {
                row.Status = RemoteConfigDiffStatus.InvalidField;
                row.Warnings.Add("marked but holds no RemoteConfig<T>; runtime skips it");
                return row;
            }

            Type variableType = variable.GetType();
            row.Key = remoteConfig.FirebaseKey;
            row.AssetJson = JsonConvert.SerializeObject(variableType.GetProperty("Value").GetValue(variable));

            if (string.IsNullOrEmpty(row.Key))
                row.Warnings.Add("firebaseKey is empty");
            else if (keyOwners.TryGetValue(row.Key, out string owner))
                row.Warnings.Add($"firebaseKey also used by field {owner}");
            else
                keyOwners.Add(row.Key, field.Name);

            if (row.Key == null || !console.TryGetValue(row.Key, out ConsoleParam param))
            {
                row.Status = RemoteConfigDiffStatus.AssetOnly;
                return row;
            }

            AddConditionalWarning(row, param);

            if (!remoteConfig.AllowFetching)
                row.Warnings.Add("allowFetching is off: runtime ignores the console value");

            if (param.Value == null)
            {
                row.Status = RemoteConfigDiffStatus.ConsoleInAppDefault;
                return row;
            }

            // Same parse as a device: on failure the runtime logs its own error and keeps the cache.
            object[] args = { param.Value, null };
            bool parsed = (bool)variableType.GetMethod("TryParseValueFromString", PrivateInstance).Invoke(variable, args);

            if (!parsed)
            {
                row.Status = RemoteConfigDiffStatus.ConsoleUnparsable;
                row.ConsoleJson = param.Value;
                return row;
            }

            row.ConsoleJson = JsonConvert.SerializeObject(args[1]);
            row.Status = JToken.DeepEquals(JToken.Parse(row.AssetJson), JToken.Parse(row.ConsoleJson))
                ? RemoteConfigDiffStatus.Match
                : RemoteConfigDiffStatus.Mismatch;

            return row;
        }

        private static bool IsRemoteConfigOfT(Type type)
            => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(RemoteConfig<>);

        private static void AddConditionalWarning(RemoteConfigDiffRow row, ConsoleParam param)
        {
            if (param.HasConditionalValues)
                row.Warnings.Add("console has conditionalValues: users in a condition get another value");
        }

        /// <summary>Reads "parameters" and every "parameterGroups.*.parameters" of a template exported from the Firebase console.</summary>
        private static bool TryReadTemplate(string templateJson, out Dictionary<string, ConsoleParam> console, out string error)
        {
            console = new Dictionary<string, ConsoleParam>();
            error = null;
            JObject root;

            try
            {
                root = JObject.Parse(templateJson ?? string.Empty);
            }
            catch (JsonReaderException e)
            {
                error = "template is not valid JSON: " + e.Message;
                return false;
            }

            bool hasAny = false;

            if (root["parameters"] is JObject parameters)
            {
                hasAny = true;
                ReadParameters(parameters, console);
            }

            if (root["parameterGroups"] is JObject groups)
            {
                hasAny = true;

                foreach (JProperty group in groups.Properties())
                {
                    if (group.Value["parameters"] is JObject groupParameters)
                        ReadParameters(groupParameters, console);
                }
            }

            if (!hasAny)
                error = "template has neither \"parameters\" nor \"parameterGroups\"; export it from Remote Config > Publish changes / Download";

            return hasAny;
        }

        private static void ReadParameters(JObject parameters, Dictionary<string, ConsoleParam> console)
        {
            foreach (JProperty property in parameters.Properties())
            {
                // Missing value means useInAppDefault: the device keeps what the app has.
                console[property.Name] = new ConsoleParam
                {
                    Value = property.Value["defaultValue"]?["value"]?.Value<string>(),
                    HasConditionalValues = property.Value["conditionalValues"] is JObject conditional && conditional.HasValues,
                };
            }
        }

        private static string Cell(string text)
            => string.IsNullOrEmpty(text) ? string.Empty : text.Replace("|", "\\|").Replace("\n", " ");
    }
}

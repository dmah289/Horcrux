using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Horcrux.Editor.WireContract
{
    /// <summary>
    /// Checks the JSON wire format of a data class the way the remote config and the CopyJson/ImportJson buttons read it:
    /// <see cref="JsonConvert"/> with default settings. Pure functions: they return findings, the caller asserts or logs.
    /// </summary>
    public static class JsonWireContract
    {
        /// <summary>Adds the key paths of <paramref name="token"/>: "a.b" for nested objects, array elements merged under "[]".</summary>
        public static void CollectKeyPaths(JToken token, ISet<string> paths) => CollectKeyPaths(token, string.Empty, paths);

        /// <summary>Key paths <paramref name="value"/> writes, sorted: the columns a console value of this type must carry.</summary>
        public static List<string> KeyPathsOf(object value)
        {
            var paths = new SortedSet<string>(StringComparer.Ordinal);
            CollectKeyPaths(JToken.Parse(JsonConvert.SerializeObject(value)), paths);
            return new List<string>(paths);
        }

        /// <summary>
        /// One line per key path of <typeparamref name="T"/> that is not required: the type writes it but
        /// <paramref name="sampleJson"/> lacks it (untested), or the sample still parses with that key removed.
        /// Empty means a console value missing any key is rejected instead of reading a default.
        /// </summary>
        public static List<string> RequiredKeyViolations<T>(string sampleJson)
        {
            JToken sample = JToken.Parse(sampleJson);
            // A sample that does not parse is a mistake in the caller's test: let it throw.
            T parsed = JsonConvert.DeserializeObject<T>(sampleJson);

            var samplePaths = new SortedSet<string>(StringComparer.Ordinal);
            var writtenPaths = new SortedSet<string>(StringComparer.Ordinal);
            CollectKeyPaths(sample, samplePaths);
            CollectKeyPaths(JToken.Parse(JsonConvert.SerializeObject(parsed)), writtenPaths);

            var violations = new List<string>();

            foreach (string path in writtenPaths)
            {
                if (!samplePaths.Contains(path))
                    violations.Add(path + ": the type writes it, the sample lacks it");
            }

            foreach (string path in samplePaths)
            {
                JToken pruned = sample.DeepClone();
                RemoveKeyPath(pruned, string.Empty, path);

                try
                {
                    JsonConvert.DeserializeObject<T>(pruned.ToString(Formatting.None));
                    violations.Add(path + ": parses without it");
                }
                catch (JsonSerializationException)
                {
                    // Rejected: the key is required.
                }
            }

            return violations;
        }

        /// <summary>Null when serialize → parse → serialize gives the same JSON; otherwise both JSON strings.</summary>
        public static string RoundTripDiff<T>(T value)
        {
            string first = JsonConvert.SerializeObject(value);
            string second = JsonConvert.SerializeObject(JsonConvert.DeserializeObject<T>(first));
            return first == second ? null : first + " -> " + second;
        }

        private static void CollectKeyPaths(JToken token, string path, ISet<string> paths)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in obj.Properties())
                {
                    string child = ChildPath(path, property.Name);
                    paths.Add(child);
                    CollectKeyPaths(property.Value, child, paths);
                }
            }
            else if (token is JArray array)
            {
                foreach (JToken item in array)
                    CollectKeyPaths(item, path + "[]", paths);
            }
        }

        /// <summary>Removes the key at <paramref name="target"/> from every array element it appears in.</summary>
        private static void RemoveKeyPath(JToken token, string path, string target)
        {
            if (token is JObject obj)
            {
                foreach (JProperty property in new List<JProperty>(obj.Properties()))
                {
                    string child = ChildPath(path, property.Name);

                    if (child == target)
                        property.Remove();
                    else if (target.StartsWith(child, StringComparison.Ordinal))
                        RemoveKeyPath(property.Value, child, target);
                }
            }
            else if (token is JArray array)
            {
                foreach (JToken item in array)
                    RemoveKeyPath(item, path + "[]", target);
            }
        }

        private static string ChildPath(string path, string key) => path.Length == 0 ? key : path + "." + key;
    }
}

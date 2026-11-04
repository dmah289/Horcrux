using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Horcrux.Editor.RemoteConfigDiff;
using Horcrux.Runtime.Implementations.RemoteConfigSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Horcrux.Tests
{
    [Serializable]
    public sealed class DiffTestRow
    {
        public int a;
        public string b;
    }

    /// <summary>A project's collection as it would declare it: private marked fields on the concrete class.</summary>
    public sealed class DiffTestCollection : BaseRemoteConfigCollection
    {
        [MarkedRemoteConfig, SerializeField] private RemoteConfig<int> count;
        [MarkedRemoteConfig, SerializeField] private RemoteConfig<string> label;
        [MarkedRemoteConfig, SerializeField] private RemoteConfig<float> rate;
        [MarkedRemoteConfig, SerializeField] private RemoteConfig<List<DiffTestRow>> rows;
        [MarkedRemoteConfig, SerializeField] private RemoteConfig<bool> offline;
        [MarkedRemoteConfig, SerializeField] private string notRemote;
    }

    public class RemoteConfigDiffTests
    {
        private const string AssetJson =
            "{\"count\":{\"firebaseKey\":\"k_count\",\"allowFetching\":true,\"value\":3}," +
            "\"label\":{\"firebaseKey\":\"k_label\",\"allowFetching\":true,\"value\":\"123\"}," +
            "\"rate\":{\"firebaseKey\":\"k_rate\",\"allowFetching\":true,\"value\":1.5}," +
            "\"rows\":{\"firebaseKey\":\"k_rows\",\"allowFetching\":true,\"value\":[{\"a\":1,\"b\":\"x\"}]}," +
            "\"offline\":{\"firebaseKey\":\"k_offline\",\"allowFetching\":false,\"value\":true}}";

        private DiffTestCollection _collection;
        private readonly List<RemoteConfigDiffRow> _rows = new();

        [SetUp]
        public void SetUp()
        {
            _collection = ScriptableObject.CreateInstance<DiffTestCollection>();
            JsonUtility.FromJsonOverwrite(AssetJson, _collection);
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_collection);

        private static string Param(string key, string value, bool conditional = false)
        {
            string conditionalPart = conditional ? ",\"conditionalValues\":{\"ios\":{\"value\":\"9\"}}" : string.Empty;
            string valuePart = value == null ? "{\"useInAppDefault\":true}" : "{\"value\":\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"}";
            return $"\"{key}\":{{\"defaultValue\":{valuePart}{conditionalPart}}}";
        }

        private static string Template(params string[] parameters) => "{\"parameters\":{" + string.Join(",", parameters) + "}}";

        private RemoteConfigDiffRow Compare(string template, string key)
        {
            Assert.IsTrue(RemoteConfigDiffCore.TryCompare(_collection, template, _rows, out string error), error);
            return _rows.Single(r => r.Key == key);
        }

        [Test]
        public void SameScalar_Match()
            => Assert.AreEqual(RemoteConfigDiffStatus.Match, Compare(Template(Param("k_count", "3")), "k_count").Status);

        [Test]
        public void DifferentScalar_Mismatch_ShowsBothValues()
        {
            var row = Compare(Template(Param("k_count", "4")), "k_count");

            Assert.AreEqual(RemoteConfigDiffStatus.Mismatch, row.Status);
            Assert.AreEqual("3", row.AssetJson);
            Assert.AreEqual("4", row.ConsoleJson);
        }

        // Runtime keeps a string field's console text as is: "123" stays a string, not a number.
        [Test]
        public void StringFieldWithDigits_Match()
            => Assert.AreEqual(RemoteConfigDiffStatus.Match, Compare(Template(Param("k_label", "123")), "k_label").Status);

        [Test]
        public void FloatWrittenDifferently_Match()
            => Assert.AreEqual(RemoteConfigDiffStatus.Match, Compare(Template(Param("k_rate", "1.50")), "k_rate").Status);

        // Compared after parse: key order and whitespace on the console do not count as a difference.
        [Test]
        public void JsonWithOtherKeyOrderAndSpaces_Match()
            => Assert.AreEqual(RemoteConfigDiffStatus.Match,
                Compare(Template(Param("k_rows", "[ { \"b\": \"x\", \"a\": 1 } ]")), "k_rows").Status);

        [Test]
        public void JsonWithOtherValue_Mismatch()
            => Assert.AreEqual(RemoteConfigDiffStatus.Mismatch,
                Compare(Template(Param("k_rows", "[{\"a\":2,\"b\":\"x\"}]")), "k_rows").Status);

        [Test]
        public void UnparsableConsoleValue_ConsoleUnparsable_KeepsRawText()
        {
            LogAssert.Expect(LogType.Error, new Regex("Can't parse remote config value for key k_count"));

            var row = Compare(Template(Param("k_count", "abc")), "k_count");

            Assert.AreEqual(RemoteConfigDiffStatus.ConsoleUnparsable, row.Status);
            Assert.AreEqual("abc", row.ConsoleJson);
        }

        [Test]
        public void KeyMissingOnConsole_AssetOnly()
            => Assert.AreEqual(RemoteConfigDiffStatus.AssetOnly, Compare(Template(Param("k_count", "3")), "k_label").Status);

        [Test]
        public void KeyMissingInAsset_ConsoleOnly_SortedAfterAssetRows()
        {
            Compare(Template(Param("z_extra", "1"), Param("a_extra", "2")), "a_extra");

            CollectionAssert.AreEqual(new[] { "a_extra", "z_extra" },
                _rows.Where(r => r.Status == RemoteConfigDiffStatus.ConsoleOnly).Select(r => r.Key));
            Assert.AreEqual(RemoteConfigDiffStatus.ConsoleOnly, _rows[^1].Status);
            Assert.IsNull(_rows[^1].FieldName);
        }

        [Test]
        public void UseInAppDefault_ConsoleInAppDefault()
            => Assert.AreEqual(RemoteConfigDiffStatus.ConsoleInAppDefault, Compare(Template(Param("k_count", null)), "k_count").Status);

        [Test]
        public void ConditionalValues_Warns()
        {
            var row = Compare(Template(Param("k_count", "3", conditional: true)), "k_count");

            Assert.AreEqual(RemoteConfigDiffStatus.Match, row.Status);
            Assert.That(row.Warnings, Has.Some.Contains("conditionalValues"));
        }

        [Test]
        public void FetchingOffButOnConsole_Warns()
            => Assert.That(Compare(Template(Param("k_offline", "true")), "k_offline").Warnings, Has.Some.Contains("allowFetching"));

        [Test]
        public void FetchingOffAndNotOnConsole_NoWarning()
            => Assert.IsEmpty(Compare(Template(Param("k_count", "3")), "k_offline").Warnings);

        [Test]
        public void ParameterGroups_AreRead()
        {
            string template = "{\"parameterGroups\":{\"economy\":{\"parameters\":{" + Param("k_count", "3") + "}}}}";

            Assert.AreEqual(RemoteConfigDiffStatus.Match, Compare(template, "k_count").Status);
        }

        [Test]
        public void MarkedFieldWithoutRemoteConfig_InvalidField()
        {
            Compare(Template(Param("k_count", "3")), "k_count");

            Assert.AreEqual(RemoteConfigDiffStatus.InvalidField, _rows.Single(r => r.FieldName == "notRemote").Status);
        }

        [Test]
        public void DuplicateFirebaseKey_Warns()
        {
            JsonUtility.FromJsonOverwrite("{\"label\":{\"firebaseKey\":\"k_count\",\"allowFetching\":true,\"value\":\"a\"}}", _collection);

            Assert.IsTrue(RemoteConfigDiffCore.TryCompare(_collection, Template(Param("k_count", "3")), _rows, out _));

            Assert.That(_rows.Single(r => r.FieldName == "label").Warnings, Has.Some.Contains("count"));
        }

        [Test]
        public void InvalidTemplate_ReturnsError()
        {
            Assert.IsFalse(RemoteConfigDiffCore.TryCompare(_collection, "{not json", _rows, out string error));
            StringAssert.Contains("not valid JSON", error);
        }

        [Test]
        public void TemplateWithoutParameters_ReturnsError()
        {
            Assert.IsFalse(RemoteConfigDiffCore.TryCompare(_collection, "{\"conditions\":[]}", _rows, out string error));
            StringAssert.Contains("parameters", error);
        }

        [Test]
        public void Report_OneLinePerRow_EscapesPipe()
        {
            Compare(Template(Param("k_label", "a|b")), "k_label");

            string report = RemoteConfigDiffCore.FormatReport("Test", _rows);

            Assert.AreEqual(_rows.Count + 5, report.Split('\n').Length, report);
            StringAssert.Contains("a\\|b", report);
        }
    }
}

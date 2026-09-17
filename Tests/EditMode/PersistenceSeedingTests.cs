using System.Collections.Generic;
using System.Text.RegularExpressions;
using Horcrux.Runtime.Abstractions.Persistence;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Horcrux.Tests
{
    /// <summary>Covers the three outcomes of loading one entry, and every path flush takes comparing payloads.</summary>
    public sealed class PersistenceSeedingTests
    {
        private const string ProbeKey = "seeding_probe";
        private const string SecondKey = "seeding_second";
        private const string TextKey = "seeding_text";
        private const string ModelKey = "seeding_model";
        private const string RouteKey = "seeding_route";

        // lastSeenUtcSeconds is inherited from the base; an unkeyed entry makes ScanEntries log an error.
        private const string ClockKey = "seeding_clock";

        private const int ProbeDefault = 7;
        private const int SecondDefault = 3;
        private const string TextDefault = "fallback";

        private static readonly string[] AuthoredKeys =
            { ProbeKey, SecondKey, TextKey, ModelKey, RouteKey, ClockKey };

        private ProbeCollection collection;

        #region Setup
        [SetUp]
        public void SetUp()
        {
            ClearStorage();
            collection = ScriptableObject.CreateInstance<ProbeCollection>();

            // Authoring goes through Unity serialization, the same door the Inspector uses.
            SerializedObject serialized = new SerializedObject(collection);

            AuthorKey(serialized, "probe", ProbeKey);
            AuthorKey(serialized, "second", SecondKey);
            AuthorKey(serialized, "text", TextKey);
            AuthorKey(serialized, "model", ModelKey);
            AuthorKey(serialized, "route", RouteKey);
            AuthorKey(serialized, "lastSeenUtcSeconds", ClockKey);

            // An entry left unauthored seeds the type default, which reads the same as a value someone meant.
            serialized.FindProperty("probe").FindPropertyRelative("defaultValue").intValue = ProbeDefault;
            serialized.FindProperty("second").FindPropertyRelative("defaultValue").intValue = SecondDefault;
            serialized.FindProperty("text").FindPropertyRelative("defaultValue").stringValue = TextDefault;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            ClearStorage();
            PlayerPrefs.Save();
            Object.DestroyImmediate(collection);
        }
        #endregion

        #region Load outcomes
        [Test]
        public void Initialize_NoStoredKey_WritesTheDefault()
        {
            Assert.IsFalse(PlayerPrefs.HasKey(StorageKeyOf(ProbeKey)), "Arrange failed: the key must start absent.");

            collection.Initialize();

            Assert.IsTrue(PlayerPrefs.HasKey(StorageKeyOf(ProbeKey)), "First use must put the key in storage.");
            Assert.AreEqual("7", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)));
            Assert.AreEqual(ProbeDefault, collection.Probe.Value);
            Assert.AreEqual("7", collection.Probe.StoredPayload, "StoredPayload must match once the write lands.");
        }

        [Test]
        public void Initialize_StoredKeyEmpty_WritesTheDefault()
        {
            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), string.Empty);
            PlayerPrefs.Save();

            collection.Initialize();

            Assert.AreEqual("7", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)));
            Assert.AreEqual(ProbeDefault, collection.Probe.Value);
            Assert.AreEqual("7", collection.Probe.StoredPayload);
        }

        [Test]
        public void Initialize_StoredKeyReadable_LeavesStorageAlone()
        {
            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), "42");
            PlayerPrefs.Save();

            collection.Initialize();

            Assert.AreEqual(42, collection.Probe.Value, "A stored value must win over the default.");
            Assert.AreEqual("42", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)), "Seeding must never overwrite a real save.");
            Assert.AreEqual("42", collection.Probe.StoredPayload);
        }

        [Test]
        public void Initialize_StoredPayloadBroken_KeepsDefaultAndLeavesStorageAlone()
        {
            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), "{not json");
            PlayerPrefs.Save();

            ExpectOneReadFailure(ProbeKey);

            collection.Initialize();

            Assert.AreEqual(ProbeDefault, collection.Probe.Value, "A broken payload falls back to the default.");
            Assert.AreEqual("{not json", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)),
                "A broken payload is the only copy of it — overwriting it destroys the evidence.");
            Assert.AreEqual("7", collection.Probe.StoredPayload, "The default counts as stored, so flush stays away.");

            collection.FlushAll();

            Assert.AreEqual("{not json", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)),
                "Flush must keep its hands off a broken payload nobody changed.");
        }

        [Test]
        public void Flush_AfterBrokenPayloadThenPlayerChange_WritesTheNewValue()
        {
            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), "{not json");
            PlayerPrefs.Save();

            ExpectOneReadFailure(ProbeKey);

            collection.Initialize();
            collection.Probe.Value = 9;
            collection.FlushAll();

            Assert.AreEqual("9", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)),
                "A real change outranks keeping the broken payload.");
        }

        [Test]
        public void Initialize_StoredPayloadIsNullLiteral_StoresTheDefault()
        {
            PlayerPrefs.SetString(StorageKeyOf(TextKey), "null");
            PlayerPrefs.Save();

            collection.Initialize();

            Assert.AreEqual(TextDefault, collection.Text.Value, "A null payload falls back to the default.");

            collection.FlushAll();

            Assert.AreEqual("\"fallback\"", PlayerPrefs.GetString(StorageKeyOf(TextKey)),
                "Storage and memory must agree once the default takes over.");
            Assert.AreEqual("\"fallback\"", collection.Text.StoredPayload);
        }
        #endregion

        #region Flush compares payloads
        [Test]
        public void Flush_AfterInPlaceEdit_WritesTheNewPayload()
        {
            PlayerPrefs.SetString(StorageKeyOf(ModelKey), "{\"items\":[]}");
            PlayerPrefs.Save();

            collection.Initialize();

            // The edit nothing announces: no setter, no event, no flag.
            collection.Model.Value.items.Add(1);
            collection.FlushAll();

            Assert.AreEqual("{\"items\":[1]}", PlayerPrefs.GetString(StorageKeyOf(ModelKey)));
            Assert.AreEqual("{\"items\":[1]}", collection.Model.StoredPayload);
        }

        [Test]
        public void Flush_NothingChanged_LeavesStorageAlone()
        {
            collection.Initialize();

            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), "sentinel");
            PlayerPrefs.Save();

            collection.FlushAll();

            Assert.AreEqual("sentinel", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)),
                "An unchanged entry must not reach SetString at all.");
        }

        [Test]
        public void Flush_ValueReassignedToTheSameValue_LeavesStorageAlone()
        {
            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), "42");
            PlayerPrefs.Save();

            collection.Initialize();

            PlayerPrefs.SetString(StorageKeyOf(ProbeKey), "sentinel");
            PlayerPrefs.Save();

            collection.Probe.Value = 42;
            collection.FlushAll();

            Assert.AreEqual("sentinel", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)),
                "Assigning the value it already had is not a change.");
        }

        [Test]
        public void FlushNow_WritesOnlyThatEntry()
        {
            collection.Initialize();

            collection.Probe.Value = 1;
            collection.Second.Value = 2;

            collection.Probe.FlushNow();

            Assert.AreEqual("1", PlayerPrefs.GetString(StorageKeyOf(ProbeKey)));
            Assert.AreEqual("3", PlayerPrefs.GetString(StorageKeyOf(SecondKey)), "Only the named entry is written.");
            Assert.AreEqual("3", collection.Second.StoredPayload, "The entry left behind still owes its change.");
        }

        [Test]
        public void Flush_WritePayloadThrows_KeepsStoredPayloadAndRetries()
        {
            collection.Initialize();

            string storedBeforeFailure = collection.Route.StoredPayload;

            // Vector3 exposes a self-referencing 'normalized' property, so Newtonsoft refuses to write it.
            collection.Route.Value = new List<Vector3> { Vector3.one };

            ExpectOneWriteFailure(RouteKey);
            collection.FlushAll();

            Assert.AreEqual(storedBeforeFailure, collection.Route.StoredPayload,
                "A write that never landed must not be recorded as stored.");

            ExpectOneWriteFailure(RouteKey);
            collection.FlushAll();

            Assert.AreEqual(storedBeforeFailure, collection.Route.StoredPayload,
                "The entry keeps failing out loud instead of going quiet.");
        }
        #endregion

        #region Class Methods
        private static string StorageKeyOf(string entryKey)
            => "persistence_" + entryKey;

        private static void AuthorKey(SerializedObject serialized, string fieldName, string entryKey)
            => serialized.FindProperty(fieldName).FindPropertyRelative("key").stringValue = entryKey;

        private static void ExpectOneReadFailure(string entryKey)
        {
            LogAssert.Expect(LogType.Error, new Regex($"Reading entry '{entryKey}' failed"));
            LogAssert.Expect(LogType.Exception, new Regex("."));
        }

        private static void ExpectOneWriteFailure(string entryKey)
        {
            LogAssert.Expect(LogType.Error, new Regex($"Writing entry '{entryKey}' failed"));
            LogAssert.Expect(LogType.Exception, new Regex("."));
        }

        private static void ClearStorage()
        {
            for (int i = 0; i < AuthoredKeys.Length; i++)
                PlayerPrefs.DeleteKey(StorageKeyOf(AuthoredKeys[i]));
        }
        #endregion

        #region Fixture
        /// <summary>One entry per payload shape a test needs; each test watches only its own key.</summary>
        private sealed class ProbeCollection : BasePersistenceDataCollection
        {
            [MarkedPersistence, SerializeField] private PersistenceDataEntry<int> probe = new();
            [MarkedPersistence, SerializeField] private PersistenceDataEntry<int> second = new();
            [MarkedPersistence, SerializeField] private PersistenceDataEntry<string> text = new();
            [MarkedPersistence, SerializeField] private PersistenceDataEntry<ProbeModel> model = new();
            [MarkedPersistence, SerializeField] private PersistenceDataEntry<List<Vector3>> route = new();

            public PersistenceDataEntry<int> Probe => probe;
            public PersistenceDataEntry<int> Second => second;
            public PersistenceDataEntry<string> Text => text;
            public PersistenceDataEntry<ProbeModel> Model => model;
            public PersistenceDataEntry<List<Vector3>> Route => route;
        }

        /// <summary>A model a test can edit in place, the way game code edits a save model.</summary>
        [System.Serializable]
        public sealed class ProbeModel
        {
            public List<int> items = new();
        }
        #endregion
    }
}

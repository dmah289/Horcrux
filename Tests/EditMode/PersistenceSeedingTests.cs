using System.Text.RegularExpressions;
using Horcrux.Runtime.Abstractions.Persistence;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Horcrux.Tests
{
    /// <summary>Covers the three outcomes of loading one entry: seeded, loaded, and failed.</summary>
    public sealed class PersistenceSeedingTests
    {
        private const string EntryKey = "seeding_probe";
        private const string StorageKey = "persistence_" + EntryKey;
        private const int DefaultValue = 7;

        private ProbeCollection collection;

        #region Setup
        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            collection = ScriptableObject.CreateInstance<ProbeCollection>();

            // Authoring goes through Unity serialization, the same door the Inspector uses.
            SerializedObject serialized = new SerializedObject(collection);
            SerializedProperty entry = serialized.FindProperty("probe");
            entry.FindPropertyRelative("key").stringValue = EntryKey;
            entry.FindPropertyRelative("defaultValue").intValue = DefaultValue;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(StorageKey);
            PlayerPrefs.Save();
            Object.DestroyImmediate(collection);
        }
        #endregion

        #region Tests
        [Test]
        public void Initialize_NoStoredKey_WritesTheDefault()
        {
            Assert.IsFalse(PlayerPrefs.HasKey(StorageKey), "Arrange failed: the key must start absent.");

            collection.Initialize();

            Assert.IsTrue(PlayerPrefs.HasKey(StorageKey), "First use must put the key in storage.");
            Assert.AreEqual("7", PlayerPrefs.GetString(StorageKey));
            Assert.AreEqual(DefaultValue, collection.Probe.Value);
            Assert.IsFalse(collection.Probe.IsDirty, "Dirty must clear once the write lands.");
        }

        [Test]
        public void Initialize_StoredKeyEmpty_WritesTheDefault()
        {
            PlayerPrefs.SetString(StorageKey, string.Empty);
            PlayerPrefs.Save();

            collection.Initialize();

            Assert.AreEqual("7", PlayerPrefs.GetString(StorageKey));
            Assert.AreEqual(DefaultValue, collection.Probe.Value);
        }

        [Test]
        public void Initialize_StoredKeyReadable_LeavesStorageAlone()
        {
            PlayerPrefs.SetString(StorageKey, "42");
            PlayerPrefs.Save();

            collection.Initialize();

            Assert.AreEqual(42, collection.Probe.Value, "A stored value must win over the default.");
            Assert.AreEqual("42", PlayerPrefs.GetString(StorageKey), "Seeding must never overwrite a real save.");
            Assert.IsFalse(collection.Probe.IsDirty);
        }

        [Test]
        public void Initialize_StoredPayloadBroken_KeepsDefaultAndLeavesStorageAlone()
        {
            PlayerPrefs.SetString(StorageKey, "{not json");
            PlayerPrefs.Save();

            LogAssert.Expect(LogType.Error, new Regex($"Reading entry '{EntryKey}' failed"));
            LogAssert.Expect(LogType.Exception, new Regex("."));

            collection.Initialize();

            Assert.AreEqual(DefaultValue, collection.Probe.Value, "A broken payload falls back to the default.");
            Assert.AreEqual("{not json", PlayerPrefs.GetString(StorageKey),
                "A broken payload is the only copy of it — overwriting it destroys the evidence.");
        }
        #endregion

        #region Fixture
        /// <summary>One entry, so a test can watch exactly one key.</summary>
        private sealed class ProbeCollection : BasePersistenceDataCollection
        {
            [MarkedPersistence, SerializeField] private PersistenceDataEntry<int> probe = new();

            public PersistenceDataEntry<int> Probe => probe;
        }
        #endregion
    }
}

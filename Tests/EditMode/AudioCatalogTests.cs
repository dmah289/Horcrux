using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Horcrux.Runtime.Implementations.Audio;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Horcrux.Tests
{
    public class AudioCatalogTests
    {
        private readonly List<Object> _created = new();
        private TestAudioCatalog _catalog;
        private AudioClip _clip;

        [SetUp]
        public void SetUp()
        {
            _catalog = Track(ScriptableObject.CreateInstance<TestAudioCatalog>());
            _clip = Track(AudioClip.Create("test", 1, 1, 44100, false));
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                Object.DestroyImmediate(_created[i]);

            _created.Clear();
        }

        [Test]
        public void ValidCatalog_ReportsNothing()
        {
            AddEntry(1, _clip);
            AddEntry(2, _clip);
            AddEntry(3, _clip);
            AddTrack(1, _clip);

            Assert.AreEqual(0, LogInvalidEntries());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void EmptyCatalog_ReportsNothing()
        {
            Assert.AreEqual(0, LogInvalidEntries());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void SfxIdAndMusicIdWithSameNumber_AreIndependent()
        {
            AddEntry(1, _clip);
            AddTrack(1, _clip);

            Assert.AreEqual(0, LogInvalidEntries());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Entry_WithoutId_IsReported()
        {
            AddEntry(0, _clip);

            ExpectError("Sfx entry #0 (0) has no Id");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void Track_WithoutId_IsReported()
        {
            AddTrack(0, _clip);

            ExpectError("Music track #0 (0) has no Id");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void Entry_WithIdOutsideEnum_IsReported()
        {
            AddEntry(99, _clip);

            ExpectError("Sfx entry #0 (99) is no member of TestSfx");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void Track_WithIdOutsideByteEnum_IsReported()
        {
            AddTrack(99, _clip);

            ExpectError("Music track #0 (99) is no member of TestMusic");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void DuplicateEntryId_ReportsTheLaterOne()
        {
            AddEntry(3, _clip);
            AddEntry(3, _clip);

            ExpectError("Sfx entry #1 (C) repeats Id C of Sfx entry #0");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void DuplicateTrackId_ReportsTheLaterOne()
        {
            AddTrack(2, _clip);
            AddTrack(2, _clip);

            ExpectError("Music track #1 (N) repeats Id N of Music track #0");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void Entry_WithoutClip_IsReported()
        {
            AddEntry(1, null);

            ExpectError("Sfx entry #0 (A) has no clip");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void Track_WithoutClip_IsReported()
        {
            AddTrack(1, null);

            ExpectError("Music track #0 (M) has no clip");
            Assert.AreEqual(1, LogInvalidEntries());
        }

        [Test]
        public void NewEntryAndTrack_StartAudible()
        {
            var entry = new AudioEntry<TestSfx>();
            var track = new MusicTrack<TestMusic>();

            Assert.AreEqual(1f, entry.Volume);
            Assert.AreEqual(0.03f, entry.MinIntervalSeconds);
            Assert.AreEqual(1f, track.Volume);
        }

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private int LogInvalidEntries()
        {
            MethodInfo method = typeof(AudioCatalog<TestSfx, TestMusic>).GetMethod("LogInvalidEntries", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method, "LogInvalidEntries is missing or no longer editor-only private.");
            return (int)method.Invoke(_catalog, null);
        }

        private static void ExpectError(string text) =>
            LogAssert.Expect(LogType.Error, new Regex(Regex.Escape($"[AudioCatalog]: {text}")));

        private void AddEntry(int id, AudioClip clip) => AddElement("entries", id, clip);

        private void AddTrack(int id, AudioClip clip) => AddElement("tracks", id, clip);

        /// <summary>Appends one element through <see cref="SerializedObject"/>, the way the Inspector would.</summary>
        private void AddElement(string arrayName, int id, AudioClip clip)
        {
            var serialized = new SerializedObject(_catalog);
            SerializedProperty array = serialized.FindProperty(arrayName);
            array.arraySize++;

            SerializedProperty element = array.GetArrayElementAtIndex(array.arraySize - 1);
            element.FindPropertyRelative("id").intValue = id;
            element.FindPropertyRelative("clip").objectReferenceValue = clip;

            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

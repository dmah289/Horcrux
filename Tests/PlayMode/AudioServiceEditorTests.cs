#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Horcrux.Runtime.Implementations.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// The Editor-only buttons of <see cref="AudioService{TSfx,TMusic}"/>: <c>SetupAudioSources</c>, <c>ConfigureAllSources</c>,
    /// <c>ValidateReferences</c>. They are private and <c>#if UNITY_EDITOR</c>, so the tests call them through reflection.
    /// </summary>
    /// <remarks>
    /// Lives in the PlayMode assembly on purpose: a <see cref="MonoBehaviour"/> from the Editor-only EditMode assembly cannot be
    /// added to a GameObject. The host is built inactive so <c>Awake</c> never runs against an unfinished setup.
    /// </remarks>
    public class AudioServiceEditorTests
    {
        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo VoicesField = ServiceField("voices");
        private static readonly FieldInfo MusicSourceField = ServiceField("musicSource");
        private static readonly FieldInfo CatalogField = ServiceField("catalog");

        /// <summary>The count <c>SetupAudioSources</c> builds; read from the service so a retuned default does not break the tests.</summary>
        private static readonly int VoiceAmount = (int)typeof(AudioService<TestSfx, TestMusic>)
            .GetField("DefaultVoiceAmount", BindingFlags.Static | BindingFlags.NonPublic).GetRawConstantValue();

        private readonly List<Object> _created = new();
        private GameObject _root;
        private TestAudioService _service;

        [SetUp]
        public void SetUp()
        {
            _root = Own(new GameObject("audio_test"));
            _root.SetActive(false);
            _service = _root.AddComponent<TestAudioService>();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
                Object.DestroyImmediate(_created[i]);

            _created.Clear();
        }

        [Test]
        public void SetupAudioSources_BuildsVoicesAndMusicSource_EachOnItsOwnChild()
        {
            Invoke("SetupAudioSources");

            AudioSource[] voices = (AudioSource[])VoicesField.GetValue(_service);
            AudioSource music = (AudioSource)MusicSourceField.GetValue(_service);

            Assert.AreEqual(VoiceAmount, voices.Length);
            Assert.NotNull(music);
            Assert.AreEqual(VoiceAmount + 1, _root.transform.childCount);

            for (int i = 0; i < voices.Length; i++)
            {
                Assert.NotNull(voices[i]);
                Assert.AreNotSame(_root, voices[i].gameObject);
                Assert.AreEqual(1, voices[i].gameObject.GetComponents<AudioSource>().Length);
            }

            Assert.AreNotSame(_root, music.gameObject);
            Assert.AreEqual(1, music.gameObject.GetComponents<AudioSource>().Length);
        }

        [Test]
        public void SetupAudioSources_NamesFollowLowercaseUnderscoreDigits()
        {
            Invoke("SetupAudioSources");

            var allowed = new Regex("^[a-z0-9_]+$");

            for (int i = 0; i < _root.transform.childCount; i++)
                Assert.IsTrue(allowed.IsMatch(_root.transform.GetChild(i).name), $"'{_root.transform.GetChild(i).name}' breaks the GameObject naming rule.");
        }

        [Test]
        public void SetupAudioSources_Twice_CreatesNothingNew()
        {
            Invoke("SetupAudioSources");
            Invoke("SetupAudioSources");

            Assert.AreEqual(VoiceAmount + 1, _root.transform.childCount);
            Assert.AreEqual(VoiceAmount, ((AudioSource[])VoicesField.GetValue(_service)).Length);
        }

        [Test]
        public void SetupAudioSources_FillsOnlyTheEmptySlots()
        {
            Invoke("SetupAudioSources");
            AudioSource[] voices = (AudioSource[])VoicesField.GetValue(_service);
            AudioSource kept = voices[0];
            Object.DestroyImmediate(voices[2].gameObject);

            Invoke("SetupAudioSources");

            Assert.AreSame(kept, ((AudioSource[])VoicesField.GetValue(_service))[0]);
            Assert.NotNull(((AudioSource[])VoicesField.GetValue(_service))[2]);
            Assert.AreEqual(VoiceAmount + 1, _root.transform.childCount);
        }

        [Test]
        public void ConfigureAllSources_ForcesTheFlagsOnEverySource()
        {
            Invoke("SetupAudioSources");
            AudioSource[] voices = (AudioSource[])VoicesField.GetValue(_service);
            AudioSource music = (AudioSource)MusicSourceField.GetValue(_service);

            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].playOnAwake = true;
                voices[i].loop = true;
                voices[i].spatialBlend = 1f;
            }

            music.playOnAwake = true;
            music.loop = false;
            music.spatialBlend = 1f;

            Invoke("ConfigureAllSources");

            for (int i = 0; i < voices.Length; i++)
            {
                Assert.IsFalse(voices[i].playOnAwake);
                Assert.IsFalse(voices[i].loop);
                Assert.AreEqual(0f, voices[i].spatialBlend);
            }

            Assert.IsFalse(music.playOnAwake);
            Assert.IsTrue(music.loop);
            Assert.AreEqual(0f, music.spatialBlend);
        }

        [Test]
        public void ValidateReferences_WhenComplete_LogsOnceAndNoError()
        {
            Invoke("SetupAudioSources");
            CatalogField.SetValue(_service, Own(ScriptableObject.CreateInstance<TestAudioCatalog>()));

            LogAssert.Expect(LogType.Log, new Regex($@"\[AudioService\]: Catalog, Music Source and {VoiceAmount} Sfx voices assigned"));
            Invoke("ValidateReferences");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void ValidateReferences_WhenNothingAssigned_ReportsCatalogMusicSourceAndVoices()
        {
            LogAssert.Expect(LogType.Error, new Regex(@"\[AudioService\]: Catalog is not assigned"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[AudioService\]: Music Source is not assigned"));
            LogAssert.Expect(LogType.Error, new Regex(@"\[AudioService\]: No Sfx voice assigned"));

            Invoke("ValidateReferences");
        }

        [Test]
        public void ValidateReferences_WhenOneVoiceSlotIsEmpty_NamesTheSlot()
        {
            Invoke("SetupAudioSources");
            CatalogField.SetValue(_service, Own(ScriptableObject.CreateInstance<TestAudioCatalog>()));
            AudioSource[] voices = (AudioSource[])VoicesField.GetValue(_service);
            voices[3] = null;

            LogAssert.Expect(LogType.Error, new Regex(@"\[AudioService\]: Sfx Voices\[3\] is empty"));
            Invoke("ValidateReferences");

            LogAssert.NoUnexpectedReceived();
        }

        private void Invoke(string methodName)
        {
            MethodInfo method = typeof(AudioService<TestSfx, TestMusic>).GetMethod(methodName, NonPublicInstance);
            Assert.NotNull(method, $"{methodName} is missing or no longer editor-only private.");
            method.Invoke(_service, null);
        }

        private static FieldInfo ServiceField(string name) =>
            typeof(AudioService<TestSfx, TestMusic>).GetField(name, NonPublicInstance);

        private T Own<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}
#endif

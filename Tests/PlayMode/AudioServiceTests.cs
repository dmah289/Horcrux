using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Horcrux.Runtime.Abstractions.Audio;
using Horcrux.Runtime.Implementations.Audio;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Runtime behaviour of <see cref="AudioService{TSfx,TMusic}"/>. The host is built inactive, wired through reflection the way the
    /// Inspector would, then activated so <c>Awake</c> runs against a finished setup.
    /// </summary>
    /// <remarks>
    /// Tests that need a playing <see cref="AudioSource"/> are inconclusive on a machine without an audio device
    /// (<see cref="Control_AudioSourceReportsPlaying"/> tells which). Exact-boundary throttle (two plays exactly
    /// <c>minInterval</c> apart) and 0 B GC need a controlled clock or a Profiler on an IL2CPP player and are not covered here.
    /// </remarks>
    public class AudioServiceTests
    {
        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const float ClipSeconds = 0.4f;
        private const int SampleRate = 44100;

        private static readonly FieldInfo VoicesField = ServiceField("voices");
        private static readonly FieldInfo MusicSourceField = ServiceField("musicSource");
        private static readonly FieldInfo CatalogField = ServiceField("catalog");
        private static readonly FieldInfo BusyUntilField = ServiceField("_voicesBusyUntil");
        private static readonly FieldInfo EntriesField = typeof(AudioCatalog<TestSfx, TestMusic>).GetField("entries", NonPublicInstance);
        private static readonly FieldInfo TracksField = typeof(AudioCatalog<TestSfx, TestMusic>).GetField("tracks", NonPublicInstance);

        private readonly List<Object> _created = new();
        private AudioClip _clip;
        private AudioClip _otherClip;
        private AudioClip _musicClip;

        [SetUp]
        public void SetUp()
        {
            _clip = Own(MakeClip(ClipSeconds));
            _otherClip = Own(MakeClip(ClipSeconds));
            _musicClip = Own(MakeClip(3f));
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;

            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        // ---------------------------------------------------------------- SFX: lookup, flags, errors

        [Test]
        public void Control_AudioSourceReportsPlaying()
        {
            Assert.IsTrue(AudioCanPlay(), "AudioSource.isPlaying stays false after Play(): no audio device, so playing-state tests are inconclusive.");
        }

        [Test]
        public void PlaySfx_WhenSfxOff_PlaysNothing()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)));
            rig.Service.IsSfxOn = false;

            rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(0, AssignedVoiceAmount(rig));
        }

        [Test]
        public void PlaySfx_UnknownId_LogsErrorAndPlaysNothing()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)));

            LogAssert.Expect(LogType.Error, new Regex(@"\[AudioService\]: Sfx 99 is not in the catalog"));
            rig.Service.PlaySfx((TestSfx)99);

            Assert.AreEqual(0, AssignedVoiceAmount(rig));
        }

        [Test]
        public void PlaySfx_ValidId_AppliesClipAndVolume()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, volume: 0.4f)));

            rig.Service.PlaySfx(TestSfx.A);

            Assert.AreSame(_clip, rig.Voices[0].clip);
            Assert.AreEqual(0.4f, rig.Voices[0].volume, 1e-5f);
        }

        [Test]
        public void PlaySfx_FirstPlayAfterAwake_IsNotThrottled()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 5f)));

            rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(1, AssignedVoiceAmount(rig));
        }

        [Test]
        public void PlaySfx_DifferentIdsSameFrame_BothPlay()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0.05f), Entry(TestSfx.B, _otherClip, minInterval: 0.05f)));

            rig.Service.PlaySfx(TestSfx.A);
            rig.Service.PlaySfx(TestSfx.B);

            Assert.AreEqual(2, AssignedVoiceAmount(rig));
        }

        [Test]
        public void PlaySfx_SameIdSameFrame_ThrottledToOne()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0.05f)));

            for (int i = 0; i < 20; i++)
                rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(1, AssignedVoiceAmount(rig));
        }

        [Test]
        public void PlaySfx_NoThrottle_FillsEveryVoiceThenTakesTheSoonestToFinish()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0f), Entry(TestSfx.B, _otherClip, minInterval: 0f)), voiceAmount: 5);

            for (int i = 0; i < 5; i++)
                rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(5, AssignedVoiceAmount(rig));
            int soonestIndex = SoonestVoiceIndex(rig);

            rig.Service.PlaySfx(TestSfx.B);

            Assert.AreSame(_otherClip, rig.Voices[soonestIndex].clip);
            Assert.AreEqual(1, CountVoicesWithClip(rig, _otherClip));
        }

        [Test]
        public void PlaySfx_HundredCallsNoThrottle_KeepsUsingTheEntryClip()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0f)));

            for (int i = 0; i < 100; i++)
                rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(rig.Voices.Length, CountVoicesWithClip(rig, _clip));
        }

        [Test]
        public void PlaySfx_Pitch_StaysInSpreadAndVaries()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0f)));
            var seenPitches = new HashSet<float>();

            for (int i = 0; i < 1000; i++)
            {
                rig.Service.PlaySfx(TestSfx.A);

                for (int v = 0; v < rig.Voices.Length; v++)
                {
                    float pitch = rig.Voices[v].pitch;
                    Assert.GreaterOrEqual(pitch, 0.95f - 1e-5f);
                    Assert.LessOrEqual(pitch, 1.05f + 1e-5f);
                    seenPitches.Add(pitch);
                }
            }

            Assert.Greater(seenPitches.Count, 1, "Pitch never changed: random spread is not applied.");
        }

        [Test]
        public void PlaySfx_BusyUntil_IsClipLengthOverPitch()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)));

            rig.Service.PlaySfx(TestSfx.A);
            float now = Time.unscaledTime;

            float busyUntil = ((float[])BusyUntilField.GetValue(rig.Service))[0];
            Assert.AreEqual(_clip.length / rig.Voices[0].pitch, busyUntil - now, 1e-4f);
        }

        [Test]
        public void SfxOff_WhilePlaying_ClearsEveryBusyMark()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0f)));

            for (int i = 0; i < 3; i++)
                rig.Service.PlaySfx(TestSfx.A);

            rig.Service.IsSfxOn = false;

            float[] busyUntil = (float[])BusyUntilField.GetValue(rig.Service);

            for (int i = 0; i < rig.Voices.Length; i++)
            {
                Assert.AreEqual(0f, busyUntil[i]);
                Assert.IsFalse(rig.Voices[i].isPlaying);
            }
        }

        [Test]
        public void SfxOff_BeforeAwake_DoesNotThrow()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)), activate: false);

            Assert.DoesNotThrow(() => rig.Service.IsSfxOn = false);
        }

        [Test]
        public void SettingsInterface_SeesTheSameFlagsAsTheService()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)));
            IAudioSetting setting = rig.Service;

            rig.Service.IsSfxOn = false;
            setting.IsMusicOn = false;

            Assert.IsFalse(setting.IsSfxOn);
            Assert.IsFalse(rig.Service.IsMusicOn);
        }

        // ---------------------------------------------------------------- SFX: setup mistakes explode where they are touched

        [Test]
        public void MissingCatalog_ThrowsInAwake()
        {
            LogAssert.Expect(LogType.Exception, new Regex("NullReferenceException"));

            Build(Entries(), assignCatalog: false);
        }

        [Test]
        public void EmptyVoices_ThrowsAtFirstPlay()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)), voiceAmount: 0);

            Assert.Throws<System.IndexOutOfRangeException>(() => rig.Service.PlaySfx(TestSfx.A));
        }

        [Test]
        public void EmptyVoiceSlot_ThrowsOnlyOnceEarlierVoicesAreBusy()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0f)), voiceAmount: 5);
            VoicesField.SetValue(rig.Service, new[] { rig.Voices[0], rig.Voices[1], rig.Voices[2], null, rig.Voices[4] });

            for (int i = 0; i < 3; i++)
                rig.Service.PlaySfx(TestSfx.A);

            Assert.Throws<System.NullReferenceException>(() => rig.Service.PlaySfx(TestSfx.A));
        }

        [Test]
        public void EntryWithoutClip_ThrowsAtPlay()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, null)));

            Assert.Throws<System.NullReferenceException>(() => rig.Service.PlaySfx(TestSfx.A));
        }

        [Test]
        public void DuplicateEntryId_ThrowsInAwake()
        {
            LogAssert.Expect(LogType.Exception, new Regex("ArgumentException"));

            Build(Entries(Entry(TestSfx.A, _clip), Entry(TestSfx.A, _otherClip)));
        }

        // ---------------------------------------------------------------- SFX: time

        [UnityTest]
        public IEnumerator PlaySfx_AfterIntervalElapsed_PlaysAgain()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0.05f)));

            rig.Service.PlaySfx(TestSfx.A);
            yield return new WaitForSecondsRealtime(0.1f);
            rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(2, AssignedVoiceAmount(rig));
        }

        [UnityTest]
        public IEnumerator PlaySfx_WhileTimeScaleZero_StillPlaysAfterInterval()
        {
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0.05f)));
            Time.timeScale = 0f;

            rig.Service.PlaySfx(TestSfx.A);
            yield return new WaitForSecondsRealtime(0.1f);
            rig.Service.PlaySfx(TestSfx.A);

            Assert.AreEqual(2, AssignedVoiceAmount(rig));
        }

        // ---------------------------------------------------------------- Music

        [Test]
        public void PlayMusic_AppliesTrackClipAndVolume()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip, 0.6f)));

            rig.Service.PlayMusic(TestMusic.M);

            Assert.AreSame(_musicClip, rig.Music.clip);
            Assert.AreEqual(0.6f, rig.Music.volume, 1e-5f);
            Assert.IsTrue(rig.Music.isPlaying);
        }

        [Test]
        public void PlayMusic_UnknownId_LogsErrorAndKeepsCurrentTrack()
        {
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip)));
            rig.Service.PlayMusic(TestMusic.M);

            LogAssert.Expect(LogType.Error, new Regex(@"\[AudioService\]: Music 99 is not in the catalog"));
            rig.Service.PlayMusic((TestMusic)99);

            Assert.AreSame(_musicClip, rig.Music.clip);
        }

        [Test]
        public void PlayMusic_AnotherTrack_ReplacesTheClip()
        {
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip), Track(TestMusic.N, _otherClip)));

            rig.Service.PlayMusic(TestMusic.M);
            rig.Service.PlayMusic(TestMusic.N);

            Assert.AreSame(_otherClip, rig.Music.clip);
        }

        [UnityTest]
        public IEnumerator PlayMusic_SameTrackTwice_DoesNotRestart()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip)));

            rig.Service.PlayMusic(TestMusic.M);
            yield return new WaitForSecondsRealtime(0.2f);
            rig.Service.PlayMusic(TestMusic.M);

            Assert.Greater(rig.Music.time, 0.05f);
        }

        [UnityTest]
        public IEnumerator PlayMusic_OtherTrackSharingTheClip_KeepsPlayingAndKeepsFirstVolume()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip, 0.3f), Track(TestMusic.N, _musicClip, 0.9f)));

            rig.Service.PlayMusic(TestMusic.M);
            yield return new WaitForSecondsRealtime(0.2f);
            rig.Service.PlayMusic(TestMusic.N);

            Assert.Greater(rig.Music.time, 0.05f);
            Assert.AreEqual(0.3f, rig.Music.volume, 1e-5f);
        }

        [Test]
        public void MusicOff_StopsMusicNowAndLeavesSfxAlone()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip)), Tracks(Track(TestMusic.M, _musicClip)));
            rig.Service.PlayMusic(TestMusic.M);

            rig.Service.IsMusicOn = false;

            Assert.IsFalse(rig.Music.isPlaying);
            Assert.IsTrue(rig.Service.IsSfxOn);
        }

        [Test]
        public void PlayMusic_WhileMusicOff_OnlyRemembersTheTrack()
        {
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip)));
            rig.Service.IsMusicOn = false;

            rig.Service.PlayMusic(TestMusic.M);
            Assert.IsNull(rig.Music.clip);

            rig.Service.IsMusicOn = true;
            Assert.AreSame(_musicClip, rig.Music.clip);
        }

        [Test]
        public void StopMusic_ForgetsTheTrack_SoTogglingMusicStaysSilent()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip)));
            rig.Service.PlayMusic(TestMusic.M);

            rig.Service.StopMusic();
            rig.Service.IsMusicOn = false;
            rig.Service.IsMusicOn = true;

            Assert.IsFalse(rig.Music.isPlaying);
        }

        [Test]
        public void SfxOff_WhileMusicPlays_MusicKeepsPlaying()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip)));
            rig.Service.PlayMusic(TestMusic.M);

            rig.Service.IsSfxOn = false;

            Assert.IsTrue(rig.Music.isPlaying);
        }

        [Test]
        public void ExhaustedVoices_NeverTakeTheMusicSource()
        {
            Assume.That(AudioCanPlay(), "No audio device.");
            Rig rig = Build(Entries(Entry(TestSfx.A, _clip, minInterval: 0f)), Tracks(Track(TestMusic.M, _musicClip)));
            rig.Service.PlayMusic(TestMusic.M);

            for (int i = 0; i < 20; i++)
                rig.Service.PlaySfx(TestSfx.A);

            Assert.AreSame(_musicClip, rig.Music.clip);
            Assert.IsTrue(rig.Music.isPlaying);
        }

        [Test]
        public void MissingMusicSource_ThrowsAtPlayMusic()
        {
            Rig rig = Build(Entries(), Tracks(Track(TestMusic.M, _musicClip)), assignMusicSource: false);

            Assert.Throws<System.NullReferenceException>(() => rig.Service.PlayMusic(TestMusic.M));
        }

        // ---------------------------------------------------------------- Rig

        private sealed class Rig
        {
            public GameObject Root;
            public TestAudioService Service;
            public TestAudioCatalog Catalog;
            public AudioSource[] Voices;
            public AudioSource Music;
        }

        private Rig Build(
            AudioEntry<TestSfx>[] entries,
            MusicTrack<TestMusic>[] tracks = null,
            int voiceAmount = 5,
            bool activate = true,
            bool assignCatalog = true,
            bool assignMusicSource = true)
        {
            GameObject root = Own(new GameObject("audio_test"));
            root.SetActive(false);

            var rig = new Rig
            {
                Root = root,
                Catalog = Own(ScriptableObject.CreateInstance<TestAudioCatalog>()),
                Voices = new AudioSource[voiceAmount],
                Music = NewSource(root, "music_source"),
                Service = root.AddComponent<TestAudioService>(),
            };

            for (int i = 0; i < voiceAmount; i++)
                rig.Voices[i] = NewSource(root, $"sfx_voice_{i}");

            EntriesField.SetValue(rig.Catalog, entries);
            TracksField.SetValue(rig.Catalog, tracks ?? new MusicTrack<TestMusic>[0]);

            VoicesField.SetValue(rig.Service, rig.Voices);

            if (assignMusicSource)
                MusicSourceField.SetValue(rig.Service, rig.Music);

            if (assignCatalog)
                CatalogField.SetValue(rig.Service, rig.Catalog);

            if (activate)
                root.SetActive(true);

            return rig;
        }

        private static AudioSource NewSource(GameObject root, string objectName)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(root.transform);

            AudioSource source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            return source;
        }

        private static AudioEntry<TestSfx>[] Entries(params AudioEntry<TestSfx>[] entries) => entries;

        private static MusicTrack<TestMusic>[] Tracks(params MusicTrack<TestMusic>[] tracks) => tracks;

        private static AudioEntry<TestSfx> Entry(TestSfx id, AudioClip clip, float volume = 1f, float minInterval = 0.05f)
        {
            var entry = new AudioEntry<TestSfx>();
            SetField(entry, "id", id);
            SetField(entry, "clip", clip);
            SetField(entry, "volume", volume);
            SetField(entry, "minIntervalSeconds", minInterval);
            return entry;
        }

        private static MusicTrack<TestMusic> Track(TestMusic id, AudioClip clip, float volume = 1f)
        {
            var track = new MusicTrack<TestMusic>();
            SetField(track, "id", id);
            SetField(track, "clip", clip);
            SetField(track, "volume", volume);
            return track;
        }

        // ---------------------------------------------------------------- Helpers

        private static FieldInfo ServiceField(string name) =>
            typeof(AudioService<TestSfx, TestMusic>).GetField(name, NonPublicInstance);

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, NonPublicInstance);
            Assert.NotNull(field, $"{target.GetType().Name}.{name} is missing.");
            field.SetValue(target, value);
        }

        private T Own<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        private static AudioClip MakeClip(float seconds) =>
            AudioClip.Create("test", Mathf.CeilToInt(SampleRate * seconds), 1, SampleRate, false);

        private bool AudioCanPlay()
        {
            GameObject probe = Own(new GameObject("audio_probe"));
            AudioSource source = probe.AddComponent<AudioSource>();
            source.clip = _musicClip;
            source.Play();
            return source.isPlaying;
        }

        private static int AssignedVoiceAmount(Rig rig)
        {
            int amount = 0;

            for (int i = 0; i < rig.Voices.Length; i++)
            {
                if (rig.Voices[i].clip != null)
                    amount++;
            }

            return amount;
        }

        private static int CountVoicesWithClip(Rig rig, AudioClip clip)
        {
            int amount = 0;

            for (int i = 0; i < rig.Voices.Length; i++)
            {
                if (rig.Voices[i].clip == clip)
                    amount++;
            }

            return amount;
        }

        private static int SoonestVoiceIndex(Rig rig)
        {
            float[] busyUntil = (float[])BusyUntilField.GetValue(rig.Service);
            int soonestIndex = 0;

            for (int i = 1; i < busyUntil.Length; i++)
            {
                if (busyUntil[i] < busyUntil[soonestIndex])
                    soonestIndex = i;
            }

            return soonestIndex;
        }
    }
}

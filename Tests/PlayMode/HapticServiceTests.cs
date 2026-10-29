using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Horcrux.Runtime.Abstractions.Haptics;
using Lofelt.NiceVibrations;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// Runtime behaviour of <see cref="HapticService{THaptic}"/>: the on/off gate and the id → preset table. The host is built
    /// inactive, wired through reflection the way the Inspector would, then activated so <c>Awake</c> runs against a finished setup.
    /// </summary>
    /// <remarks>
    /// The host calls <see cref="HapticPatterns.PlayPreset"/> directly, so a test cannot count plays: these tests read the gate
    /// and the table through the log only. In the Editor <c>PlayPreset</c> neither vibrates nor throws; the actual vibration is
    /// checked on a device (play-test script in <c>HapticSystem.md</c>).
    /// </remarks>
    public class HapticServiceTests
    {
        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo EntriesField =
            typeof(HapticService<TestHaptic>).GetField("entries", NonPublicInstance);

        private readonly List<Object> _created = new();

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                    Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
        }

        [Test]
        public void Play_KnownId_LogsNothing()
        {
            TestHapticService service = Build(Entries(Entry(TestHaptic.A, HapticPatterns.PresetType.LightImpact)));
            service.IsHapticEnabled = true;

            service.Play(TestHaptic.A);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Play_UnknownId_LogsError()
        {
            TestHapticService service = Build(Entries(Entry(TestHaptic.A, HapticPatterns.PresetType.LightImpact)));
            service.IsHapticEnabled = true;

            LogAssert.Expect(LogType.Error, new Regex(@"\[HapticService\]: Haptic B is not in the entries"));
            service.Play(TestHaptic.B);
        }

        [Test]
        public void Play_WhenDisabled_ReturnsBeforeLookup()
        {
            TestHapticService service = Build(Entries());
            service.IsHapticEnabled = false;

            service.Play(TestHaptic.A);

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Play_ReEnabled_LooksUpAgain()
        {
            TestHapticService service = Build(Entries());
            service.IsHapticEnabled = false;
            service.Play(TestHaptic.A);

            service.IsHapticEnabled = true;

            LogAssert.Expect(LogType.Error, new Regex(@"\[HapticService\]: Haptic A is not in the entries"));
            service.Play(TestHaptic.A);
        }

        [Test]
        public void GateWrittenBeforeAwake_SurvivesAwake()
        {
            TestHapticService service = Build(Entries(), activate: false);
            service.IsHapticEnabled = true;
            service.gameObject.SetActive(true);

            LogAssert.Expect(LogType.Error, new Regex(@"\[HapticService\]: Haptic A is not in the entries"));
            service.Play(TestHaptic.A);
        }

        [Test]
        public void DuplicateEntryId_ThrowsInAwake()
        {
            LogAssert.Expect(LogType.Exception, new Regex("ArgumentException"));

            Build(Entries(
                Entry(TestHaptic.A, HapticPatterns.PresetType.LightImpact),
                Entry(TestHaptic.A, HapticPatterns.PresetType.SoftImpact)));
        }

        // ---------------------------------------------------------------- helpers

        private TestHapticService Build(HapticEntry<TestHaptic>[] entries, bool activate = true)
        {
            GameObject root = Own(new GameObject("haptic_test"));
            root.SetActive(false);

            TestHapticService service = root.AddComponent<TestHapticService>();
            EntriesField.SetValue(service, entries);

            if (activate)
                root.SetActive(true);

            return service;
        }

        private static HapticEntry<TestHaptic>[] Entries(params HapticEntry<TestHaptic>[] entries) => entries;

        private static HapticEntry<TestHaptic> Entry(TestHaptic id, HapticPatterns.PresetType preset)
        {
            var entry = new HapticEntry<TestHaptic>();
            SetField(entry, "id", id);
            SetField(entry, "preset", preset);
            return entry;
        }

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
    }
}

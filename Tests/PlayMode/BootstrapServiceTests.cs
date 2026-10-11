using System.Collections.Generic;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.Bootstrap;
using Horcrux.Runtime.Implementations.Bootstrap;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// <see cref="BootstrapService"/> reached before its own <c>Awake</c>. The host is built inactive so nothing has run yet,
    /// the API is called the way another object's <c>Awake</c> would call it, and the host is activated afterwards.
    /// </summary>
    public class BootstrapServiceTests
    {
        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private static readonly FieldInfo StepsField = typeof(BootstrapService).GetField("steps", NonPublicInstance);
        private static readonly FieldInfo OrderField = typeof(ABootStep).GetField("order", NonPublicInstance);

        private static readonly (string name, int order)[] TiedOrders = { ("a", 0), ("b", 10), ("c", 0) };
        private static readonly string[] ExpectedOrder = { "a", "c", "b" };

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

        // ---------------------------------------------------------------- InitializeAsync before Awake

        [Test]
        public void InitializeAsync_BeforeAwake_RunsStepsSortedOnce()
        {
            Rig rig = Build(TiedOrders);

            UniTask task = rig.Service.InitializeAsync();

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            CollectionAssert.AreEqual(ExpectedOrder, rig.Log);
            Assert.IsTrue(rig.Service.IsInitialized);
        }

        [Test]
        public void InitializeAsync_AwakeArrivesMidPhase_NeitherRepeatsNorSkipsSteps()
        {
            Rig rig = Build(TiedOrders);
            UniTaskCompletionSource gate = rig.StepNamed("a").Block();

            UniTask task = rig.Service.InitializeAsync();
            Assert.AreEqual(UniTaskStatus.Pending, task.Status);

            rig.Root.SetActive(true);
            gate.TrySetResult();

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            CollectionAssert.AreEqual(ExpectedOrder, rig.Log);
        }

        [Test]
        public void InitializeAsync_AfterAwake_RunsStepsSortedOnce()
        {
            Rig rig = Build(TiedOrders);
            rig.Root.SetActive(true);

            UniTask task = rig.Service.InitializeAsync();

            Assert.AreEqual(UniTaskStatus.Succeeded, task.Status);
            CollectionAssert.AreEqual(ExpectedOrder, rig.Log);
        }

        // ---------------------------------------------------------------- UntilInitializedAsync before Awake

        [Test]
        public void UntilInitializedAsync_BeforeAwake_WaitsForColdStart()
        {
            Rig rig = Build(("a", 0));

            UniTask waiter = rig.Service.UntilInitializedAsync();
            Assert.AreEqual(UniTaskStatus.Pending, waiter.Status);

            rig.Service.InitializeAsync().Forget();

            Assert.AreEqual(UniTaskStatus.Succeeded, waiter.Status);
        }

        [Test]
        public void UntilInitializedAsync_TwoWaitersBeforeAwake_BothRelease()
        {
            Rig rig = Build(("a", 0));

            UniTask first = rig.Service.UntilInitializedAsync();
            UniTask second = rig.Service.UntilInitializedAsync();
            rig.Service.InitializeAsync().Forget();

            Assert.AreEqual(UniTaskStatus.Succeeded, first.Status);
            Assert.AreEqual(UniTaskStatus.Succeeded, second.Status);
        }

        // ---------------------------------------------------------------- rig

        private sealed class Rig
        {
            public GameObject Root;
            public BootstrapService Service;
            public List<TestBootStep> Steps;
            public List<string> Log;

            public TestBootStep StepNamed(string stepName) => Steps.Find(step => step.name == stepName);
        }

        /// <summary>Inactive host plus steps in the given Inspector order. Nothing has run <c>Awake</c> until the test activates the root.</summary>
        private Rig Build(params (string name, int order)[] stepSpecs)
        {
            var root = new GameObject("bootstrap");
            _created.Add(root);
            root.SetActive(false);

            var rig = new Rig
            {
                Root = root,
                Service = root.AddComponent<BootstrapService>(),
                Steps = new List<TestBootStep>(stepSpecs.Length),
                Log = new List<string>(stepSpecs.Length),
            };

            var steps = new List<ABootStep>(stepSpecs.Length);
            for (int i = 0; i < stepSpecs.Length; i++)
            {
                var child = new GameObject(stepSpecs[i].name);
                child.transform.SetParent(root.transform, false);

                TestBootStep step = child.AddComponent<TestBootStep>();
                step.Setup(rig.Log);
                OrderField.SetValue(step, stepSpecs[i].order);

                rig.Steps.Add(step);
                steps.Add(step);
            }

            StepsField.SetValue(rig.Service, steps);
            return rig;
        }
    }
}

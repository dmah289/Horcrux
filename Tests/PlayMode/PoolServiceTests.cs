using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Horcrux.Runtime.Implementations.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Horcrux.Tests
{
    /// <summary>
    /// <c>Get</c>, <c>Return</c> and <c>ReleaseAll</c> of <see cref="PoolService{TKey}"/> against an entry inserted through
    /// reflection, shaped the way <c>WarmAsync</c> builds it, with a prefab made at run time. Addressables are never touched:
    /// the entry's <see cref="AssetReference"/> is empty, so <c>ReleaseAll</c> skips the asset release.
    /// </summary>
    /// <remarks>
    /// <c>WarmAsync</c> itself (load, overlap guard, cancel from <c>ReleaseAll</c>) needs a real Addressables build and is checked
    /// by hand in Play (Pooling_Plan §6.3). <see cref="GetReturn_TenThousandCycles_DoNotAllocate"/> uses
    /// <see cref="AllocationMeter"/>; the answer that matters is on an IL2CPP player.
    /// </remarks>
    public class PoolServiceTests
    {
        private const BindingFlags NonPublicInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private const int CycleAmount = 10_000;

        private static readonly Type EntryType =
            typeof(PoolService<>).GetNestedType("PoolEntry", BindingFlags.NonPublic).MakeGenericType(typeof(TestPool));
        private static readonly FieldInfo EntryByKeyField = ServiceField("_entryByKey");
        private static readonly FieldInfo EntryByActiveInstanceField = ServiceField("_entryByActiveInstance");
        private static readonly FieldInfo InactiveField = EntryType.GetField("Inactive");

        private static readonly Regex NotFoundInAnyPool = new(@"\[PoolService\]: .* was not found in any pool");

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

        // ---------------------------------------------------------------- Get

        [Test]
        public void Get_EmptyPool_InstantiatesActivatesParentsAndCallsHookOnce()
        {
            Rig rig = Build();

            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);

            Assert.IsTrue(got != null);
            Assert.AreNotSame(rig.Prefab, got);
            Assert.IsTrue(got.gameObject.activeSelf);
            Assert.AreSame(rig.Parent, got.transform.parent);
            Assert.AreEqual(1, got.GetAmount);
            Assert.AreEqual(0, got.ReturnAmount);
            Assert.IsTrue(ActiveTable(rig).Contains(got));
            Assert.AreEqual(0, Inactive(rig).Count);
        }

        [Test]
        public void Get_NullParent_LeavesInstanceAtSceneRoot()
        {
            Rig rig = Build();

            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, null);
            Own(got.gameObject);

            Assert.IsTrue(got.transform.parent == null);
        }

        [Test]
        public void Get_PoolHasOne_PopsItWithoutInstantiating()
        {
            Rig rig = Build(inactiveAmount: 1);
            var pooled = (TestPoolable)Inactive(rig).Peek();
            int aliveBefore = AliveAmount();

            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);

            Assert.AreSame(pooled, got);
            Assert.AreEqual(aliveBefore, AliveAmount());
            Assert.AreEqual(1, got.GetAmount);
            Assert.AreEqual(0, Inactive(rig).Count);
        }

        [Test]
        public void Get_KeyNotWarmed_LogsErrorAndReturnsNull()
        {
            Rig rig = Build();

            LogAssert.Expect(LogType.Error, new Regex(@"\[PoolService\]: pool 'B' has not been warmed"));
            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.B, rig.Parent);

            Assert.IsNull(got);
        }

        [Test]
        public void Get_WrongType_ThrowsInvalidCastAndKeepsActiveTableEmpty()
        {
            Rig rig = Build();

            Assert.Throws<InvalidCastException>(() => rig.Service.Get<OtherPoolable>(TestPool.A, rig.Parent));

            Assert.AreEqual(0, ActiveTable(rig).Count);
        }

        [Test]
        public void Get_InactiveInstanceDestroyedOutside_LogsErrorAndReturnsAnotherAlive()
        {
            Rig rig = Build(inactiveAmount: 1);
            Component dead = Inactive(rig).Peek();
            Object.DestroyImmediate(dead.gameObject);

            LogAssert.Expect(LogType.Error, new Regex(@"\[PoolService\]: An instance in pool A is destroyed, skip"));
            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);

            Assert.IsTrue(got != null);
            Assert.IsTrue(got.gameObject.activeSelf);
            Assert.AreEqual(0, Inactive(rig).Count);
        }

        // ---------------------------------------------------------------- Return

        [Test]
        public void Return_ActiveInstance_CallsHookDeactivatesParentsToPoolAndStacksIt()
        {
            Rig rig = Build();
            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);

            rig.Service.Return(got);

            Assert.AreEqual(1, got.ReturnAmount);
            Assert.IsFalse(got.gameObject.activeSelf);
            Assert.AreSame(rig.Service.transform, got.transform.parent);
            Assert.AreEqual(1, Inactive(rig).Count);
            Assert.AreSame(got, Inactive(rig).Peek());
            Assert.IsFalse(ActiveTable(rig).Contains(got));
        }

        [UnityTest]
        public IEnumerator Return_SameInstanceTwice_LogsErrorAndKeepsIt()
        {
            Rig rig = Build();
            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);
            rig.Service.Return(got);

            LogAssert.Expect(LogType.Error, NotFoundInAnyPool);
            rig.Service.Return(got);

            Assert.AreEqual(1, got.ReturnAmount);
            Assert.AreEqual(1, Inactive(rig).Count);
            yield return null;
            Assert.IsTrue(got != null, "The second Return destroyed the pooled instance.");
        }

        [UnityTest]
        public IEnumerator Return_StrangerInstance_LogsErrorAndLeavesItAlive()
        {
            Rig rig = Build();
            TestPoolable stranger = Own(new GameObject("stranger")).AddComponent<TestPoolable>();

            LogAssert.Expect(LogType.Error, NotFoundInAnyPool);
            rig.Service.Return(stranger);

            Assert.AreEqual(0, stranger.ReturnAmount);
            Assert.AreEqual(0, Inactive(rig).Count);
            yield return null;
            Assert.IsTrue(stranger != null);
        }

        [UnityTest]
        public IEnumerator Return_WhenInactiveIsAtMax_CallsHookOnceAndDestroysTheInstance()
        {
            Rig rig = Build(maxPoolSize: 1);
            TestPoolable first = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);
            TestPoolable second = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);

            rig.Service.Return(first);
            rig.Service.Return(second);

            Assert.AreEqual(1, second.ReturnAmount);
            Assert.AreEqual(1, Inactive(rig).Count);
            Assert.AreSame(first, Inactive(rig).Peek());
            Assert.IsFalse(ActiveTable(rig).Contains(second));
            yield return null;
            Assert.IsTrue(second == null, "The instance over maxPoolSize was kept instead of destroyed.");
            Assert.IsTrue(first != null);
        }

        [Test]
        public void Return_Null_ThrowsArgumentNull()
        {
            Rig rig = Build();

            Assert.Throws<ArgumentNullException>(() => rig.Service.Return<TestPoolable>(null));
        }

        // ---------------------------------------------------------------- ReleaseAll

        [UnityTest]
        public IEnumerator ReleaseAll_TwoActiveThreeInactive_DestroysFiveAndEmptiesBothTables()
        {
            Rig rig = Build(inactiveAmount: 5);
            var instances = new List<Component>
            {
                rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent),
                rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent),
            };
            instances.AddRange(Inactive(rig));
            Assert.AreEqual(5, instances.Count);

            rig.Service.ReleaseAll();

            Assert.AreEqual(0, ActiveTable(rig).Count);
            Assert.AreEqual(0, EntryTable(rig).Count);
            yield return null;

            for (int i = 0; i < instances.Count; i++)
                Assert.IsTrue(instances[i] == null, $"Instance {i} survived ReleaseAll.");

            Assert.DoesNotThrow(() => rig.Service.ReleaseAll());
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Return_AfterReleaseAll_LogsError()
        {
            Rig rig = Build();
            TestPoolable got = rig.Service.Get<TestPoolable>(TestPool.A, rig.Parent);
            rig.Service.ReleaseAll();

            LogAssert.Expect(LogType.Error, NotFoundInAnyPool);
            rig.Service.Return(got);
        }

        // ---------------------------------------------------------------- Allocation

        [Test]
        public void GetReturn_TenThousandCycles_DoNotAllocate()
        {
            Rig rig = Build(inactiveAmount: 1);
            // Parent = the pool itself, so both SetParent calls are no-ops and Editor hierarchy bookkeeping stays out of the meter.
            Transform parent = rig.Service.transform;

            AllocationMeter.AssertNoAllocation(() =>
            {
                for (int i = 0; i < CycleAmount; i++)
                    rig.Service.Return(rig.Service.Get<TestPoolable>(TestPool.A, parent));
            }, $"Get/Return over {CycleAmount} cycles");

            Assert.AreEqual(1, Inactive(rig).Count);
            Assert.AreEqual(0, ActiveTable(rig).Count);
        }

        // ---------------------------------------------------------------- Rig

        private sealed class Rig
        {
            public TestPoolService Service;
            public TestPoolable Prefab;
            public Transform Parent;
        }

        /// <summary>
        /// Host with one entry for <see cref="TestPool.A"/>, built the way <c>WarmOneAsync</c> leaves it: <paramref name="inactiveAmount"/>
        /// inactive clones of the prefab parented to the host, an empty <see cref="AssetReference"/>, nothing active yet.
        /// </summary>
        private Rig Build(int maxPoolSize = 5, int inactiveAmount = 0)
        {
            GameObject prefab = Own(new GameObject("pool_prefab"));
            prefab.SetActive(false);

            var rig = new Rig
            {
                Service = Own(new GameObject("pool_service")).AddComponent<TestPoolService>(),
                Prefab = prefab.AddComponent<TestPoolable>(),
                Parent = Own(new GameObject("pool_parent")).transform,
            };

            object entry = Activator.CreateInstance(EntryType, TestPool.A, new AssetReference(), (Component)rig.Prefab, maxPoolSize);
            EntryTable(rig).Add(TestPool.A, entry);

            var inactive = (Stack<Component>)InactiveField.GetValue(entry);
            for (int i = 0; i < inactiveAmount; i++)
            {
                Component instance = Object.Instantiate(rig.Prefab, rig.Service.transform);
                instance.gameObject.SetActive(false);
                inactive.Push(instance);
            }

            return rig;
        }

        // ---------------------------------------------------------------- Helpers

        private static FieldInfo ServiceField(string name)
        {
            FieldInfo field = typeof(PoolService<TestPool>).GetField(name, NonPublicInstance);
            Assert.NotNull(field, $"PoolService.{name} is missing.");
            return field;
        }

        private static IDictionary EntryTable(Rig rig) => (IDictionary)EntryByKeyField.GetValue(rig.Service);

        private static IDictionary ActiveTable(Rig rig) => (IDictionary)EntryByActiveInstanceField.GetValue(rig.Service);

        private static Stack<Component> Inactive(Rig rig) => (Stack<Component>)InactiveField.GetValue(EntryTable(rig)[TestPool.A]);

        private static int AliveAmount() =>
            Object.FindObjectsByType<TestPoolable>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length;

        private T Own<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }
    }
}

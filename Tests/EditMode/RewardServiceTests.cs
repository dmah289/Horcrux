using System.Text.RegularExpressions;
using Horcrux.Runtime.Abstractions.Reward;
using Horcrux.Runtime.Implementations.Reward;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Horcrux.Tests
{
    /// <summary>Covers the four agent cases in RewardSystem.md "Kiểm": dispatch, missing handler, bad amount, duplicate register.</summary>
    public sealed class RewardServiceTests
    {
        // Every log the service writes opens with its name; tests match that, never the sentence.
        private static readonly Regex ServiceError = new Regex(@"^\[RewardService\]: ");

        private GameObject host;
        private RewardService service;

        #region Setup
        [SetUp]
        public void SetUp()
        {
            host = new GameObject(nameof(RewardServiceTests));
            service = host.AddComponent<RewardService>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(host);
        }
        #endregion

        [Test]
        public void Grant_RegisteredType_CallsHandlerOnceWithSameData()
        {
            RecordingHandler handler = new RecordingHandler();
            service.Register(7, handler);

            service.Grant(new RewardData(7, 30), "test");

            Assert.AreEqual(1, handler.Calls);
            Assert.AreEqual(7, handler.LastReward.TypeId);
            Assert.AreEqual(30, handler.LastReward.Amount);
            Assert.AreEqual("test", handler.LastPlacement);
        }

        [Test]
        public void Grant_UnregisteredType_LogsErrorAndDoesNotThrow()
        {
            LogAssert.Expect(LogType.Error, ServiceError);

            Assert.DoesNotThrow(() => service.Grant(new RewardData(99, 1), "test"));
        }

        [Test]
        public void Grant_NonPositiveAmount_LogsErrorAndSkipsHandler()
        {
            RecordingHandler handler = new RecordingHandler();
            service.Register(1, handler);
            LogAssert.Expect(LogType.Error, ServiceError);

            service.Grant(new RewardData(1, 0), "test");

            Assert.AreEqual(0, handler.Calls);
        }

        [Test]
        public void Register_DuplicateType_LogsErrorAndKeepsFirst()
        {
            RecordingHandler first = new RecordingHandler();
            RecordingHandler second = new RecordingHandler();
            service.Register(1, first);
            LogAssert.Expect(LogType.Error, ServiceError);
            service.Register(1, second);

            service.Grant(new RewardData(1, 5), "t");

            Assert.AreEqual(1, first.Calls);
            Assert.AreEqual(0, second.Calls);
        }

        #region Fixture
        private sealed class RecordingHandler : IRewardHandler
        {
            public int Calls;
            public RewardData LastReward;
            public string LastPlacement;

            public void Grant(in RewardData reward, string placement)
            {
                Calls++;
                LastReward = reward;
                LastPlacement = placement;
            }
        }
        #endregion
    }
}

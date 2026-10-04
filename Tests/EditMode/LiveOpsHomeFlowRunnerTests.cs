using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Horcrux.Runtime.Abstractions.LiveOps;
using Horcrux.Runtime.Implementations.LiveOps;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>
    /// Covers the runner cases in LiveOpsHomeFlow_Plan.md §7. Fake flows are driven by completion sources, which
    /// resume synchronously, so no PlayerLoop is needed.
    /// </summary>
    public sealed class LiveOpsHomeFlowRunnerTests
    {
        private const string Progress = "progress";
        private const string Tutorial = "tutorial";
        private const string Review = "review";
        private const string Promo = "promo";

        private List<string> log;
        private List<Exception> errors;

        #region Setup
        [SetUp]
        public void SetUp()
        {
            log = new List<string>();
            errors = new List<Exception>();
        }
        #endregion

        #region Order
        [Test]
        public void OneFlow_PlaysFourStagesInOrder()
        {
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", Flow("A")));

            runner.OnHomeEnter();

            CollectionAssert.AreEqual(new[] { "A:progress", "A:tutorial", "A:review", "A:promo" }, log);
        }

        [Test]
        public void FirstFlowBlocked_SecondAndParallelWaitForIt()
        {
            FakeFlow a = Flow("A");
            UniTaskCompletionSource aProgress = a.Block(Progress);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a),
                Module("b", Flow("B", LiveOpsProgressChangePhase.Second)),
                Module("c", Flow("C", LiveOpsProgressChangePhase.Parallel)));

            runner.OnHomeEnter();
            CollectionAssert.DoesNotContain(log, "B:progress");
            CollectionAssert.DoesNotContain(log, "C:progress");

            aProgress.TrySetResult();
            CollectionAssert.Contains(log, "B:progress");
            CollectionAssert.Contains(log, "C:progress");
        }

        [Test]
        public void SecondAndParallelBlocked_StartTogether_TutorialWaitsForBoth()
        {
            FakeFlow b = Flow("B", LiveOpsProgressChangePhase.Second);
            FakeFlow c = Flow("C", LiveOpsProgressChangePhase.Parallel);
            UniTaskCompletionSource bProgress = b.Block(Progress);
            UniTaskCompletionSource cProgress = c.Block(Progress);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("b", b), Module("c", c));

            runner.OnHomeEnter();
            CollectionAssert.Contains(log, "B:progress");
            CollectionAssert.Contains(log, "C:progress");

            bProgress.TrySetResult();
            Assert.IsFalse(log.Exists(entry => entry.EndsWith(":tutorial")));

            cProgress.TrySetResult();
            CollectionAssert.Contains(log, "B:tutorial");
        }

        [Test]
        public void TwoFirstFlowsBlocked_BothStarted()
        {
            FakeFlow a1 = Flow("A1");
            FakeFlow a2 = Flow("A2");
            a1.Block(Progress);
            a2.Block(Progress);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a1", a1), Module("a2", a2));

            runner.OnHomeEnter();

            CollectionAssert.Contains(log, "A1:progress");
            CollectionAssert.Contains(log, "A2:progress");
        }

        [Test]
        public void ParallelFlows_RunByPriorityThenModuleId_TutorialSameOrder()
        {
            FakeFlow b = Flow("b", LiveOpsProgressChangePhase.Parallel);
            FakeFlow z = Flow("z", LiveOpsProgressChangePhase.Parallel);
            FakeFlow a = Flow("a", LiveOpsProgressChangePhase.Parallel);
            UniTaskCompletionSource bProgress = b.Block(Progress);
            UniTaskCompletionSource zProgress = z.Block(Progress);
            UniTaskCompletionSource aProgress = a.Block(Progress);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("b", b, 1), Module("z", z, 5), Module("a", a, 1));

            runner.OnHomeEnter();
            CollectionAssert.AreEqual(new[] { "z:progress" }, Stage(Progress));

            zProgress.TrySetResult();
            CollectionAssert.AreEqual(new[] { "z:progress", "a:progress" }, Stage(Progress));

            aProgress.TrySetResult();
            bProgress.TrySetResult();
            CollectionAssert.AreEqual(new[] { "z:tutorial", "a:tutorial", "b:tutorial" }, Stage(Tutorial));
        }

        [Test]
        public void FlowWithoutPhase_IsSecond_AndWaitsForFirst()
        {
            FakeFlow a = Flow("A");
            UniTaskCompletionSource aProgress = a.Block(Progress);
            PlainFlow plain = new PlainFlow();
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a), Module("p", plain));

            runner.OnHomeEnter();
            CollectionAssert.DoesNotContain(log, "A:tutorial");

            aProgress.TrySetResult();
            Assert.AreEqual(LiveOpsProgressChangePhase.Second, plain.ProgressChangePhase);
            CollectionAssert.Contains(log, "A:tutorial");
            Assert.IsEmpty(errors);
        }
        #endregion

        #region Module list
        [Test]
        public void ModuleWithoutHomeFlow_IsSkipped()
        {
            LiveOpsHomeFlowRunner runner = NewRunner(Module("n", null), Module("a", Flow("A")));

            runner.OnHomeEnter();

            CollectionAssert.AreEqual(new[] { "A:progress", "A:tutorial", "A:review", "A:promo" }, log);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void EmptyList_RunsNothing()
        {
            LiveOpsHomeFlowRunner runner = NewRunner();

            runner.OnHomeEnter();

            Assert.IsEmpty(log);
            Assert.IsEmpty(errors);
        }

        [Test]
        public void ModuleAddedAfterFirstPass_JoinsNextPass()
        {
            List<ILiveOpsModule> modules = new List<ILiveOpsModule> { Module("a", Flow("A")) };
            LiveOpsHomeFlowRunner runner = new LiveOpsHomeFlowRunner(modules, errors.Add);
            runner.OnHomeEnter();

            modules.Add(Module("b", Flow("B")));
            runner.Request();

            CollectionAssert.Contains(log, "B:progress");
        }

        [Test]
        public void PriorityChangedBetweenPasses_NextPassFollowsNewOrder()
        {
            FakeModule a = Module("a", Flow("A"), 2);
            FakeModule b = Module("b", Flow("B"), 1);
            LiveOpsHomeFlowRunner runner = NewRunner(a, b);
            runner.OnHomeEnter();
            log.Clear();

            b.Priority = 9;
            runner.Request();

            CollectionAssert.AreEqual(new[] { "B:tutorial", "A:tutorial" }, Stage(Tutorial));
        }
        #endregion

        #region Errors
        [Test]
        public void ThrowInTogetherLaneAndInOrderLane_BothReported_RestStillRuns()
        {
            FakeFlow a = Flow("A");
            FakeFlow c = Flow("C");
            a.Throws.Add(Progress);
            c.Throws.Add(Tutorial);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a, 3), Module("b", Flow("B"), 2), Module("c", c, 1));

            runner.OnHomeEnter();

            Assert.AreEqual(2, errors.Count);
            CollectionAssert.Contains(log, "B:progress");
            CollectionAssert.Contains(log, "A:tutorial");
            CollectionAssert.Contains(log, "C:review");
            CollectionAssert.Contains(log, "A:promo");
        }

        [Test]
        public void FlowRequestsEveryPass_StopsAtThreePasses_OneError()
        {
            FakeFlow a = Flow("A");
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a));
            a.OnProgress = runner.Request;

            runner.OnHomeEnter();

            Assert.AreEqual(3, Stage(Progress).Count);
            Assert.AreEqual(1, errors.Count);
        }
        #endregion

        #region Home enter and exit
        [Test]
        public void ExitWhileTutorialWaits_NoLaterStage_NotAnError()
        {
            FakeFlow a = Flow("A");
            a.Block(Tutorial);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a));
            runner.OnHomeEnter();

            runner.OnHomeExit();

            CollectionAssert.DoesNotContain(log, "A:tutorial-done");
            CollectionAssert.DoesNotContain(log, "A:review");
            Assert.IsEmpty(errors);
        }

        [Test]
        public void ThreeRequestsDuringPass_AddExactlyOnePass()
        {
            FakeFlow a = Flow("A");
            UniTaskCompletionSource tutorial = a.Block(Tutorial);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a));
            runner.OnHomeEnter();

            runner.Request();
            runner.Request();
            runner.Request();
            a.Unblock(Tutorial);
            tutorial.TrySetResult();

            Assert.AreEqual(2, Stage(Progress).Count);
        }

        [Test]
        public void RequestThenExit_PendingPassDropped()
        {
            FakeFlow a = Flow("A");
            UniTaskCompletionSource tutorial = a.Block(Tutorial);
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a));
            runner.OnHomeEnter();

            runner.Request();
            runner.OnHomeExit();
            tutorial.TrySetResult();

            Assert.AreEqual(1, Stage(Progress).Count);
        }

        [Test]
        public void RequestAwayFromHome_RunsNothing()
        {
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", Flow("A")));

            runner.Request();

            Assert.IsEmpty(log);
        }

        [Test]
        public void ReenterWhileOldFlowCleansUp_NewPassStartsAfterCleanup()
        {
            FakeFlow a = Flow("A");
            a.Block(Tutorial);
            UniTaskCompletionSource cleanup = new UniTaskCompletionSource();
            a.CleanupGate = cleanup;
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", a));
            runner.OnHomeEnter();

            runner.OnHomeExit();
            runner.OnHomeEnter();
            Assert.AreEqual(1, Stage(Progress).Count);

            a.CleanupGate = null;
            a.Unblock(Tutorial);
            cleanup.TrySetResult();

            int cleanupEnd = log.IndexOf("A:cleanup-end");
            Assert.GreaterOrEqual(cleanupEnd, 0);
            Assert.Greater(log.LastIndexOf("A:progress"), cleanupEnd);
        }

        [Test]
        public void ExitWithNoPassRunning_DoesNotThrow_ReenterStillRunsFullPass()
        {
            LiveOpsHomeFlowRunner runner = NewRunner(Module("a", Flow("A")));

            Assert.DoesNotThrow(() =>
            {
                runner.OnHomeExit();
                runner.OnHomeEnter();
                runner.OnHomeExit();
                runner.OnHomeEnter();
            });

            Assert.AreEqual(2, Stage(Promo).Count);
            Assert.IsEmpty(errors);
        }
        #endregion

        #region Helpers
        private LiveOpsHomeFlowRunner NewRunner(params ILiveOpsModule[] modules)
            => new LiveOpsHomeFlowRunner(new List<ILiveOpsModule>(modules), errors.Add);

        private FakeFlow Flow(string name, LiveOpsProgressChangePhase phase = LiveOpsProgressChangePhase.First)
            => new FakeFlow(name, phase, log);

        private static FakeModule Module(string id, ALiveOpsHomeFlow flow, int priority = 0)
            => new FakeModule { ModuleId = id, HomeFlow = flow, Priority = priority };

        private List<string> Stage(string stage)
            => log.FindAll(entry => entry.EndsWith(":" + stage));

        private sealed class FakeModule : ILiveOpsModule
        {
            public string ModuleId { get; set; }
            public LiveOpsModuleState State => LiveOpsModuleState.Running;
            public int Priority { get; set; }
            public ALiveOpsHomeFlow HomeFlow { get; set; }
            public ALiveOpsLoseFlow LoseFlow => null;

            public void Initialize(long nowUnix) { }
            public void Tick(long nowUnix) { }
        }

        private sealed class PlainFlow : ALiveOpsHomeFlow { }

        /// <summary>Logs "name:stage" on entry; a blocked stage waits on its gate, a throwing stage throws.</summary>
        private sealed class FakeFlow : ALiveOpsHomeFlow
        {
            private readonly string name;
            private readonly LiveOpsProgressChangePhase phase;
            private readonly List<string> log;
            private readonly Dictionary<string, UniTaskCompletionSource> gates = new();

            public readonly HashSet<string> Throws = new();
            public Action OnProgress;
            /// <summary>When set, a blocked stage awaits it in finally, the way a flow undoes a spotlight.</summary>
            public UniTaskCompletionSource CleanupGate;

            public FakeFlow(string name, LiveOpsProgressChangePhase phase, List<string> log)
            {
                this.name = name;
                this.phase = phase;
                this.log = log;
            }

            public override LiveOpsProgressChangePhase ProgressChangePhase => phase;

            public UniTaskCompletionSource Block(string stage)
            {
                UniTaskCompletionSource gate = new UniTaskCompletionSource();
                gates[stage] = gate;
                return gate;
            }

            public void Unblock(string stage) => gates.Remove(stage);

            public override UniTask PlayProgressChangeAsync(CancellationToken ct)
            {
                OnProgress?.Invoke();
                return Play(Progress, ct);
            }

            public override UniTask PlayTutorialAsync(CancellationToken ct) => Play(Tutorial, ct);
            public override UniTask PlayForcedReviewAsync(CancellationToken ct) => Play(Review, ct);
            public override UniTask PlayPromotionAsync(CancellationToken ct) => Play(Promo, ct);

            private async UniTask Play(string stage, CancellationToken ct)
            {
                log.Add($"{name}:{stage}");

                if (Throws.Contains(stage))
                    throw new InvalidOperationException("boom");

                if (!gates.TryGetValue(stage, out UniTaskCompletionSource gate))
                    return;

                try
                {
                    await gate.Task.AttachExternalCancellation(ct);
                }
                finally
                {
                    if (CleanupGate != null)
                    {
                        log.Add($"{name}:cleanup-start");
                        await CleanupGate.Task;
                        log.Add($"{name}:cleanup-end");
                    }
                }

                log.Add($"{name}:{stage}-done");
            }
        }
        #endregion
    }
}

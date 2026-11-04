using System.Collections.Generic;
using System.Linq;
using Horcrux.Editor.UserSegmentation;
using Horcrux.Runtime.Abstractions.UserSegmentation;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers the Simulator's Editor-only checks and its reflection over a project pool row.</summary>
    public sealed class UserSegmentationSimulatorTests
    {
        private const int Default = 1;
        private const int Iap = 2;
        private const int Purchased = 3;
        private const int Inherit = UserSegmentInheritance.InheritValue;

        public sealed class SimConfig : IUserSegmentConfig<SimConfig>
        {
            public int segment_id;
            public string name;
            public int a;
            public int b;

            public int Id => segment_id;

            public SimConfig InheritFrom(SimConfig prev) => new()
            {
                segment_id = segment_id,
                name = name,
                a = UserSegmentInheritance.Pick(a, prev.a),
                b = UserSegmentInheritance.Pick(b, prev.b),
            };
        }

        /// <summary>A project that added column b and forgot its Pick line.</summary>
        public sealed class ForgetfulConfig : IUserSegmentConfig<ForgetfulConfig>
        {
            public int segment_id;
            public int a;
            public int b;

            public int Id => segment_id;

            public ForgetfulConfig InheritFrom(ForgetfulConfig prev) => new()
            {
                segment_id = segment_id,
                a = UserSegmentInheritance.Pick(a, prev.a),
                b = b,
            };
        }

        private sealed class Rule : IUserSegmentKeywordRule
        {
            public int SegmentId { get; }
            public IReadOnlyList<string> Keywords { get; }

            public Rule(int segmentId, params string[] keywords)
            {
                SegmentId = segmentId;
                Keywords = keywords;
            }
        }

        private static SimConfig Row(int id, int a, int b) => new() { segment_id = id, name = "s" + id, a = a, b = b };

        private static List<SimConfig> Pool() => new()
        {
            Row(Default, 10, 20),
            Row(Iap, 11, 21),
            Row(Purchased, Inherit, 5),
        };

        private static List<IUserSegmentKeywordRule> Rules() => new() { new Rule(Iap, "IAP"), new Rule(Default, "BLD") };

        private static List<string> Check<T>(List<T> pool, List<IUserSegmentKeywordRule> rules) where T : class, IUserSegmentConfig
        {
            var errors = new List<string>();
            UserSegmentationSimulatorCore.Check(typeof(T), pool, rules, Default, Purchased, errors);
            return errors;
        }

        [Test]
        public void IntFieldsOf_PublicIntFieldsOnly()
            => CollectionAssert.AreEqual(new[] { "segment_id", "a", "b" },
                UserSegmentationSimulatorCore.IntFieldsOf(typeof(SimConfig)).Select(f => f.Name));

        [Test]
        public void FindIdField_FieldBehindId()
        {
            var ints = UserSegmentationSimulatorCore.IntFieldsOf(typeof(SimConfig));

            Assert.AreEqual("segment_id", UserSegmentationSimulatorCore.FindIdField(typeof(SimConfig), ints).Name);
        }

        [Test]
        public void Inherit_CallsDtoInheritFrom()
        {
            var merged = (SimConfig)UserSegmentationSimulatorCore.Inherit(typeof(SimConfig), Row(Purchased, Inherit, 5), Row(Default, 10, 20));

            Assert.AreEqual(Purchased, merged.Id);
            Assert.AreEqual(10, merged.a);
            Assert.AreEqual(5, merged.b);
        }

        [Test]
        public void ValidData_NoError() => CollectionAssert.IsEmpty(Check(Pool(), Rules()));

        [Test]
        public void InheritOutsidePurchased_OneLineNamingSegmentAndField()
        {
            var pool = Pool();
            pool[1].a = Inherit;

            var errors = Check(pool, Rules());

            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("segment 2", errors[0]);
            StringAssert.Contains(" a;", errors[0]);
        }

        [Test]
        public void InheritFromForgetsColumn_OneLineNamingColumn()
        {
            var pool = new List<ForgetfulConfig>
            {
                new() { segment_id = Default, a = 10, b = 20 },
                new() { segment_id = Purchased, a = Inherit, b = Inherit },
            };

            var errors = Check(pool, Rules().Where(r => r.SegmentId == Default).ToList());

            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("does not take b", errors[0]);
        }

        [Test]
        public void KeywordRowPointingAtPurchased_OneLineNamingRow()
        {
            var rules = Rules();
            rules.Add(new Rule(Purchased, "Paid"));

            var errors = Check(Pool(), rules);

            Assert.AreEqual(1, errors.Count, string.Join("\n", errors));
            StringAssert.Contains("keyword row 2", errors[0]);
        }

        [Test]
        public void CoreValidateRuns_EmptyRulesReported()
            => Assert.That(Check(Pool(), new List<IUserSegmentKeywordRule>()), Has.Some.Contains("rules is empty"));

        [Test]
        public void NoDefaultRow_ReportedByCore_InheritCheckSkipped()
        {
            var pool = new List<SimConfig> { Row(Iap, 11, 21), Row(Purchased, Inherit, 5) };

            var errors = Check(pool, new List<IUserSegmentKeywordRule> { new Rule(Iap, "IAP") });

            Assert.That(errors, Has.Some.Contains("no pool row for the default segment"));
            Assert.That(errors, Has.None.Contains("does not take"));
        }
    }
}

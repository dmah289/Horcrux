using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.UserSegmentation;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers Find and the data guard in Horcrux_UserSegmentation_Plan "Luật nghiệp vụ": one line per invalid row, no role checks besides default.</summary>
    public sealed class UserSegmentConfigTableTests
    {
        private const int Default = 1;
        private const int Iap = 2;
        private const int Purchased = 3;
        private const int Ads = 4;

        private List<string> errors;

        #region Setup
        [SetUp]
        public void SetUp()
        {
            errors = new List<string>();
        }
        #endregion

        private static List<Config> Pool() => new List<Config>
        {
            new Config(Default, 10),
            new Config(Iap, 20),
            new Config(Purchased, UserSegmentInheritance.InheritValue),
            new Config(Ads, 5)
        };

        private static List<IUserSegmentKeywordRule> Rules() => new List<IUserSegmentKeywordRule>
        {
            new Rule(Iap, "IAP"),
            new Rule(Ads, "Ads"),
            new Rule(Default, "BLD", "Blended", "Hybrid")
        };

        private bool Validate(IReadOnlyList<Config> pool, IReadOnlyList<IUserSegmentKeywordRule> rules)
            => UserSegmentConfigTable.Validate(pool, rules, Default, errors);

        #region Find
        [Test]
        public void Find_RowPresent_ReturnsThatRow()
        {
            List<Config> pool = Pool();

            Assert.AreSame(pool[1], UserSegmentConfigTable.Find(pool, Iap));
        }

        [Test]
        public void Find_DuplicateId_ReturnsLastRow()
        {
            List<Config> pool = Pool();
            Config last = new Config(Iap, 99);
            pool.Add(last);

            Assert.AreSame(last, UserSegmentConfigTable.Find(pool, Iap));
        }

        [Test]
        public void Find_RowAbsent_Null()
        {
            Assert.IsNull(UserSegmentConfigTable.Find(Pool(), 9));
        }

        [Test]
        public void Find_NullConfigs_Null()
        {
            Assert.IsNull(UserSegmentConfigTable.Find<Config>(null, Default));
        }

        [Test]
        public void Find_NullRowInList_IsSkipped()
        {
            Config row = new Config(Default, 10);
            List<Config> pool = new List<Config> { null, row };

            Assert.AreSame(row, UserSegmentConfigTable.Find(pool, Default));
        }
        #endregion

        #region Validate pool
        [Test]
        public void Validate_FullPoolAndValidRules_TrueWithoutErrors()
        {
            Assert.IsTrue(Validate(Pool(), Rules()));
            CollectionAssert.IsEmpty(errors);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Validate_NullOrEmptyPool_EmptyAndMissingDefaultLinesWithoutThrowing(bool isNull)
        {
            List<Config> pool = isNull ? null : new List<Config>();
            List<IUserSegmentKeywordRule> rules = Rules();

            Assert.DoesNotThrow(() => Validate(pool, rules));

            // Two pool lines, plus one per keyword row: none of them has a pool row to point at.
            Assert.AreEqual(2 + rules.Count, errors.Count);
        }

        [Test]
        public void Validate_NullRow_OneLine()
        {
            List<Config> pool = Pool();
            pool.Insert(1, null);

            Assert.IsFalse(Validate(pool, Rules()));
            Assert.AreEqual(1, errors.Count);
        }

        [TestCase(0)]
        [TestCase(-2)]
        public void Validate_IdBelowOne_OneLine(int id)
        {
            List<Config> pool = Pool();
            pool.Add(new Config(id, 1));

            Assert.IsFalse(Validate(pool, Rules()));
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void Validate_TwoRowsShareId_OneLineAndUniqueRowsNotReported()
        {
            List<Config> pool = Pool();
            pool.Add(new Config(Ads, 6));

            Assert.IsFalse(Validate(pool, Rules()));
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void Validate_InheritCellOnNonPurchasedRow_NoLine()
        {
            // The core never reads columns; the project's Editor tool reports -1 outside the purchased row.
            List<Config> pool = Pool();
            pool[0] = new Config(Default, UserSegmentInheritance.InheritValue);

            Assert.IsTrue(Validate(pool, Rules()));
        }

        [Test]
        public void Validate_MissingDefaultRow_OneLine()
        {
            List<Config> pool = Pool();
            pool.RemoveAt(0);
            List<IUserSegmentKeywordRule> rules = new List<IUserSegmentKeywordRule> { new Rule(Iap, "IAP") };

            Assert.IsFalse(Validate(pool, rules));
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void Validate_MissingPurchasedRow_NoLine()
        {
            // The core knows no purchased role; each project guarantees its own row.
            List<Config> pool = Pool();
            pool.RemoveAt(2);

            Assert.IsTrue(Validate(pool, Rules()));
        }
        #endregion

        #region Validate rules
        [Test]
        public void Validate_NullRule_OneLine()
        {
            List<IUserSegmentKeywordRule> rules = Rules();
            rules.Add(null);

            Assert.IsFalse(Validate(Pool(), rules));
            Assert.AreEqual(1, errors.Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Validate_RuleWithoutKeywords_OneLine(bool isNull)
        {
            List<IUserSegmentKeywordRule> rules = Rules();
            rules.Add(isNull ? new Rule(Iap, null) : new Rule(Iap));

            Assert.IsFalse(Validate(Pool(), rules));
            Assert.AreEqual(1, errors.Count);
        }

        [Test]
        public void Validate_EmptyKeywordInArray_OneLineNamingPosition()
        {
            List<IUserSegmentKeywordRule> rules = Rules();
            rules.Add(new Rule(Iap, "IAP", ""));

            Assert.IsFalse(Validate(Pool(), rules));
            Assert.AreEqual(1, errors.Count);
            StringAssert.Contains("at 1", errors[0]);
        }

        [Test]
        public void Validate_RulePointsAtPurchased_NoLine()
        {
            // A data error, but only the project's Simulator knows the purchased role.
            List<IUserSegmentKeywordRule> rules = Rules();
            rules.Add(new Rule(Purchased, "Paid"));

            Assert.IsTrue(Validate(Pool(), rules));
        }

        [Test]
        public void Validate_RulePointsAtIdWithoutPoolRow_OneLine()
        {
            List<IUserSegmentKeywordRule> rules = Rules();
            rules.Add(new Rule(9, "Ghost"));

            Assert.IsFalse(Validate(Pool(), rules));
            Assert.AreEqual(1, errors.Count);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void Validate_NullOrEmptyRules_OneLineWithoutThrowing(bool isNull)
        {
            List<IUserSegmentKeywordRule> rules = isNull ? null : new List<IUserSegmentKeywordRule>();

            Assert.DoesNotThrow(() => Validate(Pool(), rules));
            Assert.AreEqual(1, errors.Count);
        }
        #endregion

        private sealed class Config : IUserSegmentConfig
        {
            public int Id { get; }
            public readonly int InterStartLevel;

            public Config(int id, int interStartLevel)
            {
                Id = id;
                InterStartLevel = interStartLevel;
            }
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
    }
}

using System.Collections.Generic;
using Horcrux.Runtime.Abstractions.UserSegmentation;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers the keyword rule in Horcrux_UserSegmentation_Plan "Luật nghiệp vụ": any keyword of a row, first row wins, ambiguity across segments only.</summary>
    public sealed class UserSegmentKeywordMatcherTests
    {
        private const int Default = 1;
        private const int Iap = 2;
        private const int Ads = 4;
        private const int Other = 5;

        private static bool Match(string campaign, IReadOnlyList<IUserSegmentKeywordRule> rules,
            out int segmentId, out string keyword, out bool isAmbiguous)
            => UserSegmentKeywordMatcher.TryMatch(campaign, rules, out segmentId, out keyword, out isAmbiguous);

        /// <summary>The category-jam table: IAP before Ads, BLD row with three keywords.</summary>
        private static List<IUserSegmentKeywordRule> Table() => new List<IUserSegmentKeywordRule>
        {
            new Rule(Iap, "IAP"),
            new Rule(Ads, "Ads"),
            new Rule(Default, "BLD", "Blended", "Hybrid")
        };

        #region Guards
        [Test]
        public void TryMatch_NullCampaign_False()
        {
            Assert.IsFalse(Match(null, Table(), out int segmentId, out string keyword, out bool isAmbiguous));
            Assert.AreEqual(0, segmentId);
            Assert.IsNull(keyword);
            Assert.IsFalse(isAmbiguous);
        }

        [Test]
        public void TryMatch_EmptyCampaign_False()
        {
            Assert.IsFalse(Match(string.Empty, Table(), out int segmentId, out _, out _));
            Assert.AreEqual(0, segmentId);
        }

        [Test]
        public void TryMatch_NullRules_False()
        {
            Assert.IsFalse(Match("IAP_US", null, out int segmentId, out _, out _));
            Assert.AreEqual(0, segmentId);
        }

        [Test]
        public void TryMatch_NullRow_IsSkipped()
        {
            List<IUserSegmentKeywordRule> rules = new List<IUserSegmentKeywordRule> { null, new Rule(Iap, "IAP") };

            Assert.IsTrue(Match("IAP_US", rules, out int segmentId, out _, out _));
            Assert.AreEqual(Iap, segmentId);
        }
        #endregion

        #region Matching
        [Test]
        public void TryMatch_DifferentCase_Matches()
        {
            Assert.IsTrue(Match("iap_us", Table(), out int segmentId, out string keyword, out _));
            Assert.AreEqual(Iap, segmentId);
            Assert.AreEqual("IAP", keyword);
        }

        [Test]
        public void TryMatch_CampaignHoldsThirdKeywordOfRow_ReportsThatKeyword()
        {
            Assert.IsTrue(Match("Hybrid_01", Table(), out int segmentId, out string keyword, out _));
            Assert.AreEqual(Default, segmentId);
            Assert.AreEqual("Hybrid", keyword);
        }

        [Test]
        public void TryMatch_CampaignHoldsTwoKeywordsOfOneRow_ReportsArrayOrderNotCampaignOrder()
        {
            Assert.IsTrue(Match("Blended_BLD", Table(), out int segmentId, out string keyword, out bool isAmbiguous));
            Assert.AreEqual(Default, segmentId);
            Assert.AreEqual("BLD", keyword);
            Assert.IsFalse(isAmbiguous);
        }

        [Test]
        public void TryMatch_TwoRowsOfSameSegmentMatch_NotAmbiguous()
        {
            List<IUserSegmentKeywordRule> rules = new List<IUserSegmentKeywordRule>
            {
                new Rule(Iap, "IAP"),
                new Rule(Iap, "Purchase")
            };

            Assert.IsTrue(Match("IAP_Purchase", rules, out int segmentId, out _, out bool isAmbiguous));
            Assert.AreEqual(Iap, segmentId);
            Assert.IsFalse(isAmbiguous);
        }

        [TestCase("IAP_Ads")]
        [TestCase("Ads_IAP")]
        public void TryMatch_TwoSegmentsMatch_TableOrderWinsAndAmbiguous(string campaign)
        {
            Assert.IsTrue(Match(campaign, Table(), out int segmentId, out string keyword, out bool isAmbiguous));
            Assert.AreEqual(Iap, segmentId);
            Assert.AreEqual("IAP", keyword);
            Assert.IsTrue(isAmbiguous);
        }

        [Test]
        public void TryMatch_NoRowMatches_False()
        {
            Assert.IsFalse(Match("Organic_US", Table(), out int segmentId, out string keyword, out bool isAmbiguous));
            Assert.AreEqual(0, segmentId);
            Assert.IsNull(keyword);
            Assert.IsFalse(isAmbiguous);
        }

        [Test]
        public void TryMatch_RowWithNullOrEmptyKeywords_IsSkipped()
        {
            List<IUserSegmentKeywordRule> rules = new List<IUserSegmentKeywordRule>
            {
                new Rule(Iap, null),
                new Rule(Other),
                new Rule(Ads, "Ads")
            };

            Assert.IsTrue(Match("Ads_US", rules, out int segmentId, out _, out bool isAmbiguous));
            Assert.AreEqual(Ads, segmentId);
            Assert.IsFalse(isAmbiguous);
        }

        [Test]
        public void TryMatch_EmptyKeywordInArray_IsSkippedAndLaterKeywordMatches()
        {
            // An empty keyword would match every campaign (IndexOf("") is 0); the row must still match through "IAP".
            List<IUserSegmentKeywordRule> rules = new List<IUserSegmentKeywordRule>
            {
                new Rule(Other, ""),
                new Rule(Iap, "", "IAP")
            };

            Assert.IsTrue(Match("IAP_US", rules, out int segmentId, out string keyword, out _));
            Assert.AreEqual(Iap, segmentId);
            Assert.AreEqual("IAP", keyword);
        }

        // Substring match: a short keyword hits any campaign containing those letters. "Android" holds neither "Ads" nor "Ad".
        [TestCase("Ads")]
        [TestCase("Ad")]
        public void TryMatch_AndroidCampaignWithAdsKeyword_False(string adsKeyword)
        {
            List<IUserSegmentKeywordRule> rules = new List<IUserSegmentKeywordRule> { new Rule(Ads, adsKeyword) };

            Assert.IsFalse(Match("CategoryJam_Android_US", rules, out _, out _, out _));
        }
        #endregion

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

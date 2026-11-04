using Horcrux.Runtime.Abstractions.UserSegmentation;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers the rule table in Horcrux_UserSegmentation_Plan "Luật nghiệp vụ", ids as in category-jam: BLD 1 default, IAP 2, Purchased 3, Ads 4.</summary>
    public sealed class UserSegmentDeciderTests
    {
        private const int Default = 1;
        private const int Iap = 2;
        private const int Purchased = 3;
        private const int Ads = 4;
        private const int MaxSessions = 3;

        private static UserSegmentDecider.Change Boot(UserSegmentState state, int maxSessions = MaxSessions)
            => UserSegmentDecider.OnBoot(state, Default, maxSessions);

        private static UserSegmentDecider.Change Campaign(UserSegmentState state, string campaign, int matchedSegmentId,
            string matchedKeyword = null, bool isAmbiguous = false)
            => UserSegmentDecider.OnCampaign(state, campaign, matchedSegmentId, matchedKeyword, isAmbiguous, Default, Purchased);

        private static UserSegmentDecider.Change Purchase(UserSegmentState state, bool isRuleEnabled = true)
            => UserSegmentDecider.OnPurchase(state, isRuleEnabled, Default, Purchased);

        /// <summary>State after the first boot: default segment, session 1, window open.</summary>
        private static UserSegmentState Booted()
        {
            UserSegmentState state = new UserSegmentState();
            Boot(state);
            return state;
        }

        #region OnBoot
        [Test]
        public void OnBoot_EmptyState_DefaultSegmentInitialChangeSessionOne()
        {
            UserSegmentState state = new UserSegmentState();

            UserSegmentDecider.Change change = Boot(state);

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(0, change.PrevSegmentId);
            Assert.AreEqual(Default, change.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.Initial, change.Reason);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.AreEqual(1, state.SessionCount);
            Assert.IsFalse(state.IsCampaignLocked);
        }

        [Test]
        public void OnBoot_SessionsTwoAndThree_WindowStillOpenNoChange()
        {
            UserSegmentState state = Booted();

            for (int session = 2; session <= MaxSessions; session++)
            {
                Assert.IsFalse(Boot(state).HasChange, $"session {session}");
                Assert.IsFalse(state.IsCampaignLocked, $"session {session}");
            }

            Assert.AreEqual(MaxSessions, state.SessionCount);
        }

        [Test]
        public void OnBoot_SessionAfterMax_LocksWithoutChange()
        {
            UserSegmentState state = new UserSegmentState { CurrSegmentId = Iap, SessionCount = MaxSessions };

            UserSegmentDecider.Change change = Boot(state);

            Assert.IsFalse(change.HasChange);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual(Iap, state.CurrSegmentId);
            Assert.AreEqual(MaxSessions + 1, state.SessionCount);
        }

        [Test]
        public void OnBoot_AlreadyLocked_StaysLockedKeepsSegment()
        {
            UserSegmentState state = new UserSegmentState { CurrSegmentId = Ads, IsCampaignLocked = true, SessionCount = 1 };

            Assert.IsFalse(Boot(state).HasChange);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual(Ads, state.CurrSegmentId);
        }

        [Test]
        public void OnBoot_MaxZero_ClampedToOne()
        {
            // 0 would lock on the very first boot, before Adjust can answer: every user default forever.
            UserSegmentState state = new UserSegmentState();

            Boot(state, 0);
            Assert.IsFalse(state.IsCampaignLocked, "session 1");

            Boot(state, 0);
            Assert.IsTrue(state.IsCampaignLocked, "session 2");
        }
        #endregion

        #region OnCampaign
        [Test]
        public void OnCampaign_UnlockedMatchesIap_MovesToIapAndLocks()
        {
            UserSegmentState state = Booted();

            UserSegmentDecider.Change change = Campaign(state, "IAP_US", Iap, "IAP");

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Default, change.PrevSegmentId);
            Assert.AreEqual(Iap, change.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, change.Reason);
            Assert.AreEqual("IAP", change.MatchedKeyword);
            Assert.AreEqual(Iap, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual("IAP_US", state.LastCampaign);
        }

        [TestCase("Organic_US")]
        [TestCase("")]
        public void OnCampaign_UnlockedNoMatchAlreadyDefault_NoChangeButLocks(string campaign)
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(Campaign(state, campaign, 0).HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual(campaign, state.LastCampaign);
        }

        [Test]
        public void OnCampaign_MatchesPurchased_TreatedAsNoMatchStaysDefaultAndLocks()
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(Campaign(state, "Paid_US", Purchased, "Paid").HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
        }

        [Test]
        public void OnCampaign_Locked_NoChangeButLastCampaignWritten()
        {
            UserSegmentState state = Booted();
            state.IsCampaignLocked = true;

            Assert.IsFalse(Campaign(state, "IAP_US", Iap, "IAP").HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.AreEqual("IAP_US", state.LastCampaign);
        }

        [Test]
        public void OnCampaign_PurchasedAndUnlocked_LocksWithoutChange()
        {
            UserSegmentState state = new UserSegmentState { CurrSegmentId = Purchased, PrevSegmentId = Default, SessionCount = 1 };

            Assert.IsFalse(Campaign(state, "IAP_US", Iap, "IAP").HasChange);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
        }

        [Test]
        public void OnCampaign_Ambiguous_PassedToChange()
        {
            UserSegmentState state = Booted();

            Assert.IsTrue(Campaign(state, "IAP_Ads", Iap, "IAP", isAmbiguous: true).IsAmbiguous);
        }
        #endregion

        #region OnPurchase
        [Test]
        public void OnPurchase_FromDefault_PrevDefaultCurrPurchasedLocked()
        {
            UserSegmentState state = Booted();

            UserSegmentDecider.Change change = Purchase(state);

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Default, change.PrevSegmentId);
            Assert.AreEqual(Purchased, change.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.Purchase, change.Reason);
            Assert.IsNull(change.MatchedKeyword);
            Assert.AreEqual(Default, state.PrevSegmentId);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
        }

        [Test]
        public void OnPurchase_RuleDisabled_NoChangeStateUntouched()
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(Purchase(state, isRuleEnabled: false).HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.AreEqual(0, state.PrevSegmentId);
            Assert.IsFalse(state.IsCampaignLocked);
        }

        [Test]
        public void OnPurchase_SecondPurchase_NoChange()
        {
            UserSegmentState state = Booted();
            Campaign(state, "IAP_US", Iap, "IAP");
            Purchase(state);

            Assert.IsFalse(Purchase(state).HasChange);
            Assert.AreEqual(Iap, state.PrevSegmentId);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
        }

        [Test]
        public void OnPurchase_Undecided_PrevIsDefault()
        {
            UserSegmentState state = new UserSegmentState();

            UserSegmentDecider.Change change = Purchase(state);

            Assert.AreEqual(Default, change.PrevSegmentId);
            Assert.AreEqual(Default, state.PrevSegmentId);
        }

        [Test]
        public void OnPurchase_ThenCampaignArrives_NoChange()
        {
            UserSegmentState state = Booted();
            Purchase(state);

            Assert.IsFalse(Campaign(state, "Ads_US", Ads, "Ads").HasChange);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.AreEqual(Default, state.PrevSegmentId);
            Assert.AreEqual("Ads_US", state.LastCampaign);
        }
        #endregion
    }
}

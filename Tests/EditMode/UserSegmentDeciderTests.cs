using Horcrux.Runtime.Abstractions.Persistence;
using Horcrux.Runtime.Abstractions.UserSegmentation;
using NUnit.Framework;

namespace Horcrux.Tests
{
    /// <summary>Covers the rule table in UserSegmentation.md "Luật"; sample ids: default 1, IAP 2, purchased 3, ads 4.</summary>
    public sealed class UserSegmentDeciderTests
    {
        private const int Default = 1;
        private const int Iap = 2;
        private const int Purchased = 3;
        private const int Ads = 4;

        private static UserSegmentDecider.Change Boot(UserSegmentState state)
            => UserSegmentDecider.OnBoot(state, Default);

        private static UserSegmentDecider.Change Campaign(UserSegmentState state, string campaign, int matchedSegmentId,
            string matchedKeyword = null, bool isAmbiguous = false)
            => UserSegmentDecider.OnCampaign(state, campaign, matchedSegmentId, matchedKeyword, isAmbiguous, Default, Purchased);

        private static UserSegmentDecider.Change Purchase(UserSegmentState state)
            => UserSegmentDecider.OnPurchase(state, Purchased);

        private static UserSegmentDecider.Change ForcedCampaign(UserSegmentState state, string campaign,
            int matchedSegmentId, string matchedKeyword = null, bool isAmbiguous = false)
            => UserSegmentDecider.OnForcedCampaign(state, campaign, matchedSegmentId, matchedKeyword, isAmbiguous, Default);

        /// <summary>State after the first boot: default segment, session 1, decided by Initial.</summary>
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
            Assert.AreEqual(0, state.PrevSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.Initial, state.LastChangeReason);
            Assert.AreEqual(1, state.SessionCount);
        }

        [Test]
        public void OnBoot_LaterSessions_NoChangeReasonKept()
        {
            UserSegmentState state = Booted();

            for (int session = 2; session <= 5; session++)
                Assert.IsFalse(Boot(state).HasChange, $"session {session}");

            Assert.AreEqual(5, state.SessionCount);
            Assert.AreEqual(UserSegmentChangeReason.Initial, state.LastChangeReason);
        }

        [Test]
        public void OnBoot_Decided_NoChangeKeepsSegmentPrevAndReason()
        {
            UserSegmentState state = new UserSegmentState
            {
                CurrSegmentId = Ads, PrevSegmentId = Default,
                LastChangeReason = UserSegmentChangeReason.CampaignResolved, SessionCount = 3
            };

            Assert.IsFalse(Boot(state).HasChange);
            Assert.AreEqual(Ads, state.CurrSegmentId);
            Assert.AreEqual(Default, state.PrevSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, state.LastChangeReason);
        }
        #endregion

        #region AcceptsCampaign
        [TestCase(UserSegmentChangeReason.None, true)]
        [TestCase(UserSegmentChangeReason.Initial, true)]
        [TestCase(UserSegmentChangeReason.CampaignResolved, true)]
        [TestCase(UserSegmentChangeReason.Purchase, false)]
        [TestCase((UserSegmentChangeReason)99, false)]
        public void AcceptsCampaign_OnlyInitialInputReasons(UserSegmentChangeReason reason, bool expected)
        {
            // 99 stands for a behavior rule appended later: it must win over a campaign without touching this rule.
            UserSegmentState state = new UserSegmentState { CurrSegmentId = Default, LastChangeReason = reason };

            Assert.AreEqual(expected, UserSegmentDecider.AcceptsCampaign(state));
        }
        #endregion

        #region OnCampaign
        [Test]
        public void OnCampaign_InitialMatchesIap_MovesToIap()
        {
            UserSegmentState state = Booted();

            UserSegmentDecider.Change change = Campaign(state, "IAP_US", Iap, "IAP");

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Default, change.PrevSegmentId);
            Assert.AreEqual(Iap, change.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, change.Reason);
            Assert.AreEqual("IAP", change.MatchedKeyword);
            Assert.AreEqual(Iap, state.CurrSegmentId);
            Assert.AreEqual(Default, state.PrevSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, state.LastChangeReason);
            Assert.AreEqual("IAP_US", state.LastCampaign);
        }

        [TestCase("Organic_US")]
        [TestCase("")]
        public void OnCampaign_NoMatchAlreadyDefault_NoChangeReasonStaysInitial(string campaign)
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(Campaign(state, campaign, 0).HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.Initial, state.LastChangeReason);
            Assert.AreEqual(campaign, state.LastCampaign);
        }

        [Test]
        public void OnCampaign_MatchesPurchased_TreatedAsNoMatchStaysDefault()
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(Campaign(state, "Paid_US", Purchased, "Paid").HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
        }

        [Test]
        public void OnCampaign_AfterCampaign_StillInputChangesAgain()
        {
            UserSegmentState state = Booted();
            Campaign(state, "IAP_US", Iap, "IAP");

            Assert.IsTrue(Campaign(state, "Ads_US", Ads, "Ads").HasChange);
            Assert.AreEqual(Ads, state.CurrSegmentId);
            Assert.AreEqual(Iap, state.PrevSegmentId);
        }

        [Test]
        public void OnCampaign_AfterPurchase_NoChangeCampaignRecorded()
        {
            UserSegmentState state = Booted();
            Purchase(state);

            Assert.IsFalse(Campaign(state, "Ads_US", Ads, "Ads").HasChange);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.AreEqual(Default, state.PrevSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.Purchase, state.LastChangeReason);
            Assert.AreEqual("Ads_US", state.LastCampaign);
        }

        [Test]
        public void OnCampaign_SaveWithoutReason_TreatedAsInitialInput()
        {
            UserSegmentState state = new UserSegmentState { CurrSegmentId = Iap, SessionCount = 2 };

            Assert.IsTrue(Campaign(state, "Ads_US", Ads, "Ads").HasChange);
            Assert.AreEqual(Ads, state.CurrSegmentId);
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
        public void OnPurchase_FromDefault_PrevDefaultCurrPurchased()
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
            Assert.AreEqual(UserSegmentChangeReason.Purchase, state.LastChangeReason);
        }

        [Test]
        public void OnPurchase_AfterCampaign_PrevIsCampaignSegment()
        {
            UserSegmentState state = Booted();
            Campaign(state, "IAP_US", Iap, "IAP");

            Purchase(state);

            Assert.AreEqual(Iap, state.PrevSegmentId);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
        }

        [Test]
        public void OnPurchase_SecondPurchase_NoChangePrevKept()
        {
            UserSegmentState state = Booted();
            Campaign(state, "IAP_US", Iap, "IAP");
            Purchase(state);

            Assert.IsFalse(Purchase(state).HasChange);
            Assert.AreEqual(Iap, state.PrevSegmentId);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
        }

        [Test]
        public void OnPurchase_Undecided_PrevZero()
        {
            // Prev 0 is fine: the config lookup falls back to the default row.
            UserSegmentState state = new UserSegmentState();

            UserSegmentDecider.Change change = Purchase(state);

            Assert.AreEqual(0, change.PrevSegmentId);
            Assert.AreEqual(0, state.PrevSegmentId);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
        }
        #endregion

        #region OnForcedCampaign
        [Test]
        public void OnForcedCampaign_Initial_ChangesCampaignResolved()
        {
            UserSegmentState state = Booted();

            UserSegmentDecider.Change change = ForcedCampaign(state, "promo_Ads", Ads, "Ads", true);

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Default, change.PrevSegmentId);
            Assert.AreEqual(Ads, change.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, change.Reason);
            Assert.AreEqual("Ads", change.MatchedKeyword);
            Assert.IsTrue(change.IsAmbiguous);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, state.LastChangeReason);
            Assert.AreEqual("promo_Ads", state.LastCampaign);
        }

        [Test]
        public void OnForcedCampaign_AfterPurchase_LeavesPurchased()
        {
            UserSegmentState state = Booted();
            Purchase(state);

            UserSegmentDecider.Change change = ForcedCampaign(state, "promo_Ads", Ads, "Ads");

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Purchased, change.PrevSegmentId);
            Assert.AreEqual(Ads, state.CurrSegmentId);
            Assert.AreEqual(Purchased, state.PrevSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, state.LastChangeReason);
        }

        [Test]
        public void OnForcedCampaign_NoMatch_LandsOnDefault()
        {
            UserSegmentState state = Booted();
            ForcedCampaign(state, "promo_Ads", Ads, "Ads");

            UserSegmentDecider.Change change = ForcedCampaign(state, "unknown_camp", 0);

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.IsNull(change.MatchedKeyword);
            Assert.AreEqual("unknown_camp", state.LastCampaign);
        }

        [Test]
        public void OnForcedCampaign_SameSegment_NoChangeCampaignRecorded()
        {
            UserSegmentState state = Booted();
            ForcedCampaign(state, "promo_Ads", Ads, "Ads");

            Assert.IsFalse(ForcedCampaign(state, "other_Ads", Ads, "Ads").HasChange);
            Assert.AreEqual("other_Ads", state.LastCampaign);
        }
        #endregion

        #region WireFormat
        // Wire format goes through the entry the save uses, so the test sees the real serializer settings.
        private static UserSegmentState ReadSave(string payload)
        {
            PersistenceDataEntry<UserSegmentState> entry = new PersistenceDataEntry<UserSegmentState>();
            entry.ReadPayload(payload);
            return entry.Value;
        }

        [Test]
        public void WireFormat_Write_LastChangeReasonAsInt()
        {
            PersistenceDataEntry<UserSegmentState> entry = new PersistenceDataEntry<UserSegmentState>
            {
                Value = new UserSegmentState { LastChangeReason = UserSegmentChangeReason.Purchase }
            };

            string payload = entry.WritePayload();

            StringAssert.Contains("\"LastChangeReason\":3", payload);
            StringAssert.DoesNotContain("LockReason", payload);
        }

        [Test]
        public void WireFormat_OldSaveWithLockReason_ReadsNoReasonKeepsSegments()
        {
            // Saves from before LastChangeReason: the dropped key is ignored, the reason reads None (initial input).
            UserSegmentState state = ReadSave("{\"CurrSegmentId\":3,\"PrevSegmentId\":2,\"LockReason\":2}");

            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.AreEqual(Iap, state.PrevSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.None, state.LastChangeReason);
        }
        #endregion
    }
}

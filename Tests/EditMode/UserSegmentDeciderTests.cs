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
            => UserSegmentDecider.OnPurchase(state, Default, Purchased);

        /// <summary>State after the first boot: default segment, session 1, window open.</summary>
        private static UserSegmentState Booted()
        {
            UserSegmentState state = new UserSegmentState();
            Boot(state);
            return state;
        }

        /// <summary>Session 1 whose attribution poll ran out.</summary>
        private static UserSegmentState TimedOut()
        {
            UserSegmentState state = Booted();
            UserSegmentDecider.OnNoAttribution(state);
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
            Assert.AreEqual(UserSegmentLockReason.None, state.LockReason);
        }

        [Test]
        public void OnBoot_LaterSessions_NeverLockNoChange()
        {
            // Session 1 quit before the poll ran out: the state stays open, and no boot closes it.
            UserSegmentState state = Booted();

            for (int session = 2; session <= 5; session++)
            {
                Assert.IsFalse(Boot(state).HasChange, $"session {session}");
                Assert.IsFalse(state.IsCampaignLocked, $"session {session}");
            }

            Assert.AreEqual(5, state.SessionCount);
        }

        [Test]
        public void OnBoot_AlreadyLocked_StaysLockedKeepsSegmentAndReason()
        {
            UserSegmentState state = new UserSegmentState
            {
                CurrSegmentId = Ads, LockReason = UserSegmentLockReason.Attribution, SessionCount = 3
            };

            Assert.IsFalse(Boot(state).HasChange);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
            Assert.AreEqual(Ads, state.CurrSegmentId);
        }

        #endregion

        #region OnNoAttribution
        [Test]
        public void OnNoAttribution_Open_LocksWithoutChange()
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(UserSegmentDecider.OnNoAttribution(state).HasChange);
            Assert.AreEqual(UserSegmentLockReason.NoAttribution, state.LockReason);
            Assert.AreEqual(Default, state.CurrSegmentId);
        }

        [Test]
        public void OnNoAttribution_AfterAttribution_FirstReasonKept()
        {
            UserSegmentState state = Booted();
            Campaign(state, "IAP_US", Iap, "IAP");

            UserSegmentDecider.OnNoAttribution(state);

            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
            Assert.AreEqual(Iap, state.CurrSegmentId);
        }

        [Test]
        public void OnNoAttribution_AfterPurchase_StaysPurchasedFirstReasonKept()
        {
            UserSegmentState state = Booted();
            Purchase(state);

            UserSegmentDecider.OnNoAttribution(state);

            Assert.AreEqual(UserSegmentLockReason.Purchase, state.LockReason);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
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
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
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
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
            Assert.AreEqual(campaign, state.LastCampaign);
        }

        [Test]
        public void OnCampaign_MatchesPurchased_TreatedAsNoMatchStaysDefaultAndLocks()
        {
            UserSegmentState state = Booted();

            Assert.IsFalse(Campaign(state, "Paid_US", Purchased, "Paid").HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
        }

        [Test]
        public void OnCampaign_AfterNoAttribution_NoChangeLastCampaignWrittenReasonKept()
        {
            UserSegmentState state = TimedOut();

            Assert.IsFalse(Campaign(state, "IAP_US", Iap, "IAP").HasChange);
            Assert.AreEqual(Default, state.CurrSegmentId);
            Assert.AreEqual("IAP_US", state.LastCampaign);
            Assert.AreEqual(UserSegmentLockReason.NoAttribution, state.LockReason);
        }

        [Test]
        public void OnCampaign_PurchasedAndUnlocked_LocksWithoutChange()
        {
            UserSegmentState state = new UserSegmentState { CurrSegmentId = Purchased, PrevSegmentId = Default, SessionCount = 1 };

            Assert.IsFalse(Campaign(state, "IAP_US", Iap, "IAP").HasChange);
            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.IsTrue(state.IsCampaignLocked);
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
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
            Assert.AreEqual(UserSegmentLockReason.Purchase, state.LockReason);
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
            Assert.AreEqual(UserSegmentLockReason.Purchase, state.LockReason);
        }
        #endregion

        #region LockReason
        [Test]
        public void LockReason_CampaignThenPurchase_FirstReasonKept()
        {
            UserSegmentState state = Booted();
            Campaign(state, "Ads_US", Ads, "Ads");

            Purchase(state);

            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
        }

        [Test]
        public void LockReason_NoAttributionThenPurchase_FirstReasonKept()
        {
            UserSegmentState state = TimedOut();

            Purchase(state);

            Assert.AreEqual(Purchased, state.CurrSegmentId);
            Assert.AreEqual(UserSegmentLockReason.NoAttribution, state.LockReason);
        }
        #endregion

        #region OnForcedCampaign
        private static UserSegmentDecider.Change ForcedCampaign(UserSegmentState state, string campaign,
            int matchedSegmentId, string matchedKeyword = null, bool isAmbiguous = false)
            => UserSegmentDecider.OnForcedCampaign(state, campaign, matchedSegmentId, matchedKeyword, isAmbiguous, Default);

        [Test]
        public void OnForcedCampaign_Open_ChangesAndLocksAttribution()
        {
            UserSegmentState state = Booted();

            UserSegmentDecider.Change change = ForcedCampaign(state, "promo_Ads", Ads, "Ads", true);

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Default, change.PrevSegmentId);
            Assert.AreEqual(Ads, change.CurrSegmentId);
            Assert.AreEqual(UserSegmentChangeReason.CampaignResolved, change.Reason);
            Assert.AreEqual("Ads", change.MatchedKeyword);
            Assert.IsTrue(change.IsAmbiguous);
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
            Assert.AreEqual("promo_Ads", state.LastCampaign);
        }

        [Test]
        public void OnForcedCampaign_AfterAttributionLock_ChangesAgain()
        {
            UserSegmentState state = Booted();
            Campaign(state, "", 0);

            UserSegmentDecider.Change change = ForcedCampaign(state, "test_IAP", Iap, "IAP");

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Iap, state.CurrSegmentId);
            Assert.AreEqual(UserSegmentLockReason.Attribution, state.LockReason);
        }

        [Test]
        public void OnForcedCampaign_AfterNoAttribution_ChangesKeepsFirstLockReason()
        {
            UserSegmentState state = TimedOut();

            ForcedCampaign(state, "promo_Ads", Ads, "Ads");

            Assert.AreEqual(Ads, state.CurrSegmentId);
            Assert.AreEqual(UserSegmentLockReason.NoAttribution, state.LockReason);
        }

        [Test]
        public void OnForcedCampaign_FromPurchased_LeavesPurchased()
        {
            UserSegmentState state = Booted();
            Purchase(state);

            UserSegmentDecider.Change change = ForcedCampaign(state, "promo_Ads", Ads, "Ads");

            Assert.IsTrue(change.HasChange);
            Assert.AreEqual(Purchased, change.PrevSegmentId);
            Assert.AreEqual(Ads, state.CurrSegmentId);
            Assert.AreEqual(UserSegmentLockReason.Purchase, state.LockReason);
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
        public void WireFormat_Write_HasLockReasonNoIsCampaignLocked()
        {
            PersistenceDataEntry<UserSegmentState> entry = new PersistenceDataEntry<UserSegmentState>
            {
                Value = new UserSegmentState { LockReason = UserSegmentLockReason.Purchase }
            };

            string payload = entry.WritePayload();

            StringAssert.DoesNotContain("IsCampaignLocked", payload);
            StringAssert.Contains("\"LockReason\":2", payload);
        }

        [Test]
        public void WireFormat_Read_LockReasonDrivesIsCampaignLocked()
        {
            UserSegmentState state = ReadSave("{\"CurrSegmentId\":2,\"LockReason\":2}");

            Assert.AreEqual(UserSegmentLockReason.Purchase, state.LockReason);
            Assert.IsTrue(state.IsCampaignLocked);
        }

        [Test]
        public void WireFormat_OldSaveWithIsCampaignLocked_ReadsUnlocked()
        {
            // Saves written before LockReason existed reopen the window once; accepted while no store user has them.
            UserSegmentState state = ReadSave("{\"CurrSegmentId\":2,\"IsCampaignLocked\":true}");

            Assert.AreEqual(UserSegmentLockReason.None, state.LockReason);
            Assert.IsFalse(state.IsCampaignLocked);
        }
        #endregion
    }
}

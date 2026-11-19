using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class GStarCurrencyUnlockTests
    {
        GameObject runObject;
        RunScore run;
        bool? oldOverride;
        const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void SetUp()
        {
            oldOverride = GStarDemo.TestOverride;
            GStarDemo.TestOverride = true;
            StageEventRuntimeModifiers.ResetCoinModifiers();
            LobbyLoadoutData.Clear();
            runObject = new GameObject("GStar currency test");
            run = runObject.AddComponent<RunScore>();
            run.Data.rules = GStarScoreRules.Version;
            typeof(RunScore).GetProperty("Current").SetValue(null, run);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(runObject);
            LobbyLoadoutData.Clear();
            RelicSaveData.Unequip();
            StageEventRuntimeModifiers.ResetCoinModifiers();
            GStarDemo.TestOverride = oldOverride;
        }

        [Test]
        public void FieldGoldFundsMerchantWithoutAddingOrSpendingRankingPoints()
        {
            var entities = runObject.AddComponent<EntityManager>();
            var stats = runObject.AddComponent<StatsManager>();
            typeof(EntityManager).GetField("statsManager", Private).SetValue(entities, stats);
            entities.AwardCoinValue((int)CoinType.Gold5);
            entities.AwardCoinValue((int)CoinType.Silver2);
            Assert.AreEqual(7, stats.CoinsGained);
            stats.IncreaseCoinsGained(-5);
            Assert.AreEqual(2, stats.CoinsGained);
            Assert.AreEqual(0, run.Data.gold);
        }

        [Test]
        public void SilverRewardAndFreeSpendingNeverWriteTheRegularWallet()
        {
            int saved = ProgressPrefs.GetInt("LobbySilverCoins", 0);
            SilverWallet.Add(12);
            Assert.AreEqual(12, run.Data.silver);
            Assert.IsTrue(SilverWallet.CanSpend(900));
            Assert.IsTrue(SilverWallet.TrySpend(900));
            Assert.IsFalse(SilverWallet.TrySpend(-1));
            SilverWallet.Set(99999);
            Assert.AreEqual(saved, ProgressPrefs.GetInt("LobbySilverCoins", 0));
            Assert.AreEqual(12, run.Data.silver);
        }

        [Test]
        public void GoldRushMultiplierIsAppliedExactlyOnceToTheSpendableBalance()
        {
            var entities = runObject.AddComponent<EntityManager>();
            typeof(StageEventRuntimeModifiers).GetProperty("CoinValueMultiplier").SetValue(null, 3f);
            var stats = runObject.AddComponent<StatsManager>();
            typeof(EntityManager).GetField("statsManager", Private).SetValue(entities, stats);
            entities.AwardCoinValue((int)CoinType.Gold5);
            Assert.AreEqual(15, stats.CoinsGained);
            Assert.AreEqual(0, run.Data.gold);
            Assert.AreEqual(0, run.Data.silver);
        }

        [Test]
        public void PermittedUnlocksAreTemporaryAndAuthorExclusionsStayClosed()
        {
            string key = "LobbyUnlock.Character.GStarTestUnowned";
            bool hadKey = ProgressPrefs.HasKey(key);
            int previous = ProgressPrefs.GetInt(key, -1);
            Assert.IsTrue(LobbyUnlockSave.IsUnlocked("Character", "GStarTestUnowned"));
            LobbyUnlockSave.Unlock("Character", "GStarTestUnowned");
            Assert.AreEqual(hadKey, ProgressPrefs.HasKey(key));
            Assert.AreEqual(previous, ProgressPrefs.GetInt(key, -1));
            Assert.IsFalse(LobbyUnlockSave.IsUnlocked("Needle", "Honey"));
            Assert.IsFalse(LobbyUnlockSave.IsUnlocked("Needle", "WoodNeedle"));
            Assert.IsTrue(LobbyUnlockSave.IsUnlocked("Needle", "FireNeedle"));
            Assert.IsFalse(RelicSaveData.IsUnlocked("lotus_chips"));
            string savedRelic = ProgressPrefs.GetString("EquippedRelicId", "");
            RelicSaveData.Equip("beads");
            Assert.AreEqual("beads", RelicSaveData.GetEquippedRelicId());
            Assert.AreEqual(savedRelic, ProgressPrefs.GetString("EquippedRelicId", ""));
        }

        [Test]
        public void FreeStartingItemsRetainSlotLimitAndSurviveRetryUntilVisitorReset()
        {
            var catalog = Resources.LoadAll<MerchantItemBlueprint>("OctoberContent/Catalog");
            Assert.Greater(catalog.Length, 2);
            foreach (var retired in catalog.Where(i => ItemOfferPolicy.Retired(i.octoberId)))
                Assert.IsFalse(LobbyLoadoutData.TryBuyAndEquip(retired, retired.silverCost, out _));
            var allowed = catalog.Where(ItemOfferPolicy.CanBuyInLobby).Take(3).ToArray();
            Assert.AreEqual(3, allowed.Length);
            Assert.IsTrue(LobbyLoadoutData.TryBuyAndEquip(allowed[0], int.MaxValue, out _));
            Assert.IsTrue(LobbyLoadoutData.TryBuyAndEquip(allowed[1], int.MaxValue, out _));
            Assert.IsFalse(LobbyLoadoutData.TryBuyAndEquip(allowed[2], 0, out _));
            CollectionAssert.AreEqual(allowed.Take(2), LobbyLoadoutData.ConsumeSelectedCarryItems());
            Assert.AreEqual(2, LobbyLoadoutData.SelectedCarryItems.Count);
            LobbyLoadoutData.Clear();
            Assert.AreEqual(0, LobbyLoadoutData.SelectedCarryItems.Count);
        }

        [Test]
        public void GoldJarLeavesPhysicalPickupButSilverStillCreditsOnBreak()
        {
            var drop = CurrencyJarDrop.Create(null, Vector2.zero, false, 30, runObject.transform);
            Assert.NotNull(drop);
            Assert.AreEqual(30, drop.Amount);
            Assert.IsFalse(drop.Silver);
            Assert.IsNull(CurrencyJarDrop.Create(null, Vector2.zero, true, 10, runObject.transform));
            Assert.AreEqual(0, run.Data.gold);
            Assert.AreEqual(10, run.Data.silver);
        }
    }
}

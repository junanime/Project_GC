using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class ItemBalancePatchTests
    {
        const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static MerchantItemBlueprint[] Catalog()=>Resources.LoadAll<MerchantItemBlueprint>("OctoberContent/Catalog");

        [Test] public void RoleFiltersPartitionOffersWithoutLosingItems()
        {
            var offers=Catalog().Where(ItemOfferPolicy.CanOffer).ToArray();
            var combined=new List<MerchantItemBlueprint>();
            for(int i=1;i<=4;i++)
            {
                var group=ItemOfferPolicy.Filter(offers,(ItemOfferPolicy.Role)i);
                Assert.Greater(group.Length,0);combined.AddRange(group);
            }
            CollectionAssert.AreEquivalent(offers,combined);
            Assert.AreEqual(combined.Count,combined.Distinct().Count());
            Assert.AreEqual(58,ItemOfferPolicy.Filter(offers,ItemOfferPolicy.Role.All).Length);
            Assert.AreEqual(ItemOfferPolicy.Role.Survival,ItemOfferPolicy.RoleOf(63));
            Assert.AreEqual(ItemOfferPolicy.Role.Status,ItemOfferPolicy.RoleOf(65));
        }

        [Test] public void ClockWaitsAfterAcquisitionAndPreservesRemainingTime()
        {
            var go=new GameObject("clock cooldown test");
            try
            {
                var runtime=go.AddComponent<OctoberItemRuntime>();
                runtime.Give(Catalog().Single(i=>i.octoberId==29));
                Assert.IsFalse(runtime.Ready(29,OctoberItemBalance.ClockInterval));
                var saved=JsonUtility.FromJson<OctoberItemState>(JsonUtility.ToJson(runtime.Capture()));
                Assert.That(saved.cooldownRemaining[29],Is.EqualTo(20).Within(.1));
                saved.cooldownRemaining[29]=5;runtime.Restore(saved);
                runtime.SetDebugHour(23);
                Assert.IsFalse(runtime.Ready(29,20),"changing time of day must not skip the cooldown");
                saved.cooldownRemaining[29]=0;runtime.Restore(saved);
                Assert.IsTrue(runtime.Ready(29,20));Assert.IsFalse(runtime.Ready(29,20));
                Assert.That(runtime.Capture().cooldownRemaining[29],Is.EqualTo(20).Within(.1));
            }
            finally {Object.DestroyImmediate(go);}
        }

        [Test] public void OffersShrinkButAllLegacyIdsStillRestore()
        {
            var all=Catalog();Assert.AreEqual(66,all.Length);Assert.AreEqual(58,all.Count(ItemOfferPolicy.CanOffer));
            var go=new GameObject("Legacy items");
            try
            {
                var runtime=go.AddComponent<OctoberItemRuntime>();
                foreach(var item in all.Where(i=>ItemOfferPolicy.Retired(i.octoberId)))
                {
                    Assert.IsTrue(runtime.Give(item));
                    Assert.IsFalse(runtime.Give(item),"no duplicate legacy acquisition");
                }
                runtime.Restore(JsonUtility.FromJson<OctoberItemState>(JsonUtility.ToJson(runtime.Capture())));
                Assert.AreEqual(8,runtime.State.owned.Count);
                Assert.IsTrue(runtime.Has(53));
            }
            finally {Object.DestroyImmediate(go);}
        }
        [Test] public void RetiredLobbyPurchaseCannotSpendMoney()
        {
            int before=SilverWallet.Silver;
            foreach(var item in Catalog().Where(i=>ItemOfferPolicy.Retired(i.octoberId)))
                Assert.IsFalse(LobbyLoadoutData.TryBuyAndEquip(item,item.silverCost,out _));
            Assert.AreEqual(before,SilverWallet.Silver);
        }
        [Test] public void NormalRerollAndFallbackPoolsExcludeRetiredItems()
        {
            var go=new GameObject("Test merchant");var rng=UnityEngine.Random.state;
            try
            {
                var shop=go.AddComponent<MerchantUIManager>();
                typeof(MerchantUIManager).GetField("allAvailableItems",Private).SetValue(shop,Catalog().ToList());
                var generate=typeof(MerchantUIManager).GetMethod("GenerateNewShopItems",Private);
                for(int i=0;i<50;i++)
                {
                    var offers=(List<MerchantItemBlueprint>)generate.Invoke(shop,new object[]{i%2==0,i});
                    Assert.AreEqual(3,offers.Count);Assert.AreEqual(3,offers.Distinct().Count());
                    Assert.IsTrue(offers.All(ItemOfferPolicy.CanOffer));
                }
                var fallback=new List<MerchantItemBlueprint>();
                typeof(MerchantUIManager).GetMethod("FillRemainingItemsRandomly",Private).Invoke(shop,new object[]{fallback});
                Assert.AreEqual(3,fallback.Count);Assert.IsTrue(fallback.All(ItemOfferPolicy.CanOffer));
                typeof(MerchantUIManager).GetField("allAvailableItems",Private).SetValue(shop,Catalog().Where(i=>ItemOfferPolicy.Retired(i.octoberId)).ToList());
                Assert.AreEqual(0,((List<MerchantItemBlueprint>)generate.Invoke(shop,new object[]{false,0})).Count);
            }
            finally {UnityEngine.Random.state=rng;Object.DestroyImmediate(go);}
        }
        [Test] public void ResistanceCapsAtThirtyAndSurvivesTransfer()
        {
            var go=new GameObject("Resistance test");var enemy=ScriptableObject.CreateInstance<MonsterBlueprint>();
            var other=ScriptableObject.CreateInstance<MonsterBlueprint>();enemy.name="A";other.name="B";
            try
            {
                var runtime=go.AddComponent<OctoberItemRuntime>();runtime.State.owned.Add(63);
                runtime.Hurt(0,enemy);Assert.AreEqual(100,runtime.Incoming(100,enemy));
                runtime.Hurt(1,enemy);Assert.That(runtime.Incoming(100,enemy),Is.EqualTo(94).Within(.001));
                for(int i=0;i<20;i++)runtime.Hurt(1,enemy);
                Assert.That(runtime.Incoming(100,enemy),Is.EqualTo(70).Within(.001));
                Assert.AreEqual(100,runtime.Incoming(100,other));
                runtime.Restore(JsonUtility.FromJson<OctoberItemState>(JsonUtility.ToJson(runtime.Capture())));
                Assert.That(runtime.Incoming(100,enemy),Is.EqualTo(70).Within(.001));
            }
            finally {Object.DestroyImmediate(go);Object.DestroyImmediate(enemy);Object.DestroyImmediate(other);}
        }
        [Test] public void GuardianGrowthAndClockOverrideHaveBoundaries()
        {
            Assert.AreEqual(.5f,OctoberItemBalance.GuardianDamage(59));
            Assert.That(OctoberItemBalance.GuardianDamage(60),Is.EqualTo(.65f).Within(.001));
            Assert.That(OctoberItemBalance.GuardianDamage(1800),Is.EqualTo(.95f).Within(.001));
            var go=new GameObject("Clock test");
            try
            {
                var runtime=go.AddComponent<OctoberItemRuntime>();runtime.State.elapsed=120;
                runtime.SetDebugHour(23);Assert.AreEqual(23,runtime.DebugHour);
                runtime.SetDebugHour(-1);Assert.AreEqual(-1,runtime.DebugHour);
                Assert.AreEqual(2,typeof(OctoberItemRuntime).GetField("lastHour",Private).GetValue(runtime));
                Assert.AreEqual(120,runtime.State.elapsed,"override does not advance run state");
            }
            finally {Object.DestroyImmediate(go);}
        }
    }
}

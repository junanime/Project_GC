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

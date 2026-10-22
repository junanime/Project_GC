using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class RelicBalanceTests
    {
        [Test] public void AllCatalogEffectsPricesAndDescriptionsMatchSource()
        {
            var source=JsonUtility.FromJson<RelicCatalogInstaller.Catalog>(System.IO.File.ReadAllText("Tools/Relics/catalog.json"));
            var catalog=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            foreach(var entry in source.entries)
            {
                var relic=catalog.relics.Single(r=>r.relicId==entry.id);
                Assert.AreEqual(entry.value,relic.effectValue,entry.id);Assert.AreEqual(entry.price,relic.price,entry.id);
                Assert.AreEqual(entry.description,relic.Description,entry.id);
            }
            Assert.LessOrEqual(catalog.relics.Where(RelicSelectionPolicy.IsNewOffer).Max(r=>r.price),280);
        }
        [Test] public void AdditionalNeedleIsPeriodicAndReadsDoNotConsumeProgress()
        {
            var go=new GameObject("relic volley test");
            var catalog=Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
            try
            {
                var runtime=go.AddComponent<RelicRuntime>();
                Assert.AreEqual(0,runtime.NextNeedleVolleyBonus());Assert.AreEqual(0,runtime.State.needleVolleyCount);
                runtime.Equip(catalog.relics.Single(r=>r.relicId=="beads"));
                int extra=0;
                for(int i=1;i<=20;i++)
                {
                    for(int read=0;read<10;read++)Assert.AreEqual(1,runtime.Bonus(RelicBlueprint.RelicEffectType.ProjectileCount));
                    int next=runtime.NextNeedleVolleyBonus();Assert.AreEqual(i%5==0?1:0,next);extra+=next;
                }
                Assert.AreEqual(4,extra,"20 primary volleys yield four extra needles, not twenty");
                for(int i=0;i<4;i++)runtime.NextNeedleVolleyBonus();
                var snapshot=runtime.Capture();Assert.AreEqual(4,snapshot.needleVolleyCount);
                Assert.AreEqual(1,runtime.NextNeedleVolleyBonus());Assert.AreEqual(4,snapshot.needleVolleyCount,"capture is independent");
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test] public void BarrierAbsorbsOnlyItsCapacityAndRestartsDelay()
        {
            var go=new GameObject("barrier test");
            try
            {
                var runtime=go.AddComponent<RelicRuntime>();
                runtime.Equip(Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").relics.Single(r=>r.relicId=="bandage"));
                Assert.AreEqual(10,runtime.Absorb(25));Assert.AreEqual(0,runtime.State.barrier);
                Assert.AreEqual(10,runtime.State.rechargeDelay);
                runtime.State.rechargeDelay=2;runtime.Absorb(1);Assert.AreEqual(10,runtime.State.rechargeDelay);
            }
            finally{Object.DestroyImmediate(go);}
        }
    }
}

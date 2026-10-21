using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class RelicSelectionReviewTests
    {
        static RelicBlueprint[] Catalog=>Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").relics;
        [Test] public void EveryRelicHasReasonAndRetiredChoicesHaveStrongerEquivalent()
        {
            var all=Catalog;Assert.AreEqual(34,all.Length);
            Assert.AreEqual(26,all.Count(RelicSelectionPolicy.IsNewOffer));
            var source=JsonUtility.FromJson<RelicCatalogInstaller.Catalog>(System.IO.File.ReadAllText("Tools/Relics/catalog.json"));
            foreach(var relic in all)
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(relic.selectionReason),relic.relicId);
                Assert.AreEqual(source.entries.Single(e=>e.id==relic.relicId).selectionReason,relic.selectionReason);
                string replacement=RelicSelectionPolicy.Replacement(relic.relicId);if(replacement==null)continue;
                var kept=all.Single(r=>r.relicId==replacement);
                Assert.IsTrue(RelicSelectionPolicy.IsNewOffer(kept));
                if(relic.relicId=="relic_01"||relic.relicId=="citrus")
                {
                    foreach(var c in Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").characters)
                    {
                        float oldValue=relic.relicId=="relic_01"?relic.effectValue:c.movespeed*relic.effectValue;
                        float newValue=relic.relicId=="relic_01"?c.hp*kept.effectValue:kept.effectValue;
                        Assert.Greater(newValue,oldValue,c.name+": revisit retired choice if roster changes");
                    }
                }
                else
                {
                    Assert.AreEqual(kept.effectType,relic.effectType);
                    Assert.GreaterOrEqual(kept.effectValue,relic.effectValue);
                }
                Assert.AreEqual(relic.relicId=="lotus_chips",RelicSelectionPolicy.SameChoice(relic.relicId,replacement),"weaker owned relic must not block stronger supply reward");
            }
        }
        [Test] public void RetiredUnlocksAndEquipmentRemainVisibleWithoutOfferingThemToNewPlayers()
        {
            var all=Catalog;var keys=all.Select(r=>"RelicUnlocked_"+r.relicId).ToArray();
            var exists=keys.Select(PlayerPrefs.HasKey).ToArray();var values=keys.Select(k=>PlayerPrefs.GetInt(k)).ToArray();
            bool equippedExists=PlayerPrefs.HasKey("EquippedRelicId");string equipped=RelicSaveData.GetEquippedRelicId();
            try
            {
                foreach(var key in keys)PlayerPrefs.DeleteKey(key);PlayerPrefs.DeleteKey("EquippedRelicId");
                Assert.AreEqual(26,RelicSelectionPolicy.ForLobby(all).Length);
                foreach(var relic in all.Where(r=>!RelicSelectionPolicy.IsNewOffer(r)))
                {
                    RelicSaveData.Unlock(relic.relicId);RelicSaveData.Equip(relic.relicId);
                    Assert.Contains(relic,RelicSelectionPolicy.ForLobby(all));
                    Assert.IsTrue(RelicSaveData.IsEquipped(relic.relicId));
                    Assert.IsFalse(RelicSelectionPolicy.IsNewOffer(relic));
                }
                Assert.AreEqual(34,RelicSelectionPolicy.ForLobby(all).Length);
            }
            finally
            {
                for(int i=0;i<keys.Length;i++){if(exists[i])PlayerPrefs.SetInt(keys[i],values[i]);else PlayerPrefs.DeleteKey(keys[i]);}
                if(equippedExists)PlayerPrefs.SetString("EquippedRelicId",equipped);else PlayerPrefs.DeleteKey("EquippedRelicId");
                PlayerPrefs.Save();
            }
        }
    }
}

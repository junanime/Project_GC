using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using P=Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire.Tests.Editor
{
    public sealed class ApprovedBalanceTests
    {
        [Test] public void AssetAndDefaultsMatchApprovedRowsWithoutChangingOdds()
        {
            var asset=Resources.Load<Ver4AugmentBalance>("Ver4AugmentBalance");
            var defaults=ScriptableObject.CreateInstance<Ver4AugmentBalance>();
            try
            {
                float[][] expected={new[]{.08f,.05f,.10f,.05f,.05f,.06f,.04f,.05f,1},new[]{.11f,.09f,.17f,.075f,.08f,.10f,.07f,.08f,1},new[]{.15f,.14f,.23f,.11f,.12f,.15f,.10f,.12f,2},new[]{.23f,.19f,.38f,.17f,.18f,.23f,.15f,.18f,3},new[]{.34f,.23f,.57f,.25f,.27f,.35f,.23f,.27f,4}};
                var grades=new[]{AugmentUpgradeGrade.Common,AugmentUpgradeGrade.Rare,AugmentUpgradeGrade.Epic,AugmentUpgradeGrade.Legendary,AugmentUpgradeGrade.Supreme};
                for(int row=0;row<5;row++)for(int col=0;col<9;col++)
                {Assert.That(asset.NumericValue(grades[row],col),Is.EqualTo(expected[row][col]).Within(.00001));Assert.AreEqual(asset.NumericValue(grades[row],col),defaults.NumericValue(grades[row],col));}
                int[] distribution=new int[6];
                for(int i=0;i<10000;i++){Assert.IsTrue(asset.odds.TryRoll((i+.5)/10000,_=>true,out var grade));distribution[(int)grade]++;}
                CollectionAssert.AreEqual(new[]{3437,2749,1718,1400,516,180},distribution);
            }
            finally {UnityEngine.Object.DestroyImmediate(defaults);}
        }
        [Test] public void StackCapsAndPressurePreheatStayFinite()
        {
            for(int rank=0;rank<=3;rank++)
            {
                Assert.That(OriginalCombatRules.PierceSpeed(rank,10000),Is.EqualTo(new[]{1f,1.3f,1.6f,2f}[rank]).Within(.00001));
                Assert.That(OriginalCombatRules.PierceDamage(rank,10000),Is.EqualTo(1f+.3f*rank).Within(.00001));
                Assert.That(OriginalCombatRules.PressureBonus(rank,0,.1f,.5f),Is.EqualTo(.05f*rank).Within(.00001));
                Assert.AreEqual(.5f,OriginalCombatRules.PressureBonus(rank,1000,.1f,.5f));
                Assert.AreEqual(0,OriginalCombatRules.BacteriaHarvestChance(rank,3));
                Assert.LessOrEqual(OriginalCombatRules.BacteriaHarvestChance(rank,10000),1f);
                var progress=new OriginalAugmentProgress();
                foreach(var type in new[]{P.GutBacteriaNeedle,P.CorrosionNeedle,P.HungerNeedle,P.MarkNeedle})
                {progress.RegisterOwnedParent(type.ToString());for(int i=0;i<rank;i++)progress.TrySelect(type.ToString(),type==P.CorrosionNeedle||type==P.HungerNeedle?2:0);}
                var runtime=new SyringeSpecialRuntime{gutBacteriaMaxStacks=5,gutBacteriaStackDuration=6,corrosionMaxStacks=3,hungerMaxStacks=8,markDuration=4};
                new Ver4CombatSnapshot(progress).Modify(ref runtime);
                Assert.AreEqual(5+rank,runtime.gutBacteriaMaxStacks);Assert.AreEqual(6,runtime.gutBacteriaStackDuration);
                Assert.AreEqual(3+rank,runtime.corrosionMaxStacks);Assert.AreEqual(8+rank,runtime.hungerMaxStacks);
                Assert.That(runtime.markDuration,Is.EqualTo(4*(1+.2f*rank)).Within(.00001));
            }
            Assert.AreEqual(1,OriginalCombatRules.BacteriaHarvestChance(3,8));
            Assert.That(OriginalCombatRules.BacteriaHarvestChance(2,7),Is.EqualTo(.6f).Within(.00001));
        }
        [Test] public void RetiredDefinitionsRemainButCannotUnlockOrActivate()
        {
            Assert.AreEqual(19,Enum.GetValues(typeof(P)).Cast<P>().Count(StartingNeedleSelection.Available));
            foreach(var type in new[]{P.Honey,P.WoodNeedle})
            {
                string key="LobbyUnlock.Needle."+type;bool exists=PlayerPrefs.HasKey(key);int old=PlayerPrefs.GetInt(key);
                try
                {
                    PlayerPrefs.DeleteKey(key);LobbyUnlockSave.Unlock("Needle",type.ToString());Assert.IsFalse(PlayerPrefs.HasKey(key));
                    PlayerPrefs.SetInt(key,1);LobbyUnlockSave.Unlock("Needle",type.ToString());Assert.AreEqual(1,PlayerPrefs.GetInt(key));
                    var progress=new OriginalAugmentProgress();progress.RegisterOwnedParent(type.ToString());progress.TrySelect(type.ToString(),0);
                    var snapshot=new Ver4CombatSnapshot(progress);Assert.IsFalse(snapshot.Has(type));Assert.AreEqual(0,snapshot.Count(type,0));
                }
                finally {if(exists)PlayerPrefs.SetInt(key,old);else PlayerPrefs.DeleteKey(key);PlayerPrefs.Save();}
            }
        }
        public static void Run()
        {
            try
            {
                var tests=new ApprovedBalanceTests();tests.AssetAndDefaultsMatchApprovedRowsWithoutChangingOdds();tests.StackCapsAndPressurePreheatStayFinite();tests.RetiredDefinitionsRemainButCannotUnlockOrActivate();
                var foundation=new Ver4FoundationTests();foundation.EveryBasisPointResolvesToConfirmedGrade();foundation.EachPanelUsesItsOwnSampleAndInvalidPoolDoesNotFallbackToLegend();foundation.OriginalProgressIsParentLocalCappedAndPreviewDoesNotMutate();foundation.RestoreIsAtomicDeepCopiedAndIdempotent();foundation.TouchPointerOwnershipAndCancellationPreventCrossFingerRelease();
                var integration=new Ver4IntegrationTests();integration.AllTwentyOneParentsHaveThreeOriginalDefinitions();integration.OriginalSnapshotChangesOnlyRequestedParent();integration.NumericEpicAndOriginalAreDifferentBranches();integration.SerializedKeyboardBindingsUseInputSystem();
                new StageEventPresentationTests().SpecialMechanicsOnlyExplainTheCurrentSelection();
                new StageEventPresentationTests().EveryOriginalHasShortRepeatTextAndUnchangedProgress();
                Debug.Log("APPROVED_BALANCE_PASS 14 tests, including exhaustive grade boundaries and asset values");EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

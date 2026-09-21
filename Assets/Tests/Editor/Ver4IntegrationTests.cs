using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire.Tests.Editor
{
    public class Ver4IntegrationTests
    {
        public static void InstallAssets()
        {
            const string path="Assets/Junhan/Resources/Ver4AugmentBalance.asset";
            var balance=AssetDatabase.LoadAssetAtPath<Ver4AugmentBalance>(path);
            if(balance==null)
            {
                balance=ScriptableObject.CreateInstance<Ver4AugmentBalance>(); AssetDatabase.CreateAsset(balance,path);
            }
            string[] icons={"WoodNeedle","FireNeedle","IceNeedle","WindNeedle","VibrationNeedle"};
            for(int i=0;i<5;i++) balance.plannedIcons[i]=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Junhan/Art/AugmentIcons/Planned/"+icons[i]+".png");
            const string chestPath="Assets/Junhan/Resources/Ver4LegendaryChest.asset";
            var chest=AssetDatabase.LoadAssetAtPath<ChestBlueprint>(chestPath);
            if(chest==null)
            {
                chest=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ChestBlueprint>("Assets/Blueprints/Chests/Failsafe Chest.asset"));
                chest.name="Ver4 Legendary Chest";
                var visuals=AssetDatabase.LoadAssetAtPath<ChestBlueprint>("Assets/Blueprints/Chests/Boss Chest.asset");
                chest.closedChest=visuals.closedChest; chest.openingChest=visuals.openingChest; chest.openChest=visuals.openChest;
                chest.abilityChest=true; chest.legendaryAugmentChest=true;
                AssetDatabase.CreateAsset(chest,chestPath);
            }
            balance.legendaryChest=chest;
            EditorUtility.SetDirty(balance); AssetDatabase.SaveAssets();
        }

        [Test] public void AllTwentyOneParentsHaveThreeOriginalDefinitions()
        {
            Assert.AreEqual(21,Enum.GetValues(typeof(P)).Length);
            Assert.AreEqual(21,Ver4AugmentCatalog.ParentNames.Length);
            Assert.AreEqual(63,Ver4AugmentCatalog.OriginalNames.Length);
            Assert.AreEqual(63,Ver4AugmentCatalog.OriginalDescriptions.Length);
            Assert.AreEqual(0,(int)P.Poison); Assert.AreEqual(15,(int)P.GutBacteriaNeedle);
            var balance=Resources.Load<Ver4AugmentBalance>("Ver4AugmentBalance"); Assert.NotNull(balance);
            Assert.That(balance.plannedIcons.All(i=>i!=null)); Assert.NotNull(balance.legendaryChest);
            Assert.IsTrue(balance.legendaryChest.legendaryAugmentChest);
        }

        [Test] public void OriginalSnapshotChangesOnlyRequestedParent()
        {
            var progress=new OriginalAugmentProgress(); progress.RegisterOwnedParent("Poison"); progress.RegisterOwnedParent("Explosion");
            progress.TrySelect("Poison",0);
            var snapshot=new Ver4CombatSnapshot(progress);
            progress.TrySelect("Poison",0);
            var runtime=new SyringeSpecialRuntime {poisonTickDamage=4,poisonDuration=3,poisonTickInterval=.5f,explosionDamage=4,explosionRadius=1};
            snapshot.Modify(ref runtime);
            Assert.That(runtime.poisonTickDamage,Is.EqualTo(4.48f).Within(.0001));
            Assert.AreEqual(4,runtime.explosionDamage); Assert.AreEqual(3,runtime.poisonDuration);
            Assert.AreEqual(1,snapshot.Count(P.Poison,0));
            Assert.IsFalse(snapshot.Has(P.FireNeedle));
        }

        [Test] public void NumericEpicAndOriginalAreDifferentBranches()
        {
            var balance=ScriptableObject.CreateInstance<Ver4AugmentBalance>();
            try
            {
                float[] expected={.16f,.15f,.25f,.12f,.12f,.15f,.10f,.12f,2};
                for(int i=0;i<9;i++) Assert.AreEqual(expected[i],balance.NumericValue(AugmentUpgradeGrade.Epic,i));
                Assert.Throws<ArgumentException>(()=>balance.NumericValue(AugmentUpgradeGrade.Original,0));
                for(int i=0;i<9;i++)
                {
                    float previous=0;
                    foreach(var grade in new[]{AugmentUpgradeGrade.Common,AugmentUpgradeGrade.Rare,AugmentUpgradeGrade.Epic,AugmentUpgradeGrade.Legendary,AugmentUpgradeGrade.Supreme})
                    { float next=balance.NumericValue(grade,i); Assert.GreaterOrEqual(next,previous); previous=next; }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(balance); }
        }

        [Test] public void SerializedKeyboardBindingsUseInputSystem()
        {
            // EditMode has separate keyboard state buffers. Frame-press behavior
            // is exercised in PlayMode; here verify the serialized-key mapping.
            Assert.AreEqual(UnityEngine.InputSystem.Key.E,GameInput.ResolveKeyboardKey(KeyCode.E));
            Assert.AreEqual(UnityEngine.InputSystem.Key.UpArrow,GameInput.ResolveKeyboardKey(KeyCode.UpArrow));
            Assert.AreEqual(UnityEngine.InputSystem.Key.Escape,GameInput.ResolveKeyboardKey(KeyCode.Escape));
            Assert.AreEqual(UnityEngine.InputSystem.Key.Digit1,GameInput.ResolveKeyboardKey(KeyCode.Alpha1));
            Assert.AreEqual(UnityEngine.InputSystem.Key.LeftCtrl,GameInput.ResolveKeyboardKey(KeyCode.LeftControl));
            Assert.AreEqual(UnityEngine.InputSystem.Key.NumpadEnter,GameInput.ResolveKeyboardKey(KeyCode.KeypadEnter));
            Assert.AreEqual(UnityEngine.InputSystem.Key.None,GameInput.ResolveKeyboardKey(KeyCode.Mouse0));
            Assert.IsFalse(GameInput.GetKeyDown(KeyCode.None));
        }

        public static void Run()
        {
            try
            {
                InstallAssets();
                var tests=new Ver4IntegrationTests(); tests.AllTwentyOneParentsHaveThreeOriginalDefinitions();
                tests.OriginalSnapshotChangesOnlyRequestedParent(); tests.NumericEpicAndOriginalAreDifferentBranches();
                tests.SerializedKeyboardBindingsUseInputSystem();
                var foundation=new Ver4FoundationTests(); foundation.EveryBasisPointResolvesToConfirmedGrade();
                foundation.EachPanelUsesItsOwnSampleAndInvalidPoolDoesNotFallbackToLegend();
                foundation.OriginalProgressIsParentLocalCappedAndPreviewDoesNotMutate();
                foundation.RestoreIsAtomicDeepCopiedAndIdempotent(); foundation.TouchPointerOwnershipAndCancellationPreventCrossFingerRelease();
                var mappings=new SyringeAugmentMappingTests();
                mappings.LegendaryPrefabSerializedTypesMatchTheirCards();mappings.SpecialPrefabSerializedTypesMatchTheirCards();
                mappings.StartingSyringeHasNoForcedAugments();mappings.ConditionalSourceIconsMatchTheirSerializedEnum();
                mappings.LevelOneContainsEverySpecialAndLegendaryCard();mappings.SelectingEachLegendaryOnlyEnablesItsOwnState();
                mappings.SelectingEachSpecialOnlyEnablesItsOwnState();
                mappings.SelectionAndRerollRollTierPerSlotAndExcludePreviousCards();mappings.LifeBurnRuntimeBlocksHealingAttempts();
                Debug.Log("[Ver4] PASS 18 data/input/mapping regression tests. PlayMode is reported separately."); EditorApplication.Exit(0);
            }
            catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}

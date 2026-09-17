using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class AugmentSelectionTestAbility : Ability
    {
        protected override void Use() { }
        protected override void Upgrade() { }
    }

    public sealed class AugmentSelectionTestManager : AbilityManager
    {
        private readonly Queue<Ability.AugmentTier> tierRolls =
            new Queue<Ability.AugmentTier>();

        public int TierRollCount { get; private set; }

        public void SetTierRolls(params Ability.AugmentTier[] tiers)
        {
            tierRolls.Clear();
            TierRollCount = 0;

            foreach (Ability.AugmentTier tier in tiers)
            {
                tierRolls.Enqueue(tier);
            }
        }

        protected override Ability.AugmentTier RollTier()
        {
            TierRollCount++;
            return tierRolls.Count > 0
                ? tierRolls.Dequeue()
                : Ability.AugmentTier.General;
        }
    }

    public class SyringeAugmentMappingTests
    {
        private const string AbilityRoot = "Assets/Junhan/Prefabs/Abilities";

        private static readonly Dictionary<string, SyringeLegendaryAugmentAbility.LegendaryAugmentType>
            LegendaryPrefabs = new Dictionary<string, SyringeLegendaryAugmentAbility.LegendaryAugmentType>
            {
                { "Syringe Life Burn Augment.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.LifeBurn },
                { "Syringe Clone Culture Augment.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.CloneCulture },
                { "Hedgehog Needle Legendary Augment.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.HedgehogNeedle },
                { "HeavySnipe.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.HeavySnipe },
                { "CursorControl.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.CursorControl },
                { "NeuralBlock.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.NeuralBlock },
                { "PoisonContagion.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.PoisonContagion },
                { "OrganCompression.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.OrganCompression },
                { "SyringeGastricPeristalsisWave_Legendary.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.GastricPeristalsisWave },
                { "SyringeMucosalFortress_Legendary.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.MucosalFortress },
                { "HungrySpirit.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.HungrySpirit },
                { "NeedleShotgun.prefab", SyringeLegendaryAugmentAbility.LegendaryAugmentType.NeedleShotgun }
            };

        private static readonly Dictionary<string, SyringeSpecialAugmentAbility.SpecialAugmentType>
            SpecialPrefabs = new Dictionary<string, SyringeSpecialAugmentAbility.SpecialAugmentType>
            {
                { "Syringe Poison Augment.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.Poison },
                { "Syringe Explosion Augment.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.Explosion },
                { "Syringe Homing Augment.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.Homing },
                { "Syringe Pierce Augment.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.Pierce },
                { "Honey Needle Augment.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.Honey },
                { "Mosquito Needle Augment.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.Mosquito },
                { "ReturnNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.ReturnNeedle },
                { "AcupunctureFormation.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.AcupunctureFormation },
                { "SpecialAugment_FiberNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.FiberNeedle },
                { "SpecialAugment_CorrosionNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.CorrosionNeedle },
                { "SpecialAugment_PressureNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.PressureNeedle },
                { "SpecialAugment_MarkNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.MarkNeedle },
                { "SpecialAugment_BipolarNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.BipolarNeedle },
                { "SpecialAugment_DigestiveAcidSacNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.DigestiveAcidSacNeedle },
                { "SpecialAugment_HungerNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.HungerNeedle },
                { "SpecialAugment_GutBacteriaNeedle.prefab", SyringeSpecialAugmentAbility.SpecialAugmentType.GutBacteriaNeedle }
            };

        [Test]
        public void LegendaryPrefabSerializedTypesMatchTheirCards()
        {
            foreach (KeyValuePair<string, SyringeLegendaryAugmentAbility.LegendaryAugmentType> pair in LegendaryPrefabs)
            {
                string path = $"{AbilityRoot}/Legendary/{pair.Key}";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                SyringeLegendaryAugmentAbility ability = prefab.GetComponent<SyringeLegendaryAugmentAbility>();
                Assert.That(ability, Is.Not.Null, path);
                SerializedProperty type = new SerializedObject(ability).FindProperty("augmentType");
                Assert.That(type.enumValueIndex, Is.EqualTo((int)pair.Value), path);
            }
        }

        [Test]
        public void SpecialPrefabSerializedTypesMatchTheirCards()
        {
            foreach (KeyValuePair<string, SyringeSpecialAugmentAbility.SpecialAugmentType> pair in SpecialPrefabs)
            {
                string path = $"{AbilityRoot}/Rare/{pair.Key}";
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                SyringeSpecialAugmentAbility ability = prefab.GetComponent<SyringeSpecialAugmentAbility>();
                Assert.That(ability, Is.Not.Null, path);
                SerializedProperty type = new SerializedObject(ability).FindProperty("augmentType");
                Assert.That(type.enumValueIndex, Is.EqualTo((int)pair.Value), path);
            }
        }

        [Test]
        public void StartingSyringeHasNoForcedAugments()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{AbilityRoot}/Syringe Dart Ability.prefab");
            SyringeDartAbility syringe = prefab.GetComponent<SyringeDartAbility>();
            SerializedObject serialized = new SerializedObject(syringe);

            string[] enabledFields =
            {
                "poisonEnabled", "fiberNeedleEnabled", "corrosionNeedleEnabled",
                "pressureNeedleEnabled", "markNeedleEnabled", "bipolarNeedleEnabled",
                "digestiveAcidSacNeedleEnabled", "hungerNeedleEnabled", "gutBacteriaNeedleEnabled",
                "explosionEnabled", "homingEnabled", "pierceEnabled", "honeyEnabled",
                "mosquitoEnabled", "returnNeedleEnabled", "acupunctureFormationEnabled",
                "lifeBurnEnabled", "hungrySpiritEnabled", "needleShotgunEnabled",
                "cloneLegendaryTaken", "neuralBlockEnabled", "poisonContagionEnabled",
                "gastricPeristalsisWaveEnabled", "mucosalFortressEnabled",
                "organCompressionEnabled", "hedgehogNeedleEnabled", "heavySnipeEnabled",
                "cursorControlEnabled"
            };

            foreach (string field in enabledFields)
            {
                Assert.That(serialized.FindProperty(field).boolValue, Is.False, field);
            }
        }

        [Test]
        public void ConditionalSourceIconsMatchTheirSerializedEnum()
        {
            AssertSourceIconMappings<SyringeSpecialConditionalGeneralAugmentAbility>(
                $"{AbilityRoot}/Rare/Ability_Syringe_SpecialConditionalGeneral.prefab",
                "sourceIconEntries", "specialType", typeof(SyringeSpecialAugmentAbility), "augmentType");

            AssertSourceIconMappings<SyringeLegendaryConditionalSpecialAugmentAbility>(
                $"{AbilityRoot}/Legendary/Ability_Syringe_LegendaryConditionalSpecial.prefab",
                "sourceIconEntries", "legendaryType", typeof(SyringeLegendaryAugmentAbility), "augmentType");
        }

        [Test]
        public void LevelOneContainsEverySpecialAndLegendaryCard()
        {
            UnityEngine.Object blueprint = AssetDatabase.LoadMainAssetAtPath(
                "Assets/Blueprints/Levels/Level 1.asset");
            SerializedProperty prefabs = new SerializedObject(blueprint).FindProperty("abilityPrefabs");
            HashSet<string> names = new HashSet<string>();

            for (int i = 0; i < prefabs.arraySize; i++)
            {
                GameObject prefab = prefabs.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                if (prefab != null)
                {
                    names.Add(prefab.name + ".prefab");
                }
            }

            foreach (string name in SpecialPrefabs.Keys)
            {
                Assert.That(names, Contains.Item(name), name);
            }

            foreach (string name in LegendaryPrefabs.Keys)
            {
                Assert.That(names, Contains.Item(name), name);
            }
        }

        [Test]
        public void SelectingEachLegendaryOnlyEnablesItsOwnState()
        {
            foreach (SyringeLegendaryAugmentAbility.LegendaryAugmentType selected in
                     Enum.GetValues(typeof(SyringeLegendaryAugmentAbility.LegendaryAugmentType)))
            {
                GameObject root = new GameObject($"Legendary_{selected}");
                try
                {
                    AbilityManager manager = root.AddComponent<AbilityManager>();
                    SyringeDartAbility syringe = new GameObject("Syringe").AddComponent<SyringeDartAbility>();
                    syringe.transform.SetParent(root.transform);
                    SyringeLegendaryAugmentAbility card = new GameObject("Card").AddComponent<SyringeLegendaryAugmentAbility>();
                    card.transform.SetParent(root.transform);
                    SetPrivateField(card, "augmentType", selected);
                    card.Init(manager, null, null);
                    card.Select();

                    foreach (SyringeLegendaryAugmentAbility.LegendaryAugmentType candidate in
                             Enum.GetValues(typeof(SyringeLegendaryAugmentAbility.LegendaryAugmentType)))
                    {
                        Assert.That(ReadLegendaryState(syringe, candidate),
                            Is.EqualTo(candidate == selected), $"selected={selected}, state={candidate}");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void SelectingEachSpecialOnlyEnablesItsOwnState()
        {
            foreach (SyringeSpecialAugmentAbility.SpecialAugmentType selected in
                     Enum.GetValues(typeof(SyringeSpecialAugmentAbility.SpecialAugmentType)))
            {
                GameObject root = new GameObject($"Special_{selected}");
                try
                {
                    AbilityManager manager = root.AddComponent<AbilityManager>();
                    SyringeDartAbility syringe = new GameObject("Syringe").AddComponent<SyringeDartAbility>();
                    syringe.transform.SetParent(root.transform);
                    SyringeSpecialAugmentAbility card = new GameObject("Card").AddComponent<SyringeSpecialAugmentAbility>();
                    card.transform.SetParent(root.transform);
                    SetPrivateField(card, "augmentType", selected);
                    card.Init(manager, null, null);
                    card.Select();

                    foreach (SyringeSpecialAugmentAbility.SpecialAugmentType candidate in
                             Enum.GetValues(typeof(SyringeSpecialAugmentAbility.SpecialAugmentType)))
                    {
                        Assert.That(ReadSpecialState(syringe, candidate),
                            Is.EqualTo(candidate == selected), $"selected={selected}, state={candidate}");
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void SelectionAndRerollRollTierPerSlotAndExcludePreviousCards()
        {
            GameObject root = new GameObject("SelectionManager");

            try
            {
                AugmentSelectionTestManager manager =
                    root.AddComponent<AugmentSelectionTestManager>();
                SetPrivateField(manager, "selectionCount", 3);
                object newPool = CreateWeightedPool();
                object ownedPool = CreateWeightedPool();
                SetPrivateField(manager, "newAbilities", newPool);
                SetPrivateField(manager, "ownedAbilities", ownedPool);

                for (int tier = 0; tier < 3; tier++)
                {
                    for (int i = 0; i < 12; i++)
                    {
                        AugmentSelectionTestAbility ability =
                            new GameObject($"Tier{tier}_{i}").AddComponent<AugmentSelectionTestAbility>();
                        ability.transform.SetParent(root.transform);
                        SetAbilityTier(ability, (Ability.AugmentTier)tier);
                        AddToWeightedPool(newPool, ability);
                    }
                }

                manager.SetTierRolls(
                    Ability.AugmentTier.General,
                    Ability.AugmentTier.Special,
                    Ability.AugmentTier.Legendary);
                List<Ability> initial = manager.SelectAbilities();

                Assert.That(initial.ConvertAll(x => x.Tier), Is.EqualTo(new[]
                {
                    Ability.AugmentTier.General,
                    Ability.AugmentTier.Special,
                    Ability.AugmentTier.Legendary
                }));
                Assert.That(manager.TierRollCount, Is.EqualTo(3),
                    "Each panel must make its own tier roll.");
                manager.ReturnAbilities(initial);

                manager.SetTierRolls(
                    Ability.AugmentTier.Legendary,
                    Ability.AugmentTier.Special,
                    Ability.AugmentTier.General);
                List<Ability> previous = manager.SelectAbilities();
                manager.ReturnAbilities(previous);
                manager.SetTierRolls(
                    Ability.AugmentTier.Legendary,
                    Ability.AugmentTier.Special,
                    Ability.AugmentTier.General);
                List<Ability> rerolled = manager.SelectAbilities(previous);

                Assert.That(rerolled, Has.Count.EqualTo(3));
                Assert.That(rerolled.ConvertAll(x => x.Tier), Is.EqualTo(new[]
                {
                    Ability.AugmentTier.Legendary,
                    Ability.AugmentTier.Special,
                    Ability.AugmentTier.General
                }));
                Assert.That(manager.TierRollCount, Is.EqualTo(3),
                    "Reroll must make one tier roll per panel.");
                Assert.That(rerolled.Exists(previous.Contains), Is.False);
                manager.ReturnAbilities(rerolled);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void LifeBurnRuntimeBlocksHealingAttempts()
        {
            GameObject root = new GameObject("LifeBurnRuntime");

            try
            {
                LegendaryConditionalSpecialRuntime runtime =
                    root.AddComponent<LegendaryConditionalSpecialRuntime>();

                Assert.That(runtime.BlockHealingAndRegisterAttempt(), Is.False);
                runtime.SetLifeBurnActive();
                Assert.That(runtime.BlockHealingAndRegisterAttempt(), Is.True);

                runtime.lifeBurnBloodFlowBlock = true;
                Assert.That(runtime.BlockHealingAndRegisterAttempt(), Is.True);
                Assert.That(runtime.lifeBurnBloodFlowAttackSpeedStacks, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertSourceIconMappings<T>(string path, string listField,
            string sourceTypeField, Type sourceAbilityType, string sourceAbilityTypeField)
            where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            SerializedProperty entries = new SerializedObject(prefab.GetComponent<T>()).FindProperty(listField);

            for (int i = 0; i < entries.arraySize; i++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(i);
                int expectedType = entry.FindPropertyRelative(sourceTypeField).enumValueIndex;
                Component source = entry.FindPropertyRelative("sourceAbility")
                    .objectReferenceValue as Component;
                Assert.That(source, Is.Not.Null, $"{path} entry {i}");
                Assert.That(sourceAbilityType.IsInstanceOfType(source), Is.True,
                    $"{path} entry {i}");
                int actualType = new SerializedObject(source).FindProperty(sourceAbilityTypeField).enumValueIndex;
                Assert.That(actualType, Is.EqualTo(expectedType), $"{path} entry {i}");
            }
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            Type type = target.GetType();
            FieldInfo field = null;

            while (type != null && field == null)
            {
                field = type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                type = type.BaseType;
            }

            Assert.That(field, Is.Not.Null, name);
            field.SetValue(target, value);
        }

        private static object CreateWeightedPool()
        {
            Type type = typeof(AbilityManager).GetNestedType(
                "WeightedAbilities", BindingFlags.NonPublic);
            Assert.That(type, Is.Not.Null);
            return Activator.CreateInstance(type, true);
        }

        private static void AddToWeightedPool(object pool, Ability ability)
        {
            pool.GetType().GetMethod("Add", BindingFlags.Instance | BindingFlags.Public)
                .Invoke(pool, new object[] { ability });
        }

        private static void SetAbilityTier(Ability ability, Ability.AugmentTier tier)
        {
            SetPrivateField(ability, "augmentTier", tier);
            SetPrivateField(ability, "maxLevel", 1);
        }

        private static bool ReadLegendaryState(SyringeDartAbility syringe,
            SyringeLegendaryAugmentAbility.LegendaryAugmentType type)
        {
            switch (type)
            {
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.LifeBurn: return syringe.HasLifeBurnLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.CloneCulture: return syringe.HasCloneLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.HedgehogNeedle: return syringe.HasHedgehogNeedleLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.HeavySnipe: return syringe.HasHeavySnipeLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.CursorControl: return syringe.HasCursorControlLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.NeuralBlock: return syringe.HasNeuralBlockLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.PoisonContagion: return syringe.HasPoisonContagionLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.OrganCompression: return syringe.HasOrganCompressionLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.GastricPeristalsisWave: return syringe.HasGastricPeristalsisWaveAugment();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.MucosalFortress: return syringe.HasMucosalFortressAugment();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.HungrySpirit: return syringe.HasHungrySpiritLegendary();
                case SyringeLegendaryAugmentAbility.LegendaryAugmentType.NeedleShotgun: return syringe.HasNeedleShotgunLegendary();
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }

        private static bool ReadSpecialState(SyringeDartAbility syringe,
            SyringeSpecialAugmentAbility.SpecialAugmentType type)
        {
            switch (type)
            {
                case SyringeSpecialAugmentAbility.SpecialAugmentType.Poison: return syringe.HasPoisonAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.Explosion: return syringe.HasExplosionAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.Homing: return syringe.HasHomingAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.Pierce: return syringe.HasPierceAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.Honey: return syringe.HasHoneyAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.Mosquito: return syringe.HasMosquitoAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.ReturnNeedle: return syringe.HasReturnNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.AcupunctureFormation: return syringe.HasAcupunctureFormationAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.FiberNeedle: return syringe.HasFiberNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.CorrosionNeedle: return syringe.HasCorrosionNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.PressureNeedle: return syringe.HasPressureNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.MarkNeedle: return syringe.HasMarkNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.BipolarNeedle: return syringe.HasBipolarNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.DigestiveAcidSacNeedle: return syringe.HasDigestiveAcidSacNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.HungerNeedle: return syringe.HasHungerNeedleAugment();
                case SyringeSpecialAugmentAbility.SpecialAugmentType.GutBacteriaNeedle: return syringe.HasGutBacteriaNeedleAugment();
                default: throw new ArgumentOutOfRangeException(nameof(type), type, null);
            }
        }
    }

    public static class SyringeAugmentMappingTestRunner
    {
        public static void RunAll()
        {
            SyringeAugmentMappingTests tests = new SyringeAugmentMappingTests();
            Action[] cases =
            {
                tests.LegendaryPrefabSerializedTypesMatchTheirCards,
                tests.SpecialPrefabSerializedTypesMatchTheirCards,
                tests.StartingSyringeHasNoForcedAugments,
                tests.ConditionalSourceIconsMatchTheirSerializedEnum,
                tests.LevelOneContainsEverySpecialAndLegendaryCard,
                tests.SelectingEachLegendaryOnlyEnablesItsOwnState,
                tests.SelectingEachSpecialOnlyEnablesItsOwnState,
                tests.SelectionAndRerollRollTierPerSlotAndExcludePreviousCards,
                tests.LifeBurnRuntimeBlocksHealingAttempts
            };

            int failed = 0;

            foreach (Action test in cases)
            {
                try
                {
                    test();
                    Debug.Log($"[SyringeAugmentTests][PASS] {test.Method.Name}");
                }
                catch (Exception exception)
                {
                    failed++;
                    Debug.LogException(new Exception(
                        $"[SyringeAugmentTests][FAIL] {test.Method.Name}", exception));
                }
            }

            Debug.Log($"[SyringeAugmentTests] Completed | Total={cases.Length}, Failed={failed}");
            EditorApplication.Exit(failed == 0 ? 0 : 1);
        }
    }
}

using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class AbilityManager : MonoBehaviour
    {
        [Header("Augment Selection")]
        [SerializeField] private int selectionCount = 3;

        [Header("Base Tier Odds")]
        [SerializeField] private float baseGeneralChance = 55f;
        [SerializeField] private float baseSpecialChance = 35f;
        [SerializeField] private float baseLegendaryChance = 10f;

        [Header("Luck Scaling (per 1 Luck above 1)")]
        [SerializeField] private float specialChancePerLuck = 2f;
        [SerializeField] private float legendaryChancePerLuck = 1f;

        private LevelBlueprint levelBlueprint;
        private Character playerCharacter;
        private WeightedAbilities newAbilities;
        private WeightedAbilities ownedAbilities;
        private FastList<IUpgradeableValue> registeredUpgradeableValues;

        public int DamageUpgradeablesCount { get; set; } = 0;
        public int KnockbackUpgradeablesCount { get; set; } = 0;
        public int WeaponCooldownUpgradeablesCount { get; set; } = 0;
        public int RecoveryCooldownUpgradeablesCount { get; set; } = 0;
        public int AOEUpgradeablesCount { get; set; } = 0;
        public int ProjectileSpeedUpgradeablesCount { get; set; } = 0;
        public int ProjectileCountUpgradeablesCount { get; set; } = 0;
        public int RecoveryUpgradeablesCount { get; set; } = 0;
        public int RecoveryChanceUpgradeablesCount { get; set; } = 0;
        public int BleedDamageUpgradeablesCount { get; set; } = 0;
        public int BleedRateUpgradeablesCount { get; set; } = 0;
        public int BleedDurationUpgradeablesCount { get; set; } = 0;
        public int MovementSpeedUpgradeablesCount { get; set; } = 0;
        public int ArmorUpgradeablesCount { get; set; } = 0;
        public int FireRateUpgradeablesCount { get; set; } = 0;
        public int DurationUpgradeablesCount { get; set; } = 0;
        public int RotationSpeedUpgradeablesCount { get; set; } = 0;

        public void Init(LevelBlueprint levelBlueprint, EntityManager entityManager, Character playerCharacter, AbilityManager abilityManager)
        {
            this.levelBlueprint = levelBlueprint;
            this.playerCharacter = playerCharacter;

            registeredUpgradeableValues = new FastList<IUpgradeableValue>();

            ownedAbilities = new WeightedAbilities();
            foreach (GameObject abilityPrefab in playerCharacter.Blueprint.startingAbilities)
            {
                Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();
                ability.Init(abilityManager, entityManager, playerCharacter);
                ability.Select();
                ownedAbilities.Add(ability);
            }

            newAbilities = new WeightedAbilities();
            foreach (GameObject abilityPrefab in levelBlueprint.abilityPrefabs)
            {
                if (playerCharacter.Blueprint.startingAbilities.Contains(abilityPrefab))
                {
                    continue;
                }

                Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();
                ability.Init(abilityManager, entityManager, playerCharacter);
                newAbilities.Add(ability);
            }
        }

        public void RegisterUpgradeableValue(IUpgradeableValue upgradeableValue, bool inUse = false)
        {
            upgradeableValue.Register(this);
            registeredUpgradeableValues.Add(upgradeableValue);

            if (inUse)
            {
                upgradeableValue.RegisterInUse();
            }
        }

        // 원본 프로젝트의 FloatUpgradeAbility / IntUpgradeAbility 호출 방식과 맞춘 버전
        public void UpgradeValue<TUpgradeable, TValue>(TValue value)
            where TUpgradeable : UpgradeableValue<TValue>
        {
            foreach (IUpgradeableValue upgradeableValue in registeredUpgradeableValues)
            {
                if (upgradeableValue is TUpgradeable typedUpgradeableValue)
                {
                    typedUpgradeableValue.Upgrade(value);
                }
            }
        }

        public List<Ability> SelectAbilities()
        {
            List<Ability> selectedAbilities = new List<Ability>();

            WeightedAbilities availableOwnedAbilities = ExtractAvailableAbilities(ownedAbilities);
            WeightedAbilities availableNewAbilities = ExtractAvailableAbilities(newAbilities);

            for (int slotIndex = 0; slotIndex < selectionCount; slotIndex++)
            {
                Ability selectedAbility = PullAbilityForSlot(availableOwnedAbilities, availableNewAbilities);

                if (selectedAbility == null)
                {
                    break;
                }

                selectedAbilities.Add(selectedAbility);
            }

            foreach (Ability ability in availableNewAbilities)
            {
                newAbilities.Add(ability);
            }

            foreach (Ability ability in availableOwnedAbilities)
            {
                ownedAbilities.Add(ability);
            }

            return selectedAbilities;
        }

        public void ReturnAbilities(List<Ability> abilities)
        {
            foreach (Ability ability in abilities)
            {
                if (ability.Owned)
                {
                    ownedAbilities.Add(ability);
                }
                else
                {
                    newAbilities.Add(ability);
                }
            }
        }

        public void DestroyActiveAbilities()
        {
            foreach (Ability ability in ownedAbilities)
            {
                Destroy(ability.gameObject);
            }
        }

        public bool HasAvailableAbilities()
        {
            foreach (Ability ability in ownedAbilities)
            {
                if (ability.RequirementsMet())
                {
                    return true;
                }
            }

            foreach (Ability ability in newAbilities)
            {
                if (ability.RequirementsMet())
                {
                    return true;
                }
            }

            return false;
        }

        private WeightedAbilities ExtractAvailableAbilities(WeightedAbilities abilities)
        {
            WeightedAbilities availableAbilities = new WeightedAbilities();

            foreach (Ability ability in abilities)
            {
                if (ability.RequirementsMet())
                {
                    availableAbilities.Add(ability);
                }
            }

            foreach (Ability ability in availableAbilities)
            {
                abilities.Remove(ability);
            }

            return availableAbilities;
        }

        private Ability PullAbilityForSlot(WeightedAbilities availableOwnedAbilities, WeightedAbilities availableNewAbilities)
        {
            Ability.AugmentTier rolledTier = RollTier();

            foreach (Ability.AugmentTier tier in GetFallbackOrder(rolledTier))
            {
                Ability ability = PullRandomAbilityByTier(availableOwnedAbilities, availableNewAbilities, tier);
                if (ability != null)
                {
                    return ability;
                }
            }

            return PullRandomAnyAbility(availableOwnedAbilities, availableNewAbilities);
        }

        private Ability PullRandomAbilityByTier(
            WeightedAbilities availableOwnedAbilities,
            WeightedAbilities availableNewAbilities,
            Ability.AugmentTier tier)
        {
            List<Ability> candidates = new List<Ability>();

            foreach (Ability ability in availableOwnedAbilities)
            {
                if (ability.Tier == tier)
                {
                    candidates.Add(ability);
                }
            }

            foreach (Ability ability in availableNewAbilities)
            {
                if (ability.Tier == tier)
                {
                    candidates.Add(ability);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            Ability selected = candidates[Random.Range(0, candidates.Count)];

            if (selected.Owned)
            {
                availableOwnedAbilities.Remove(selected);
            }
            else
            {
                availableNewAbilities.Remove(selected);
            }

            return selected;
        }

        private Ability PullRandomAnyAbility(WeightedAbilities availableOwnedAbilities, WeightedAbilities availableNewAbilities)
        {
            List<Ability> candidates = new List<Ability>();

            foreach (Ability ability in availableOwnedAbilities)
            {
                candidates.Add(ability);
            }

            foreach (Ability ability in availableNewAbilities)
            {
                candidates.Add(ability);
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            Ability selected = candidates[Random.Range(0, candidates.Count)];

            if (selected.Owned)
            {
                availableOwnedAbilities.Remove(selected);
            }
            else
            {
                availableNewAbilities.Remove(selected);
            }

            return selected;
        }

        private Ability.AugmentTier RollTier()
        {
            float luckBonus = Mathf.Max(0f, playerCharacter.Luck - 1f);

            float generalChance = baseGeneralChance - ((specialChancePerLuck + legendaryChancePerLuck) * luckBonus);
            float specialChance = baseSpecialChance + (specialChancePerLuck * luckBonus);
            float legendaryChance = baseLegendaryChance + (legendaryChancePerLuck * luckBonus);

            generalChance = Mathf.Max(0f, generalChance);
            specialChance = Mathf.Max(0f, specialChance);
            legendaryChance = Mathf.Max(0f, legendaryChance);

            float total = generalChance + specialChance + legendaryChance;
            if (total <= 0f)
            {
                return Ability.AugmentTier.General;
            }

            float roll = Random.Range(0f, total);

            if (roll < generalChance)
            {
                return Ability.AugmentTier.General;
            }

            roll -= generalChance;

            if (roll < specialChance)
            {
                return Ability.AugmentTier.Special;
            }

            return Ability.AugmentTier.Legendary;
        }

        private IEnumerable<Ability.AugmentTier> GetFallbackOrder(Ability.AugmentTier rolledTier)
        {
            switch (rolledTier)
            {
                case Ability.AugmentTier.Legendary:
                    yield return Ability.AugmentTier.Legendary;
                    yield return Ability.AugmentTier.Special;
                    yield return Ability.AugmentTier.General;
                    break;

                case Ability.AugmentTier.Special:
                    yield return Ability.AugmentTier.Special;
                    yield return Ability.AugmentTier.General;
                    yield return Ability.AugmentTier.Legendary;
                    break;

                default:
                    yield return Ability.AugmentTier.General;
                    yield return Ability.AugmentTier.Special;
                    yield return Ability.AugmentTier.Legendary;
                    break;
            }
        }

        private class WeightedAbilities : IEnumerable<Ability>
        {
            private readonly FastList<Ability> abilities;

            public int Count => abilities.Count;

            public WeightedAbilities()
            {
                abilities = new FastList<Ability>();
            }

            public void Add(Ability ability)
            {
                abilities.Add(ability);
            }

            public void Remove(Ability ability)
            {
                abilities.Remove(ability);
            }

            public IEnumerator<Ability> GetEnumerator()
            {
                foreach (Ability ability in abilities)
                {
                    yield return ability;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }
    }
}
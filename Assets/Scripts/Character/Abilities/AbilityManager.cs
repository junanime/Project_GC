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
        [Header("Ver.4 Shared Needle Rewards")]
        [SerializeField] private Ver4AugmentBalance ver4Balance;
        public Ver4AugmentRuntime Ver4 { get; private set; }
        public bool LegendaryRewardContext { get; set; }

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
                if (abilityPrefab == null)
                {
                    continue;
                }

                Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();

                if (ability == null)
                {
                    Debug.LogWarning($"[AbilityManager] Starting Ability Prefab에 Ability 컴포넌트가 없습니다: {abilityPrefab.name}");
                    continue;
                }

                ability.Init(abilityManager, entityManager, playerCharacter);
                ability.Select();

                ownedAbilities.Add(ability);
            }

            newAbilities = new WeightedAbilities();

            foreach (GameObject abilityPrefab in levelBlueprint.abilityPrefabs)
            {
                if (abilityPrefab == null)
                {
                    continue;
                }

                if (playerCharacter.Blueprint.startingAbilities.Contains(abilityPrefab))
                {
                    continue;
                }

                Ability ability = Instantiate(abilityPrefab, transform).GetComponent<Ability>();

                if (ability == null)
                {
                    Debug.LogWarning($"[AbilityManager] LevelBlueprint Ability Prefab에 Ability 컴포넌트가 없습니다: {abilityPrefab.name}");
                    continue;
                }

                ability.Init(abilityManager, entityManager, playerCharacter);
                newAbilities.Add(ability);
            }
            if (GetComponentInChildren<SyringeDartAbility>(true) != null)
            {
                if (ver4Balance == null) ver4Balance = Resources.Load<Ver4AugmentBalance>("Ver4AugmentBalance");
                if (ver4Balance != null)
                {
                    for (int i=0; i<5; i++)
                    {
                        var obj=new GameObject("Ver4 Special " + ((SyringeSpecialAugmentAbility.SpecialAugmentType)(16+i)));
                        obj.transform.SetParent(transform,false);
                        var ability=obj.AddComponent<SyringeSpecialAugmentAbility>();
                        ability.ConfigureNewAugment((SyringeSpecialAugmentAbility.SpecialAugmentType)(16+i),ver4Balance.plannedIcons[i]);
                        ability.Init(abilityManager,entityManager,playerCharacter);
                        newAbilities.Add(ability);
                    }
                    var objState=new GameObject("Ver4 Upgrade State"); objState.transform.SetParent(transform,false);
                    Ver4=objState.AddComponent<Ver4AugmentRuntime>();
                    Ver4.Configure(ver4Balance,abilityManager,entityManager,playerCharacter);
                    ownedAbilities.Add(Ver4);
                }
            }
        }

        public bool AcquireVer4Ability(Ability ability)
        {
            if (ability==null || ability.Owned || !ability.RequirementsMet() || !newAbilities.Remove(ability)) return false;
            ability.Select();
            ownedAbilities.Add(ability);
            return true;
        }

        public void RegisterUpgradeableValue(IUpgradeableValue upgradeableValue, bool inUse = false)
        {
            if (upgradeableValue == null)
            {
                return;
            }

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
            return SelectAbilities(null);
        }

        /// <summary>
        /// 각 카드 슬롯마다 등급을 독립적으로 다시 굴려 증강을 선택합니다.
        /// rerollExcludedAbilities는 새로고침 직전 카드이며, 다른 후보가 충분한 동안
        /// 재등장하지 않습니다. 후보가 부족할 때만 빈 슬롯을 채우기 위해 재사용합니다.
        /// </summary>
        public List<Ability> SelectAbilities(IReadOnlyCollection<Ability> rerollExcludedAbilities)
        {
            if (Ver4 != null) return Ver4.CreateOffers(LegendaryRewardContext, selectionCount);
            List<Ability> selectedAbilities = new List<Ability>();

            WeightedAbilities availableOwnedAbilities = ExtractAvailableAbilities(ownedAbilities);
            WeightedAbilities availableNewAbilities = ExtractAvailableAbilities(newAbilities);
            WeightedAbilities excludedOwnedAbilities = new WeightedAbilities();
            WeightedAbilities excludedNewAbilities = new WeightedAbilities();

            ExtractRerollExcludedAbilities(
                availableOwnedAbilities,
                availableNewAbilities,
                rerollExcludedAbilities,
                excludedOwnedAbilities,
                excludedNewAbilities);

            for (int slotIndex = 0; slotIndex < selectionCount; slotIndex++)
            {
                // 슬롯마다 호출해야 세 패널의 등급이 하나의 추첨값으로 고정되지 않는다.
                Ability selectedAbility = PullAbilityForSlot(availableOwnedAbilities, availableNewAbilities);

                if (selectedAbility == null)
                {
                    break;
                }

                selectedAbilities.Add(selectedAbility);
            }

            // 이전 카드 제외 때문에 슬롯이 비었을 때만 해당 카드를 다시 후보로 허용한다.
            if (selectedAbilities.Count < selectionCount)
            {
                MoveAll(excludedOwnedAbilities, availableOwnedAbilities);
                MoveAll(excludedNewAbilities, availableNewAbilities);

                while (selectedAbilities.Count < selectionCount)
                {
                    Ability selectedAbility = PullAbilityForSlot(
                        availableOwnedAbilities,
                        availableNewAbilities);

                    if (selectedAbility == null)
                    {
                        break;
                    }

                    selectedAbilities.Add(selectedAbility);
                }
            }

            MoveAll(excludedOwnedAbilities, availableOwnedAbilities);
            MoveAll(excludedNewAbilities, availableNewAbilities);

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

        private static void ExtractRerollExcludedAbilities(
            WeightedAbilities availableOwnedAbilities,
            WeightedAbilities availableNewAbilities,
            IReadOnlyCollection<Ability> rerollExcludedAbilities,
            WeightedAbilities excludedOwnedAbilities,
            WeightedAbilities excludedNewAbilities)
        {
            if (rerollExcludedAbilities == null || rerollExcludedAbilities.Count == 0)
            {
                return;
            }

            foreach (Ability ability in rerollExcludedAbilities)
            {
                if (ability == null)
                {
                    continue;
                }

                WeightedAbilities source = ability.Owned
                    ? availableOwnedAbilities
                    : availableNewAbilities;
                WeightedAbilities destination = ability.Owned
                    ? excludedOwnedAbilities
                    : excludedNewAbilities;

                if (source.Remove(ability))
                {
                    destination.Add(ability);
                }
            }
        }

        private static void MoveAll(WeightedAbilities source, WeightedAbilities destination)
        {
            if (source == null || destination == null)
            {
                return;
            }

            List<Ability> abilitiesToMove = new List<Ability>();

            foreach (Ability ability in source)
            {
                abilitiesToMove.Add(ability);
            }

            foreach (Ability ability in abilitiesToMove)
            {
                source.Remove(ability);
                destination.Add(ability);
            }
        }

        public void ReturnAbilities(List<Ability> abilities)
        {
            if (abilities == null)
            {
                return;
            }

            foreach (Ability ability in abilities)
            {
                if (ability == null)
                {
                    continue;
                }
                if (ability is Ver4AugmentOffer)
                {
                    Destroy(ability.gameObject);
                    continue;
                }

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
                if (ability != null)
                {
                    Destroy(ability.gameObject);
                }
            }
        }

        public bool HasAvailableAbilities()
        {
            if (Ver4 != null)
            {
                if (!LegendaryRewardContext) return true;
                return GetComponentsInChildren<SyringeLegendaryAugmentAbility>(true).Any(a=>!a.Owned && a.RequirementsMet());
            }
            foreach (Ability ability in ownedAbilities)
            {
                if (ability == null)
                {
                    continue;
                }

                if (!ability.CanAppearAsOwnedUpgrade)
                {
                    continue;
                }

                if (ability.RequirementsMet())
                {
                    return true;
                }
            }

            foreach (Ability ability in newAbilities)
            {
                if (ability == null)
                {
                    continue;
                }

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
                if (ability == null)
                {
                    continue;
                }

                if (ability.Owned && !ability.CanAppearAsOwnedUpgrade)
                {
                    continue;
                }

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
                if (ability != null && ability.Tier == tier)
                {
                    candidates.Add(ability);
                }
            }

            foreach (Ability ability in availableNewAbilities)
            {
                if (ability != null && ability.Tier == tier)
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
                if (ability != null)
                {
                    candidates.Add(ability);
                }
            }

            foreach (Ability ability in availableNewAbilities)
            {
                if (ability != null)
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

        protected virtual Ability.AugmentTier RollTier()
        {
            float playerLuck = playerCharacter != null ? playerCharacter.Luck : 1f;
            float luckBonus = Mathf.Max(0f, playerLuck - 1f);

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
        /// <summary>
        /// 현재 소유 중인 Ability의 종류, 레벨, 조건부 증강 ID를 저장합니다.
        /// </summary>
        public List<RunSceneAbilitySnapshot>
            CaptureRunSceneAbilities()
        {
            List<RunSceneAbilitySnapshot> snapshots =
                new List<RunSceneAbilitySnapshot>();

            if (ownedAbilities == null)
            {
                return snapshots;
            }

            foreach (Ability ability in ownedAbilities)
            {
                if (ability == null ||
                    !ability.Owned)
                {
                    continue;
                }

                RunSceneAbilitySnapshot snapshot =
                    new RunSceneAbilitySnapshot
                    {
                        AbilityTypeName =
                            GetRunSceneAbilityTypeName(
                                ability),

                        AbilityObjectName =
                            GetRunSceneAbilityObjectName(
                                ability),

                        Level =
                            ability.Level
                    };

                if (ability is IRunSceneConditionalAugmentState conditionalState)
                {
                    snapshot.ConditionalAugmentIds =
                        conditionalState.CaptureRunSceneConditionalAugmentIds();
                }

                snapshots.Add(snapshot);
            }

            return snapshots;
        }

        /// <summary>
        /// 새 씬에서 AbilityManager.Init 완료 후
        /// 이전 씬의 소유 Ability와 레벨을 복원합니다.
        /// </summary>
        public int RestoreRunSceneAbilities(
            List<RunSceneAbilitySnapshot> snapshots)
        {
            if (snapshots == null ||
                snapshots.Count == 0)
            {
                return 0;
            }

            if (ownedAbilities == null ||
                newAbilities == null)
            {
                Debug.LogWarning(
                    "[RunSceneTransfer][AbilityManager] " +
                    "AbilityManager.Init 이전이라 복원할 수 없습니다.",
                    this);

                return 0;
            }

            int restoredCount = 0;

            // 부모 특수/전설 증강을 먼저 복원해야 조건부 증강의 보유 조건이
            // 새 씬에서도 정확히 성립한다.
            for (int restorePass = 0; restorePass < 2; restorePass++)
            {
                for (int i = 0;
                     i < snapshots.Count;
                     i++)
                {
                    RunSceneAbilitySnapshot snapshot =
                        snapshots[i];

                    if (snapshot == null)
                    {
                        continue;
                    }

                    Ability ability =
                        FindRunSceneAbility(
                            ownedAbilities,
                            snapshot);

                    bool cameFromNewAbilities =
                        false;

                    if (ability == null)
                    {
                        ability =
                            FindRunSceneAbility(
                                newAbilities,
                                snapshot);

                        cameFromNewAbilities =
                            ability != null;
                    }

                    if (ability == null)
                    {
                        if (restorePass == 0)
                        {
                            Debug.LogWarning(
                                $"[RunSceneTransfer][AbilityManager] " +
                                $"복원할 Ability를 현재 LevelBlueprint에서 찾지 못했습니다. " +
                                $"Type={snapshot.AbilityTypeName}, " +
                                $"Object={snapshot.AbilityObjectName}",
                                this);
                        }

                        continue;
                    }

                    bool isConditional =
                        ability is IRunSceneConditionalAugmentState;

                    if ((restorePass == 0 && isConditional) ||
                        (restorePass == 1 && !isConditional))
                    {
                        continue;
                    }

                    if (cameFromNewAbilities)
                    {
                        newAbilities.Remove(
                            ability);
                    }

                    int targetLevel =
                        Mathf.Max(
                            1,
                            snapshot.Level);

                    bool restoreFailed =
                        false;

                    if (ability is IRunSceneConditionalAugmentState conditionalState)
                    {
                        restoreFailed =
                            !conditionalState.RestoreRunSceneConditionalAugments(
                                snapshot.ConditionalAugmentIds ??
                                new List<string>());
                    }
                    else
                    {
                        while (ability.Level <
                               targetLevel)
                        {
                            if (!ability.RequirementsMet())
                            {
                                Debug.LogWarning(
                                    $"[RunSceneTransfer][AbilityManager] " +
                                    $"RequirementsMet=false로 Ability 복원을 중단했습니다. " +
                                    $"Ability={ability.gameObject.name}, " +
                                    $"Current={ability.Level}, " +
                                    $"Target={targetLevel}",
                                    ability);

                                restoreFailed =
                                    true;

                                break;
                            }

                            ability.Select();
                        }
                    }

                    if (cameFromNewAbilities)
                    {
                        if (ability.Owned)
                        {
                            ownedAbilities.Add(
                                ability);
                        }
                        else
                        {
                            newAbilities.Add(
                                ability);
                        }
                    }

                    if (!restoreFailed &&
                        ability.Level >= targetLevel)
                    {
                        restoredCount++;
                    }
                }
            }

            Debug.Log(
                $"[RunSceneTransfer][AbilityManager] " +
                $"Ability 복원 완료 | " +
                $"Requested={snapshots.Count}, " +
                $"Restored={restoredCount}",
                this);

            return restoredCount;
        }

        private Ability FindRunSceneAbility(
            WeightedAbilities abilities,
            RunSceneAbilitySnapshot snapshot)
        {
            if (abilities == null ||
                snapshot == null)
            {
                return null;
            }

            foreach (Ability ability in abilities)
            {
                if (ability == null)
                {
                    continue;
                }

                string typeName =
                    GetRunSceneAbilityTypeName(
                        ability);

                string objectName =
                    GetRunSceneAbilityObjectName(
                        ability);

                if (typeName ==
                        snapshot.AbilityTypeName &&
                    objectName ==
                        snapshot.AbilityObjectName)
                {
                    return ability;
                }
            }

            return null;
        }

        private static string
            GetRunSceneAbilityTypeName(
                Ability ability)
        {
            if (ability == null)
            {
                return string.Empty;
            }

            string fullName =
                ability.GetType().FullName;

            return !string.IsNullOrEmpty(fullName)
                ? fullName
                : ability.GetType().Name;
        }

        private static string
            GetRunSceneAbilityObjectName(
                Ability ability)
        {
            if (ability == null ||
                ability.gameObject == null)
            {
                return string.Empty;
            }

            string objectName =
                ability.gameObject.name;

            const string cloneSuffix =
                "(Clone)";

            if (objectName.EndsWith(
                    cloneSuffix))
            {
                objectName =
                    objectName.Substring(
                        0,
                        objectName.Length -
                        cloneSuffix.Length);
            }

            return objectName.Trim();
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
                if (ability == null)
                {
                    return;
                }

                abilities.Add(ability);
            }

            public bool Remove(Ability ability)
            {
                if (ability == null)
                {
                    return false;
                }

                int previousCount = abilities.Count;
                abilities.Remove(ability);
                return abilities.Count < previousCount;
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

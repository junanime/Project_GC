using System.Reflection;
using UnityEngine;

namespace Vampire
{
    public class SyringeLegendaryAugmentAbility : Ability
    {
        public enum LegendaryAugmentType
        {
            LifeBurn,
            CloneCulture,

            // 전설 증강: 고슴도침
            HedgehogNeedle,

            // 전설 증강: 대물침
            HeavySnipe,

            // 전설 증강: 이기어침
            CursorControl,

            // 전설 증강: 신경차단
            NeuralBlock,

            // 전설 증강: 독 전염
            PoisonContagion,

            // 전설 증강: 장기압착
            OrganCompression,

            // 전설 증강: 위산 연동파
            // SyringeDartAbility 쪽 실제 메서드는 현재 EnableGastricPeristalsisWaveAugment() 이름으로 존재한다.
            GastricPeristalsisWave,

            // 전설 증강: 점막 요새
            // SyringeDartAbility 쪽 실제 메서드는 현재 EnableMucosalFortressAugment() 이름으로 존재한다.
            MucosalFortress,

            HungrySpirit,

            NeedleShotgun
        }

        [Header("Legendary Augment")]
        [Tooltip("이 Ability가 적용할 전설증강 종류입니다.")]
        [SerializeField] private LegendaryAugmentType augmentType;

        [Header("Debug")]
        [Tooltip("전설증강 적용/조건 검사 로그를 출력할지 여부입니다.")]
        [SerializeField] private bool debugLog = false;

        private SyringeDartAbility syringeDartAbility;
        private CharacterBlueprint originalBlueprintAsset;
        private CharacterBlueprint runtimeClonedBlueprint;

        public override void Init(
            AbilityManager abilityManager,
            EntityManager entityManager,
            Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            maxLevel = 1;

            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null)
            {
                Debug.LogError(
                    "[SyringeLegendaryAugmentAbility] SyringeDartAbility를 찾지 못했습니다.\n" +
                    "AbilityManager 아래에 실제 시작 침 능력이 있는지 확인하세요.",
                    this);
            }

            if (playerCharacter != null)
            {
                originalBlueprintAsset = playerCharacter.Blueprint;
            }
        }

        protected override void Use()
        {
            base.Use();

            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null)
            {
                Debug.LogError(
                    $"[SyringeLegendaryAugmentAbility] {augmentType} 적용 실패: SyringeDartAbility가 없습니다.",
                    this);
                return;
            }

            switch (augmentType)
            {
                case LegendaryAugmentType.LifeBurn:
                    ApplyLifeBurnLegendary();
                    break;

                case LegendaryAugmentType.CloneCulture:
                    ApplyCloneLegendary();
                    break;

                case LegendaryAugmentType.HedgehogNeedle:
                    syringeDartAbility.EnableHedgehogNeedleLegendary();
                    break;

                case LegendaryAugmentType.HeavySnipe:
                    syringeDartAbility.EnableHeavySnipeLegendary();
                    break;

                case LegendaryAugmentType.CursorControl:
                    syringeDartAbility.EnableCursorControlLegendary();
                    break;

                case LegendaryAugmentType.NeuralBlock:
                    syringeDartAbility.EnableNeuralBlockLegendary();
                    break;

                case LegendaryAugmentType.PoisonContagion:
                    syringeDartAbility.EnablePoisonContagionLegendary();
                    break;

                case LegendaryAugmentType.OrganCompression:
                    syringeDartAbility.EnableOrganCompressionLegendary();
                    break;

                case LegendaryAugmentType.GastricPeristalsisWave:
                    // 현재 SyringeDartAbility에는 Legendary 이름 메서드가 아니라 Augment 이름 메서드가 존재한다.
                    // 전설증강 카드에서 선택되지만, 실제 런타임 활성화는 기존 메서드로 연결한다.
                    syringeDartAbility.EnableGastricPeristalsisWaveAugment();
                    break;

                case LegendaryAugmentType.MucosalFortress:
                    // 현재 SyringeDartAbility에는 Legendary 이름 메서드가 아니라 Augment 이름 메서드가 존재한다.
                    // 전설증강 카드에서 선택되지만, 실제 런타임 활성화는 기존 메서드로 연결한다.
                    syringeDartAbility.EnableMucosalFortressAugment();
                    break;
                case LegendaryAugmentType.HungrySpirit:
                    syringeDartAbility.EnableHungrySpiritLegendary();
                    break;

                case LegendaryAugmentType.NeedleShotgun:
                    syringeDartAbility.EnableNeedleShotgunLegendary();
                    break;
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[SyringeLegendaryAugmentAbility] 전설증강 적용 완료 | " +
                    $"Type={augmentType} | Target={syringeDartAbility.name}",
                    syringeDartAbility);
            }
        }

        public override bool RequirementsMet()
        {
            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        $"[SyringeLegendaryAugmentAbility] {augmentType} 등장 불가: SyringeDartAbility 참조 없음",
                        this);
                }

                return false;
            }

            bool baseRequirement = base.RequirementsMet();
            bool result = false;

            switch (augmentType)
            {
                case LegendaryAugmentType.LifeBurn:
                    result = !syringeDartAbility.HasLifeBurnLegendary() && baseRequirement;
                    break;

                case LegendaryAugmentType.CloneCulture:
                    result = !syringeDartAbility.HasCloneLegendary() && baseRequirement;
                    break;

                case LegendaryAugmentType.HedgehogNeedle:
                    result = !syringeDartAbility.HasHedgehogNeedleLegendary() && baseRequirement;
                    break;

                case LegendaryAugmentType.HeavySnipe:
                    result =
                        !syringeDartAbility.HasHeavySnipeLegendary() &&
                        !syringeDartAbility.HasNeedleShotgunLegendary() &&
                        baseRequirement;
                    break;

                case LegendaryAugmentType.CursorControl:
                    result =
                        !syringeDartAbility.HasCursorControlLegendary() &&
                        !syringeDartAbility.HasNeedleShotgunLegendary() &&
                        baseRequirement;
                    break;
                case LegendaryAugmentType.NeuralBlock:
                    result = !syringeDartAbility.HasNeuralBlockLegendary() && baseRequirement;
                    break;

                case LegendaryAugmentType.PoisonContagion:
                    result =
                        syringeDartAbility.HasPoisonAugment() &&
                        !syringeDartAbility.HasPoisonContagionLegendary() &&
                        baseRequirement;
                    break;

                case LegendaryAugmentType.OrganCompression:
                    result = !syringeDartAbility.HasOrganCompressionLegendary() && baseRequirement;
                    break;

                case LegendaryAugmentType.GastricPeristalsisWave:
                    // 현재 SyringeDartAbility에는 HasGastricPeristalsisWaveLegendary()가 없다.
                    // 그래서 기존에 존재하는 HasGastricPeristalsisWaveAugment()를 사용한다.
                    result = !syringeDartAbility.HasGastricPeristalsisWaveAugment() && baseRequirement;
                    break;

                case LegendaryAugmentType.MucosalFortress:
                    // 현재 SyringeDartAbility에는 HasMucosalFortressLegendary()가 없다.
                    // 그래서 기존에 존재하는 HasMucosalFortressAugment()를 사용한다.
                    result = !syringeDartAbility.HasMucosalFortressAugment() && baseRequirement;
                    break;
                case LegendaryAugmentType.HungrySpirit:
                    result = !syringeDartAbility.HasHungrySpiritLegendary() && baseRequirement;
                    break;

                case LegendaryAugmentType.NeedleShotgun:
                    result =
                        !syringeDartAbility.HasNeedleShotgunLegendary() &&
                        !syringeDartAbility.HasHeavySnipeLegendary() &&
                        !syringeDartAbility.HasCursorControlLegendary() &&
                        baseRequirement;
                    break;
                default:
                    result = false;
                    break;
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[SyringeLegendaryAugmentAbility] 등장 조건 검사 | " +
                    $"Type={augmentType} | Result={result} | Base={baseRequirement} | " +
                    $"Syringe={syringeDartAbility.name}",
                    this);
            }

            return result;
        }

        private void RefreshSyringeDartAbilityReference()
        {
            if (abilityManager == null)
            {
                return;
            }

            SyringeDartAbility resolvedAbility = SyringeAbilityResolver.FindOwnedOrFirst(abilityManager);

            if (resolvedAbility != null)
            {
                syringeDartAbility = resolvedAbility;
                return;
            }

            if (syringeDartAbility == null)
            {
                syringeDartAbility = abilityManager.GetComponentInChildren<SyringeDartAbility>(true);
            }
        }

        private void ApplyLifeBurnLegendary()
        {
            syringeDartAbility.EnableLifeBurnLegendary();

            if (playerCharacter == null)
            {
                return;
            }

            EnsureRuntimeBlueprintClone();

            if (playerCharacter.Blueprint != null)
            {
                playerCharacter.Blueprint.hp = 1f;
            }

            SetCurrentHealth(1f);
            RefreshHealthBar(1f, 1f);
        }

        private void ApplyCloneLegendary()
        {
            syringeDartAbility.MarkCloneLegendaryTaken();

            if (playerCharacter == null || entityManager == null)
            {
                Debug.LogWarning(
                    "[SyringeLegendaryAugmentAbility] 분신배양 생성 실패: playerCharacter 또는 entityManager가 없습니다.",
                    this);
                return;
            }

            SyringeCloneController.Create(playerCharacter, entityManager, syringeDartAbility);
        }

        private void EnsureRuntimeBlueprintClone()
        {
            if (playerCharacter == null)
            {
                return;
            }

            if (runtimeClonedBlueprint != null && playerCharacter.Blueprint == runtimeClonedBlueprint)
            {
                return;
            }

            if (originalBlueprintAsset == null)
            {
                originalBlueprintAsset = playerCharacter.Blueprint;
            }

            if (originalBlueprintAsset == null)
            {
                Debug.LogWarning(
                    "[SyringeLegendaryAugmentAbility] 원본 CharacterBlueprint를 찾지 못했습니다.",
                    this);
                return;
            }

            runtimeClonedBlueprint = Object.Instantiate(originalBlueprintAsset);
            runtimeClonedBlueprint.name = originalBlueprintAsset.name + " (Runtime Clone)";

            FieldInfo blueprintField = typeof(Character).GetField(
                "characterBlueprint",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (blueprintField == null)
            {
                Debug.LogWarning(
                    "[SyringeLegendaryAugmentAbility] Character의 characterBlueprint 필드를 찾지 못했습니다.",
                    this);
                return;
            }

            blueprintField.SetValue(playerCharacter, runtimeClonedBlueprint);
        }

        private void SetCurrentHealth(float hp)
        {
            if (playerCharacter == null)
            {
                return;
            }

            FieldInfo currentHealthField = typeof(Character).GetField(
                "currentHealth",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (currentHealthField != null)
            {
                currentHealthField.SetValue(playerCharacter, hp);
            }
        }

        private void RefreshHealthBar(float currentHp, float maxHp)
        {
            if (playerCharacter == null)
            {
                return;
            }

            FieldInfo healthBarField = typeof(Character).GetField(
                "healthBar",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (healthBarField == null)
            {
                return;
            }

            object healthBar = healthBarField.GetValue(playerCharacter);

            if (healthBar == null)
            {
                return;
            }

            MethodInfo setupMethod = healthBar.GetType().GetMethod(
                "Setup",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new System.Type[] { typeof(float), typeof(float), typeof(float) },
                null);

            if (setupMethod != null)
            {
                setupMethod.Invoke(healthBar, new object[] { currentHp, 0f, maxHp });
            }
        }
    }
}
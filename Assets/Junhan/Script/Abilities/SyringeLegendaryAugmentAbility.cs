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

            // 신규 전설 증강: 신경차단
            NeuralBlock,

            // 신규 전설 증강: 독 전염
            PoisonContagion,

            // 신규 전설 증강: 장기압착
            OrganCompression
        }

        [Header("Legendary Augment")]
        [Tooltip("이 Ability가 적용할 전설증강 종류입니다.")]
        [SerializeField] private LegendaryAugmentType augmentType;

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

            syringeDartAbility = SyringeAbilityResolver.FindOwnedOrFirst(abilityManager);

            if (syringeDartAbility == null)
            {
                syringeDartAbility = abilityManager.GetComponentInChildren<SyringeDartAbility>(true);
            }

            if (syringeDartAbility == null)
            {
                Debug.LogError("[SyringeLegendaryAugmentAbility] SyringeDartAbility를 찾지 못했습니다.");
            }

            if (playerCharacter != null)
            {
                originalBlueprintAsset = playerCharacter.Blueprint;
            }
        }

        protected override void Use()
        {
            base.Use();

            if (syringeDartAbility == null)
            {
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
            }
        }

        public override bool RequirementsMet()
        {
            if (syringeDartAbility == null)
            {
                return false;
            }

            switch (augmentType)
            {
                case LegendaryAugmentType.LifeBurn:
                    return !syringeDartAbility.HasLifeBurnLegendary() && base.RequirementsMet();

                case LegendaryAugmentType.CloneCulture:
                    return !syringeDartAbility.HasCloneLegendary() && base.RequirementsMet();

                case LegendaryAugmentType.HedgehogNeedle:
                    return !syringeDartAbility.HasHedgehogNeedleLegendary() && base.RequirementsMet();

                case LegendaryAugmentType.HeavySnipe:
                    return !syringeDartAbility.HasHeavySnipeLegendary() && base.RequirementsMet();

                case LegendaryAugmentType.CursorControl:
                    return !syringeDartAbility.HasCursorControlLegendary() && base.RequirementsMet();

                case LegendaryAugmentType.NeuralBlock:
                    return !syringeDartAbility.HasNeuralBlockLegendary() && base.RequirementsMet();

                case LegendaryAugmentType.PoisonContagion:
                    return syringeDartAbility.HasPoisonAugment() &&
                           !syringeDartAbility.HasPoisonContagionLegendary() &&
                           base.RequirementsMet();

                case LegendaryAugmentType.OrganCompression:
                    return !syringeDartAbility.HasOrganCompressionLegendary() && base.RequirementsMet();

                default:
                    return false;
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
                Debug.LogWarning("[SyringeLegendaryAugmentAbility] 원본 CharacterBlueprint를 찾지 못했습니다.");
                return;
            }

            runtimeClonedBlueprint = Object.Instantiate(originalBlueprintAsset);
            runtimeClonedBlueprint.name = originalBlueprintAsset.name + " (Runtime Clone)";

            FieldInfo blueprintField = typeof(Character).GetField(
                "characterBlueprint",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (blueprintField == null)
            {
                Debug.LogWarning("[SyringeLegendaryAugmentAbility] Character의 characterBlueprint 필드를 찾지 못했습니다.");
                return;
            }

            blueprintField.SetValue(playerCharacter, runtimeClonedBlueprint);
        }

        private void SetCurrentHealth(float hp)
        {
            FieldInfo currentHealthField = typeof(Character).GetField(
                "currentHealth",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            if (currentHealthField != null)
            {
                currentHealthField.SetValue(playerCharacter, hp);
            }
        }

        private void RefreshHealthBar(float currentHp, float maxHp)
        {
            FieldInfo healthBarField = typeof(Character).GetField(
                "healthBar",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

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
                null
            );

            if (setupMethod != null)
            {
                setupMethod.Invoke(healthBar, new object[] { currentHp, 0f, maxHp });
            }
        }
    }
}
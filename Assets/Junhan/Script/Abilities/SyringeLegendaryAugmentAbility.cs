using System.Reflection;
using UnityEngine;

namespace Vampire
{
    public class SyringeLegendaryAugmentAbility : Ability
    {
        public enum LegendaryAugmentType
        {
            LifeBurn,
            CloneCulture
        }

        [Header("Legendary Augment")]
        [SerializeField] private LegendaryAugmentType augmentType;

        private SyringeDartAbility syringeDartAbility;

        // 원본 블루프린트와 런타임 복제본 구분
        private CharacterBlueprint originalBlueprintAsset;
        private CharacterBlueprint runtimeClonedBlueprint;

        public override void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            maxLevel = 1;
            syringeDartAbility = abilityManager.GetComponentInChildren<SyringeDartAbility>(true);

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

            // 이미 런타임 복제본을 쓰고 있으면 다시 안 만듦
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
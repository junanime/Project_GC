using UnityEngine;

namespace Vampire
{
    public class SyringeSpecialAugmentAbility : Ability
    {
        public enum SpecialAugmentType
        {
            Poison,
            Explosion,
            Homing,
            Pierce
        }

        [Header("Special Augment")]
        [SerializeField] private SpecialAugmentType augmentType;

        private SyringeDartAbility syringeDartAbility;

        public override void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            // 특수증강은 한 번만 선택 가능하게 한다.
            maxLevel = 1;

            syringeDartAbility = abilityManager.GetComponentInChildren<SyringeDartAbility>(true);

            if (syringeDartAbility == null)
            {
                Debug.LogError("[SyringeSpecialAugmentAbility] SyringeDartAbility를 찾지 못했습니다.");
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
                case SpecialAugmentType.Poison:
                    syringeDartAbility.EnablePoisonAugment();
                    break;

                case SpecialAugmentType.Explosion:
                    syringeDartAbility.EnableExplosionAugment();
                    break;

                case SpecialAugmentType.Homing:
                    syringeDartAbility.EnableHomingAugment();
                    break;

                case SpecialAugmentType.Pierce:
                    syringeDartAbility.EnablePierceAugment();
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
                case SpecialAugmentType.Poison:
                    return !syringeDartAbility.HasPoisonAugment() && base.RequirementsMet();

                case SpecialAugmentType.Explosion:
                    return !syringeDartAbility.HasExplosionAugment() && base.RequirementsMet();

                case SpecialAugmentType.Homing:
                    return !syringeDartAbility.HasHomingAugment() && base.RequirementsMet();

                case SpecialAugmentType.Pierce:
                    return !syringeDartAbility.HasPierceAugment() && base.RequirementsMet();

                default:
                    return false;
            }
        }
    }
}
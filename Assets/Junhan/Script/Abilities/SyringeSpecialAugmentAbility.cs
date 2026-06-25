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
            Pierce,
            Honey,
            Mosquito,
            ReturnNeedle,
            AcupunctureFormation,

            // 추가 특수증강
            FiberNeedle,
            CorrosionNeedle,
            PressureNeedle,
            MarkNeedle,
            BipolarNeedle,

            // 신규 특수증강
            DigestiveAcidSacNeedle,
            HungerNeedle,
            GutBacteriaNeedle
        }

        [Header("Special Augment")]
        [Tooltip("이 Ability가 적용할 특수증강 종류입니다.")]
        [SerializeField] private SpecialAugmentType augmentType;

        [Header("Debug")]
        [Tooltip("특수증강 적용 로그를 출력할지 여부입니다.")]
        [SerializeField] private bool debugLog = true;

        private SyringeDartAbility syringeDartAbility;

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
                    "[SyringeSpecialAugmentAbility] SyringeDartAbility를 찾지 못했습니다.\n" +
                    "AbilityManager 아래에 실제 시작 침 능력이 있는지 확인하세요.",
                    this
                );
            }
        }

        protected override void Use()
        {
            base.Use();

            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null)
            {
                Debug.LogError(
                    $"[SyringeSpecialAugmentAbility] {augmentType} 적용 실패: SyringeDartAbility가 없습니다.",
                    this
                );
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

                case SpecialAugmentType.Honey:
                    syringeDartAbility.EnableHoneyAugment();
                    break;

                case SpecialAugmentType.Mosquito:
                    syringeDartAbility.EnableMosquitoAugment();
                    break;

                case SpecialAugmentType.ReturnNeedle:
                    syringeDartAbility.EnableReturnNeedleAugment();
                    break;

                case SpecialAugmentType.AcupunctureFormation:
                    syringeDartAbility.EnableAcupunctureFormationAugment();
                    break;

                case SpecialAugmentType.FiberNeedle:
                    syringeDartAbility.EnableFiberNeedleAugment();
                    break;

                case SpecialAugmentType.CorrosionNeedle:
                    syringeDartAbility.EnableCorrosionNeedleAugment();
                    break;

                case SpecialAugmentType.PressureNeedle:
                    syringeDartAbility.EnablePressureNeedleAugment();
                    break;

                case SpecialAugmentType.MarkNeedle:
                    syringeDartAbility.EnableMarkNeedleAugment();
                    break;

                case SpecialAugmentType.BipolarNeedle:
                    syringeDartAbility.EnableBipolarNeedleAugment();
                    break;

                case SpecialAugmentType.DigestiveAcidSacNeedle:
                    syringeDartAbility.EnableDigestiveAcidSacNeedleAugment();
                    break;

                case SpecialAugmentType.HungerNeedle:
                    syringeDartAbility.EnableHungerNeedleAugment();
                    break;

                case SpecialAugmentType.GutBacteriaNeedle:
                    syringeDartAbility.EnableGutBacteriaNeedleAugment();
                    break;
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[SyringeSpecialAugmentAbility] 특수증강 적용 완료 | " +
                    $"Type={augmentType} | " +
                    $"Target={syringeDartAbility.name} | " +
                    $"Owned={syringeDartAbility.Owned} | " +
                    $"Active={syringeDartAbility.gameObject.activeInHierarchy}",
                    syringeDartAbility
                );
            }
        }

        public override bool RequirementsMet()
        {
            RefreshSyringeDartAbilityReference();

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

                case SpecialAugmentType.Honey:
                    return !syringeDartAbility.HasHoneyAugment() && base.RequirementsMet();

                case SpecialAugmentType.Mosquito:
                    return !syringeDartAbility.HasMosquitoAugment() && base.RequirementsMet();

                case SpecialAugmentType.ReturnNeedle:
                    return !syringeDartAbility.HasReturnNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.AcupunctureFormation:
                    return !syringeDartAbility.HasAcupunctureFormationAugment() && base.RequirementsMet();

                case SpecialAugmentType.FiberNeedle:
                    return !syringeDartAbility.HasFiberNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.CorrosionNeedle:
                    return !syringeDartAbility.HasCorrosionNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.PressureNeedle:
                    return !syringeDartAbility.HasPressureNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.MarkNeedle:
                    return !syringeDartAbility.HasMarkNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.BipolarNeedle:
                    return !syringeDartAbility.HasBipolarNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.DigestiveAcidSacNeedle:
                    return !syringeDartAbility.HasDigestiveAcidSacNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.HungerNeedle:
                    return !syringeDartAbility.HasHungerNeedleAugment() && base.RequirementsMet();

                case SpecialAugmentType.GutBacteriaNeedle:
                    return !syringeDartAbility.HasGutBacteriaNeedleAugment() && base.RequirementsMet();

                default:
                    return false;
            }
        }

        private void RefreshSyringeDartAbilityReference()
        {
            SyringeDartAbility resolvedAbility = SyringeAbilityResolver.FindOwnedOrFirst(abilityManager);

            if (resolvedAbility != null)
            {
                syringeDartAbility = resolvedAbility;
            }
        }
    }
}
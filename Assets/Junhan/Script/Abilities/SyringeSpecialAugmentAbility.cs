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
            GutBacteriaNeedle,
            WoodNeedle = 16,
            FireNeedle = 17,
            IceNeedle = 18,
            WindNeedle = 19,
            VibrationNeedle = 20
         
        }

        [Header("Special Augment")]
        [Tooltip("이 Ability가 적용할 특수증강 종류입니다.")]
        [SerializeField] private SpecialAugmentType augmentType;

        [Header("Debug")]
        [Tooltip("특수증강 적용 로그를 출력할지 여부입니다.")]
        [SerializeField] private bool debugLog = true;

        private SyringeDartAbility syringeDartAbility;
        public SpecialAugmentType Type => augmentType;
        public override string Name => (int)augmentType >= 16 ? Ver4AugmentCatalog.ParentNames[(int)augmentType] : base.Name;
        public override string Description => (int)augmentType >= 16 ? NewEffectDescription() : base.Description;
        public void ConfigureNewAugment(SpecialAugmentType type, Sprite icon)
        {
            augmentType=type; image=icon; augmentTier=AugmentTier.Special;
        }
        private string NewEffectDescription()
        {
            switch(augmentType)
            {
                case SpecialAugmentType.WoodNeedle: return "적중 시 4초 씨앗. 3중첩이면 소모하여 2 거리 내 다른 적에게 침 피해의 40% 가지 추가타.";
                case SpecialAugmentType.FireNeedle: return "적중 시 25% 확률로 3초 화상. 1초마다 최대 체력 0.5% 피해, 기본 3중첩. 보스 틱 피해는 부여한 침 피해의 25% 상한.";
                case SpecialAugmentType.IceNeedle: return "적중 시 20% 확률로 1초 빙결. 다음 침 적중 피해 +20% 후 해제, 재빙결 대기 2초. 보스는 20% 감속.";
                case SpecialAugmentType.WindNeedle: return "공유 침 공격 속도 +10%, 피해량 +8%, 투사체 속도 +12%.";
                case SpecialAugmentType.VibrationNeedle: return "적중 시 반경 1 충격파: 침 피해 35%와 넉백. 플레이어당 발동 간격 0.5초. 추가타로 재발동하지 않음.";
                default:return string.Empty;
            }
        }

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
            if ((int)augmentType >= 16 && syringeDartAbility != null)
            {
                syringeDartAbility.EnableVer4Special(augmentType);
                return;
            }

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
            if ((int)augmentType >= 16)
                return syringeDartAbility != null && !syringeDartAbility.HasVer4Special(augmentType) && base.RequirementsMet();

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

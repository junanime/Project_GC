using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using SpecialType = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    /// <summary>
    /// 특수증강 획득 후에만 등장하는 조건부 일반증강 카드입니다.
    ///
    /// 기존 SyringeGeneralRandomAugmentAbility의 ConditionalRandomOne 구조를 확장한 버전입니다.
    /// 기존 enum/switch에 160개를 직접 추가하면 유지보수가 어려워지므로,
    /// 이 스크립트는 데이터 테이블 방식으로 16종 특수증강 × 10개 조건부 일반증강을 관리합니다.
    ///
    /// 사용 방식:
    /// - 일반 랜덤 2개 카드: 기존 SyringeGeneralRandomAugmentAbility 유지
    /// - 조건부 일반 1개 카드: 이 스크립트를 붙인 Ability 프리팹 사용
    /// </summary>
    public class SyringeSpecialConditionalGeneralAugmentAbility : Ability
    {
        [Header("Special Conditional General Augment")]
        [Tooltip("이 조건부 일반증강 카드가 몇 번까지 다시 등장할 수 있는지 설정합니다.")]
        [SerializeField] private int maxSelections = 999;

        [Tooltip("체크하면 선택된 조건부 일반증강 적용 로그를 출력합니다.")]
        [SerializeField] private bool conditionalGeneralDebugLog = true;

        private SyringeDartAbility syringeDartAbility;
        private PlayerGeneralStatRuntime statRuntime;

        private readonly Dictionary<string, int> stackCounts = new Dictionary<string, int>();

        private Definition previewDefinition;
        private bool previewPrepared = false;

        public override string Description
        {
            get
            {
                PreparePreviewIfNeeded();

                if (previewDefinition == null)
                {
                    return "현재 보유한 특수증강 중 강화 가능한 조건부 일반증강이 없습니다.";
                }

                return
                    "현재 보유한 특수증강과 관련된 일반증강 1개를 획득합니다.\n\n" +
                    "- " + previewDefinition.DisplayName + ": " + previewDefinition.EffectText;
            }
        }

        public override void Init(
            AbilityManager abilityManager,
            EntityManager entityManager,
            Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            augmentTier = AugmentTier.General;
            maxLevel = maxSelections;

            RefreshSyringeDartAbilityReference();
            statRuntime = PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);

            if (syringeDartAbility == null)
            {
                Debug.LogError(
                    "[조건부 일반증강] SyringeDartAbility를 찾지 못했습니다. " +
                    "AbilityManager 아래에 시작 침 능력이 있는지 확인하세요.",
                    this);
            }
        }

        protected override void Use()
        {
            ApplyPreparedAugment();
        }

        protected override void Upgrade()
        {
            ApplyPreparedAugment();
        }

        public override bool RequirementsMet()
        {
            if (level >= maxSelections)
            {
                return false;
            }

            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null || playerCharacter == null)
            {
                return false;
            }

            List<Definition> candidates = BuildCandidateList();

            if (candidates.Count <= 0)
            {
                return false;
            }

            ClearPreview();
            return true;
        }

        private void PreparePreviewIfNeeded()
        {
            if (previewPrepared)
            {
                return;
            }

            List<Definition> candidates = BuildCandidateList();

            if (candidates.Count <= 0)
            {
                previewDefinition = null;
                previewPrepared = true;
                return;
            }

            int index = UnityEngine.Random.Range(0, candidates.Count);
            previewDefinition = candidates[index];
            previewPrepared = true;
        }

        private void ApplyPreparedAugment()
        {
            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null || playerCharacter == null)
            {
                return;
            }

            if (statRuntime == null)
            {
                statRuntime = PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);
            }

            PreparePreviewIfNeeded();

            if (previewDefinition == null)
            {
                Debug.LogWarning("[조건부 일반증강] 적용 가능한 미리보기 증강이 없습니다.", this);
                ClearPreview();
                return;
            }

            ApplyContext context = new ApplyContext(
                playerCharacter,
                syringeDartAbility,
                statRuntime,
                this,
                conditionalGeneralDebugLog);

            previewDefinition.Apply(context);
            AddStack(previewDefinition.Id);

            if (conditionalGeneralDebugLog)
            {
                Debug.Log(
                    $"[조건부 일반증강] {previewDefinition.DisplayName} 적용 | " +
                    $"{previewDefinition.EffectText} | " +
                    $"조건={previewDefinition.RequiredSpecial}",
                    this);
            }

            ClearPreview();
        }

        private void ClearPreview()
        {
            previewDefinition = null;
            previewPrepared = false;
        }

        private void RefreshSyringeDartAbilityReference()
        {
            SyringeDartAbility resolvedAbility = SyringeAbilityResolver.FindOwnedOrFirst(abilityManager);

            if (resolvedAbility != null)
            {
                syringeDartAbility = resolvedAbility;
            }
        }

        private List<Definition> BuildCandidateList()
        {
            List<Definition> candidates = new List<Definition>();
            List<Definition> allDefinitions = GetAllDefinitions();

            for (int i = 0; i < allDefinitions.Count; i++)
            {
                Definition definition = allDefinitions[i];

                if (!HasRequiredSpecial(definition.RequiredSpecial))
                {
                    continue;
                }

                if (IsMaxed(definition))
                {
                    continue;
                }

                candidates.Add(definition);
            }

            return candidates;
        }

        private bool HasRequiredSpecial(SpecialType specialType)
        {
            if (syringeDartAbility == null)
            {
                return false;
            }

            switch (specialType)
            {
                case SpecialType.Poison:
                    return syringeDartAbility.HasPoisonAugment();

                case SpecialType.Explosion:
                    return syringeDartAbility.HasExplosionAugment();

                case SpecialType.Homing:
                    return syringeDartAbility.HasHomingAugment();

                case SpecialType.Pierce:
                    return syringeDartAbility.HasPierceAugment();

                case SpecialType.Honey:
                    return syringeDartAbility.HasHoneyAugment();

                case SpecialType.Mosquito:
                    return syringeDartAbility.HasMosquitoAugment();

                case SpecialType.ReturnNeedle:
                    return syringeDartAbility.HasReturnNeedleAugment();

                case SpecialType.AcupunctureFormation:
                    return syringeDartAbility.HasAcupunctureFormationAugment();

                case SpecialType.FiberNeedle:
                    return syringeDartAbility.HasFiberNeedleAugment();

                case SpecialType.CorrosionNeedle:
                    return syringeDartAbility.HasCorrosionNeedleAugment();

                case SpecialType.PressureNeedle:
                    return syringeDartAbility.HasPressureNeedleAugment();

                case SpecialType.MarkNeedle:
                    return syringeDartAbility.HasMarkNeedleAugment();

                case SpecialType.BipolarNeedle:
                    return syringeDartAbility.HasBipolarNeedleAugment();

                case SpecialType.DigestiveAcidSacNeedle:
                    return syringeDartAbility.HasDigestiveAcidSacNeedleAugment();

                case SpecialType.HungerNeedle:
                    return syringeDartAbility.HasHungerNeedleAugment();

                case SpecialType.GutBacteriaNeedle:
                    return syringeDartAbility.HasGutBacteriaNeedleAugment();

                default:
                    return false;
            }
        }

        private void AddStack(string id)
        {
            if (!stackCounts.ContainsKey(id))
            {
                stackCounts[id] = 0;
            }

            stackCounts[id]++;
        }

        private bool IsMaxed(Definition definition)
        {
            if (definition == null)
            {
                return true;
            }

            if (definition.MaxStack <= 0)
            {
                return false;
            }

            int currentStack = stackCounts.ContainsKey(definition.Id)
                ? stackCounts[definition.Id]
                : 0;

            return currentStack >= definition.MaxStack;
        }

        private static List<Definition> cachedDefinitions;

        private static List<Definition> GetAllDefinitions()
        {
            if (cachedDefinitions != null)
            {
                return cachedDefinitions;
            }

            cachedDefinitions = BuildAllDefinitions();
            return cachedDefinitions;
        }

        private static List<Definition> BuildAllDefinitions()
        {
            List<Definition> defs = new List<Definition>();

            // ------------------------------------------------------------------
            // Poison Needle / 독침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.Poison, "Poison_01", "독성 농축", "독 틱 피해 +12%", 8,
                c => c.MultiplyFloat("poisonTickDamage", 1.12f));

            Add(defs, SpecialType.Poison, "Poison_02", "느린 부식독", "독 지속시간 +12%", 8,
                c => c.MultiplyFloat("poisonDuration", 1.12f));

            Add(defs, SpecialType.Poison, "Poison_03", "빠른 침투독", "독 피해 간격 -8%", 8,
                c => c.MultiplyFloat("poisonTickInterval", 0.92f, 0.08f, 999f));

            Add(defs, SpecialType.Poison, "Poison_04", "위산성 독막", "상태이상 피해 +5%, 독 피해 +5%", 8,
                c =>
                {
                    c.Runtime.AddStatusDamageMultiplier(0.05f);
                    c.MultiplyFloat("poisonTickDamage", 1.05f);
                });

            Add(defs, SpecialType.Poison, "Poison_05", "독성 점막 반응", "경험치 획득량 +5%, 독 지속시간 +5%", 8,
                c =>
                {
                    c.Player.AddExpMultiplier(0.05f);
                    c.MultiplyFloat("poisonDuration", 1.05f);
                });

            Add(defs, SpecialType.Poison, "Poison_06", "독 지속 안정화", "상태이상 지속시간 +6%, 독 지속시간 +6%", 8,
                c =>
                {
                    c.Runtime.AddStatusDurationMultiplier(0.06f);
                    c.MultiplyFloat("poisonDuration", 1.06f);
                });

            Add(defs, SpecialType.Poison, "Poison_07", "독 바늘 예열", "치명타 확률 +3%, 독 피해 +4%", 8,
                c =>
                {
                    c.Player.AddCritChance(0.03f);
                    c.MultiplyFloat("poisonTickDamage", 1.04f);
                });

            Add(defs, SpecialType.Poison, "Poison_08", "농도 유지", "독 지속시간 +8%, 독 피해 간격 -3%", 8,
                c =>
                {
                    c.MultiplyFloat("poisonDuration", 1.08f);
                    c.MultiplyFloat("poisonTickInterval", 0.97f, 0.08f, 999f);
                });

            Add(defs, SpecialType.Poison, "Poison_09", "위산 독침질", "넉백 +6%, 독 피해 +4%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.06f);
                    c.MultiplyFloat("poisonTickDamage", 1.04f);
                });

            Add(defs, SpecialType.Poison, "Poison_10", "독성 잔향", "상태이상 피해 +6%, 상태이상 지속시간 +4%", 8,
                c =>
                {
                    c.Runtime.AddStatusDamageMultiplier(0.06f);
                    c.Runtime.AddStatusDurationMultiplier(0.04f);
                });

            // ------------------------------------------------------------------
            // Explosion Needle / 폭발침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.Explosion, "Explosion_01", "폭발 안정화", "폭발 확률 +5%", 6,
                c => c.AddFloat("specialExplosionChance", 0.05f, 0f, 1f));

            Add(defs, SpecialType.Explosion, "Explosion_02", "위산 폭압", "폭발 반경 +8%", 8,
                c => c.MultiplyFloat("explosionRadius", 1.08f));

            Add(defs, SpecialType.Explosion, "Explosion_03", "점막 파편", "폭발 피해 +10%", 8,
                c => c.MultiplyFloat("explosionDamage", 1.10f));

            Add(defs, SpecialType.Explosion, "Explosion_04", "짧은 충격파", "넉백 +10%, 폭발 반경 +3%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.10f);
                    c.MultiplyFloat("explosionRadius", 1.03f);
                });

            Add(defs, SpecialType.Explosion, "Explosion_05", "저온 폭발", "폭발 반경 +5%, 상태이상 지속시간 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("explosionRadius", 1.05f);
                    c.Runtime.AddStatusDurationMultiplier(0.04f);
                });

            Add(defs, SpecialType.Explosion, "Explosion_06", "폭심 집중", "폭발 피해 +15%, 폭발 반경 -4%", 8,
                c =>
                {
                    c.MultiplyFloat("explosionDamage", 1.15f);
                    c.MultiplyFloat("explosionRadius", 0.96f, 0.1f, 999f);
                });

            Add(defs, SpecialType.Explosion, "Explosion_07", "가스 압축", "폭발 피해 +18%, 투사체 크기 -2%", 8,
                c =>
                {
                    c.MultiplyFloat("explosionDamage", 1.18f);
                    c.Player.AddProjectileSize(-0.02f);
                });

            Add(defs, SpecialType.Explosion, "Explosion_08", "분산 폭발", "폭발 반경 +12%, 폭발 피해 -3%", 8,
                c =>
                {
                    c.MultiplyFloat("explosionRadius", 1.12f);
                    c.MultiplyFloat("explosionDamage", 0.97f, 0.1f, 999f);
                });

            Add(defs, SpecialType.Explosion, "Explosion_09", "연쇄 예열", "공격속도 +4%, 폭발 피해 +5%", 8,
                c =>
                {
                    c.Player.AddAttackSpeed(0.04f);
                    c.MultiplyFloat("explosionDamage", 1.05f);
                });

            Add(defs, SpecialType.Explosion, "Explosion_10", "소화 가스 반응", "폭발 반경 +4%, 꿀침 보유 시 감속 지속시간 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("explosionRadius", 1.04f);
                    c.MultiplyFloat("honeyDuration", 1.04f);
                });

            // ------------------------------------------------------------------
            // Homing Needle / 유도침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.Homing, "Homing_01", "후각 추적", "유도 탐지 범위 +10%", 8,
                c => c.MultiplyFloat("homingRange", 1.10f));

            Add(defs, SpecialType.Homing, "Homing_02", "점막 방향감각", "유도 회전 속도 +10%", 8,
                c => c.MultiplyFloat("homingLerpSpeed", 1.10f));

            Add(defs, SpecialType.Homing, "Homing_03", "위장 냄새 각인", "유도 탐지 범위 +6%, 유도 회전 속도 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("homingRange", 1.06f);
                    c.MultiplyFloat("homingLerpSpeed", 1.04f);
                });

            Add(defs, SpecialType.Homing, "Homing_04", "짧은 재탐색", "유도 회전 속도 +6%, 투사체 속도 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("homingLerpSpeed", 1.06f);
                    c.Player.AddProjectileSpeed(0.04f);
                });

            Add(defs, SpecialType.Homing, "Homing_05", "근접 추적 보정", "유도 회전 속도 +12%, 유도 범위 -3%", 8,
                c =>
                {
                    c.MultiplyFloat("homingLerpSpeed", 1.12f);
                    c.MultiplyFloat("homingRange", 0.97f, 0.1f, 999f);
                });

            Add(defs, SpecialType.Homing, "Homing_06", "원거리 추적 보정", "유도 범위 +14%, 유도 회전 속도 -2%", 8,
                c =>
                {
                    c.MultiplyFloat("homingRange", 1.14f);
                    c.MultiplyFloat("homingLerpSpeed", 0.98f, 0.1f, 999f);
                });

            Add(defs, SpecialType.Homing, "Homing_07", "흔들림 억제", "유도 회전 속도 +8%", 8,
                c => c.MultiplyFloat("homingLerpSpeed", 1.08f));

            Add(defs, SpecialType.Homing, "Homing_08", "약점 추적", "치명타 확률 +3%, 유도 범위 +4%", 8,
                c =>
                {
                    c.Player.AddCritChance(0.03f);
                    c.MultiplyFloat("homingRange", 1.04f);
                });

            Add(defs, SpecialType.Homing, "Homing_09", "군집 추적", "유도 범위 +8%, 투사체 크기 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("homingRange", 1.08f);
                    c.Player.AddProjectileSize(0.03f);
                });

            Add(defs, SpecialType.Homing, "Homing_10", "위산 냄새 잔류", "유도 회전 속도 +5%, 사거리 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("homingLerpSpeed", 1.05f);
                    c.Player.AddRangeBoost(0.04f);
                });

            // ------------------------------------------------------------------
            // Pierce Needle / 관통침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.Pierce, "Pierce_01", "점막 절삭", "관통 횟수 +1", 5,
                c => c.AddInt("pierceCount", 1, 0, 999));

            Add(defs, SpecialType.Pierce, "Pierce_02", "관통 안정성", "방어 관통 +5%", 8,
                c => c.Runtime.AddDefensePierce(0.05f));

            Add(defs, SpecialType.Pierce, "Pierce_03", "얇은 침끝", "투사체 속도 +7%", 8,
                c => c.Player.AddProjectileSpeed(0.07f));

            Add(defs, SpecialType.Pierce, "Pierce_04", "직선 절개", "침 피해 +6%, 사거리 +3%", 8,
                c =>
                {
                    c.Player.AddDamageMultiplier(0.06f);
                    c.Player.AddRangeBoost(0.03f);
                });

            Add(defs, SpecialType.Pierce, "Pierce_05", "관통 경로 기억", "사거리 +8%", 8,
                c => c.Player.AddRangeBoost(0.08f));

            Add(defs, SpecialType.Pierce, "Pierce_06", "내벽 긁기", "넉백 +8%, 관통 횟수 +1", 4,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.08f);
                    c.AddInt("pierceCount", 1, 0, 999);
                });

            Add(defs, SpecialType.Pierce, "Pierce_07", "깊은 찌르기", "침 피해 +9%, 공격속도 -2%", 8,
                c =>
                {
                    c.Player.AddDamageMultiplier(0.09f);
                    c.Player.AddAttackSpeed(-0.02f);
                });

            Add(defs, SpecialType.Pierce, "Pierce_08", "연속 관통 감각", "공격속도 +5%, 투사체 속도 +4%", 8,
                c =>
                {
                    c.Player.AddAttackSpeed(0.05f);
                    c.Player.AddProjectileSpeed(0.04f);
                });

            Add(defs, SpecialType.Pierce, "Pierce_09", "점막 절단면", "치명타 피해 +8%, 방어 관통 +3%", 8,
                c =>
                {
                    c.Runtime.AddCritDamageMultiplier(0.08f);
                    c.Runtime.AddDefensePierce(0.03f);
                });

            Add(defs, SpecialType.Pierce, "Pierce_10", "관통 회수 훈련", "공격속도 +4%, 관통 횟수 +1", 4,
                c =>
                {
                    c.Player.AddAttackSpeed(0.04f);
                    c.AddInt("pierceCount", 1, 0, 999);
                });

            // ------------------------------------------------------------------
            // Honey Needle / 꿀침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.Honey, "Honey_01", "당분 점착", "감속 지속시간 +12%", 8,
                c => c.MultiplyFloat("honeyDuration", 1.12f));

            Add(defs, SpecialType.Honey, "Honey_02", "고농도 꿀막", "감속 강도 +6%", 8,
                c => c.MultiplyFloat("honeySlowMultiplier", 0.94f, 0.15f, 1f));

            Add(defs, SpecialType.Honey, "Honey_03", "끈적한 발밑", "넉백 +8%, 감속 지속시간 +4%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.08f);
                    c.MultiplyFloat("honeyDuration", 1.04f);
                });

            Add(defs, SpecialType.Honey, "Honey_04", "꿀막 유지", "감속 지속시간 +8%, 상태이상 지속시간 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("honeyDuration", 1.08f);
                    c.Runtime.AddStatusDurationMultiplier(0.04f);
                });

            Add(defs, SpecialType.Honey, "Honey_05", "저속 표적화", "감속 대상 대응력 강화: 치명타 확률 +3%", 8,
                c => c.Player.AddCritChance(0.03f));

            Add(defs, SpecialType.Honey, "Honey_06", "당분 굳힘", "치명타 피해 +8%, 감속 지속시간 +3%", 8,
                c =>
                {
                    c.Runtime.AddCritDamageMultiplier(0.08f);
                    c.MultiplyFloat("honeyDuration", 1.03f);
                });

            Add(defs, SpecialType.Honey, "Honey_07", "점착 확산", "투사체 크기 +5%, 감속 지속시간 +5%", 8,
                c =>
                {
                    c.Player.AddProjectileSize(0.05f);
                    c.MultiplyFloat("honeyDuration", 1.05f);
                });

            Add(defs, SpecialType.Honey, "Honey_08", "꿀 흔적", "감속 강도 +4%, 사거리 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("honeySlowMultiplier", 0.96f, 0.15f, 1f);
                    c.Player.AddRangeBoost(0.03f);
                });

            Add(defs, SpecialType.Honey, "Honey_09", "무거운 당막", "감속 지속시간 +10%, 넉백 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("honeyDuration", 1.10f);
                    c.Runtime.AddKnockbackMultiplier(0.04f);
                });

            Add(defs, SpecialType.Honey, "Honey_10", "꿀침 숙성", "골드 드롭 확률 +3%, 감속 지속시간 +4%", 8,
                c =>
                {
                    c.Runtime.AddGoldDropChance(0.03f);
                    c.MultiplyFloat("honeyDuration", 1.04f);
                });

            // ------------------------------------------------------------------
            // Mosquito Needle / 모기침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.Mosquito, "Mosquito_01", "흡혈 효율", "흡혈 회복량 +12%", 8,
                c => c.MultiplyFloat("mosquitoHealPerHit", 1.12f));

            Add(defs, SpecialType.Mosquito, "Mosquito_02", "얇은 혈관 탐색", "공격속도 +5%, 흡혈량 +4%", 8,
                c =>
                {
                    c.Player.AddAttackSpeed(0.05f);
                    c.MultiplyFloat("mosquitoHealPerHit", 1.04f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_03", "응고 지연", "흡혈량 +6%, 상태이상 지속시간 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("mosquitoHealPerHit", 1.06f);
                    c.Runtime.AddStatusDurationMultiplier(0.03f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_04", "혈액 보존", "받는 피해 감소 +3%, 흡혈량 +5%", 8,
                c =>
                {
                    c.Runtime.AddDamageReduction(0.03f);
                    c.MultiplyFloat("mosquitoHealPerHit", 1.05f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_05", "위장 혈류 감각", "최대 HP +5, 흡혈량 +4%", 8,
                c =>
                {
                    c.Player.AddMaxHealthBonus(5f);
                    c.MultiplyFloat("mosquitoHealPerHit", 1.04f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_06", "혈액 농축", "침 피해 +5%, 흡혈량 +5%", 8,
                c =>
                {
                    c.Player.AddDamageMultiplier(0.05f);
                    c.MultiplyFloat("mosquitoHealPerHit", 1.05f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_07", "모기침 안정화", "보스 흡혈 배율 +10%", 8,
                c => c.MultiplyFloat("mosquitoBossHealMultiplier", 1.10f));

            Add(defs, SpecialType.Mosquito, "Mosquito_08", "소량 수혈", "정지 시 회복 +0.1, 흡혈량 +4%", 8,
                c =>
                {
                    c.Player.AddHealOnIdle(0.1f);
                    c.MultiplyFloat("mosquitoHealPerHit", 1.04f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_09", "혈액 회수", "흡혈량 +8%, 골드 드롭 확률 +2%", 8,
                c =>
                {
                    c.MultiplyFloat("mosquitoHealPerHit", 1.08f);
                    c.Runtime.AddGoldDropChance(0.02f);
                });

            Add(defs, SpecialType.Mosquito, "Mosquito_10", "과흡혈 억제", "흡혈량 +5%, 받는 피해 감소 +2%", 8,
                c =>
                {
                    c.MultiplyFloat("mosquitoHealPerHit", 1.05f);
                    c.Runtime.AddDamageReduction(0.02f);
                });

            // ------------------------------------------------------------------
            // Return Needle / 귀환침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.ReturnNeedle, "Return_01", "회귀 속도", "귀환 속도 +10%", 8,
                c => c.MultiplyFloat("returnNeedleSpeedMultiplier", 1.10f));

            Add(defs, SpecialType.ReturnNeedle, "Return_02", "귀환 절삭", "귀환 피해 +10%", 8,
                c => c.MultiplyFloat("returnNeedleDamageMultiplier", 1.10f));

            Add(defs, SpecialType.ReturnNeedle, "Return_03", "점막 탄성", "귀환 유지시간 +10%", 8,
                c => c.MultiplyFloat("returnNeedleMaxDuration", 1.10f));

            Add(defs, SpecialType.ReturnNeedle, "Return_04", "회수 거리 보정", "귀환 완료 거리 +8%", 8,
                c => c.MultiplyFloat("returnNeedleArriveDistance", 1.08f));

            Add(defs, SpecialType.ReturnNeedle, "Return_05", "뒤통수 찌르기", "귀환 피해 +6%, 치명타 확률 +2%", 8,
                c =>
                {
                    c.MultiplyFloat("returnNeedleDamageMultiplier", 1.06f);
                    c.Player.AddCritChance(0.02f);
                });

            Add(defs, SpecialType.ReturnNeedle, "Return_06", "역방향 예리함", "귀환 피해 +14%, 기본 피해 -2%", 8,
                c =>
                {
                    c.MultiplyFloat("returnNeedleDamageMultiplier", 1.14f);
                    c.Player.AddDamageMultiplier(-0.02f);
                });

            Add(defs, SpecialType.ReturnNeedle, "Return_07", "왕복 균형", "귀환 피해 +6%, 투사체 속도 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("returnNeedleDamageMultiplier", 1.06f);
                    c.Player.AddProjectileSpeed(0.04f);
                });

            Add(defs, SpecialType.ReturnNeedle, "Return_08", "회귀 감각", "귀환 속도 +6%, 공격속도 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("returnNeedleSpeedMultiplier", 1.06f);
                    c.Player.AddAttackSpeed(0.03f);
                });

            Add(defs, SpecialType.ReturnNeedle, "Return_09", "귀환 궤적 안정화", "귀환 유지시간 +6%, 귀환 속도 +5%", 8,
                c =>
                {
                    c.MultiplyFloat("returnNeedleMaxDuration", 1.06f);
                    c.MultiplyFloat("returnNeedleSpeedMultiplier", 1.05f);
                });

            Add(defs, SpecialType.ReturnNeedle, "Return_10", "혈관 회수술", "귀환 피해 +5%, 흡혈량 +5%", 8,
                c =>
                {
                    c.MultiplyFloat("returnNeedleDamageMultiplier", 1.05f);
                    c.MultiplyFloat("mosquitoHealPerHit", 1.05f);
                });

            // ------------------------------------------------------------------
            // Acupuncture Formation / 침술진 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_01", "혈자리 확장", "침술진 침 개수 +1", 5,
                c => c.AddInt("acupunctureFormationNeedleCount", 1, 1, 999));

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_02", "침술 각도 보정", "침술진 시각 크기 +8%", 8,
                c => c.MultiplyFloat("acupunctureFormationVisualScale", 1.08f));

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_03", "빠른 혈자리 반응", "대쉬 재충전 속도 +6%", 8,
                c => c.Player.AddDashRechargeSpeed(0.06f));

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_04", "침술진 안정화", "침술진 피해 +8%", 8,
                c => c.MultiplyFloat("acupunctureFormationDamageMultiplier", 1.08f));

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_05", "신경 혈자리", "침술진 피해 +5%, 투사체 속도 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("acupunctureFormationDamageMultiplier", 1.05f);
                    c.Player.AddProjectileSpeed(0.04f);
                });

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_06", "혈자리 회수", "대쉬 재충전 속도 +5%, 침술진 지속시간 +4%", 8,
                c =>
                {
                    c.Player.AddDashRechargeSpeed(0.05f);
                    c.MultiplyFloat("acupunctureFormationLifetime", 1.04f);
                });

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_07", "근육 이완", "이동속도 +5%, 대쉬 거리 +3%", 8,
                c =>
                {
                    c.AddMoveSpeedPercent(0.05f);
                    c.Player.AddDashDistance(0.10f);
                });

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_08", "침술 호흡", "침술진 지속시간 +8%, 대쉬 재충전 속도 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("acupunctureFormationLifetime", 1.08f);
                    c.Player.AddDashRechargeSpeed(0.03f);
                });

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_09", "후방 혈자리", "침술진 피해 +6%, 넉백 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("acupunctureFormationDamageMultiplier", 1.06f);
                    c.Runtime.AddKnockbackMultiplier(0.04f);
                });

            Add(defs, SpecialType.AcupunctureFormation, "Acupuncture_10", "혈자리 숙련", "침술진 침 개수 +1, 피해 +3%", 4,
                c =>
                {
                    c.AddInt("acupunctureFormationNeedleCount", 1, 1, 999);
                    c.MultiplyFloat("acupunctureFormationDamageMultiplier", 1.03f);
                });

            // ------------------------------------------------------------------
            // Fiber Needle / 섬유침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.FiberNeedle, "Fiber_01", "섬유질 응집", "섬유 선 지속시간 +12%", 8,
                c => c.MultiplyFloat("fiberTrailLifetime", 1.12f));

            Add(defs, SpecialType.FiberNeedle, "Fiber_02", "두꺼운 섬유선", "섬유 선 두께 +10%", 8,
                c => c.MultiplyFloat("fiberTrailWidth", 1.10f));

            Add(defs, SpecialType.FiberNeedle, "Fiber_03", "섬유 마찰", "섬유 선 피해 +10%", 8,
                c => c.MultiplyFloat("fiberTrailDamagePerSecond", 1.10f));

            Add(defs, SpecialType.FiberNeedle, "Fiber_04", "섬유 간격 안정화", "섬유 피해 간격 -8%", 8,
                c => c.MultiplyFloat("fiberTrailTickInterval", 0.92f, 0.05f, 999f));

            Add(defs, SpecialType.FiberNeedle, "Fiber_05", "점막 섬유화", "섬유 지속시간 +6%, 상태이상 지속시간 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("fiberTrailLifetime", 1.06f);
                    c.Runtime.AddStatusDurationMultiplier(0.04f);
                });

            Add(defs, SpecialType.FiberNeedle, "Fiber_06", "섬유 절단면", "섬유 피해 +6%, 방어 관통 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("fiberTrailDamagePerSecond", 1.06f);
                    c.Runtime.AddDefensePierce(0.03f);
                });

            Add(defs, SpecialType.FiberNeedle, "Fiber_07", "짧은 섬유 다발", "섬유 피해 +18%, 지속시간 -6%", 8,
                c =>
                {
                    c.MultiplyFloat("fiberTrailDamagePerSecond", 1.18f);
                    c.MultiplyFloat("fiberTrailLifetime", 0.94f, 0.1f, 999f);
                });

            Add(defs, SpecialType.FiberNeedle, "Fiber_08", "긴 섬유 다발", "섬유 지속시간 +18%, 피해 -4%", 8,
                c =>
                {
                    c.MultiplyFloat("fiberTrailLifetime", 1.18f);
                    c.MultiplyFloat("fiberTrailDamagePerSecond", 0.96f, 0.1f, 999f);
                });

            Add(defs, SpecialType.FiberNeedle, "Fiber_09", "섬유 경로 기억", "투사체 속도 +5%, 섬유 두께 +4%", 8,
                c =>
                {
                    c.Player.AddProjectileSpeed(0.05f);
                    c.MultiplyFloat("fiberTrailWidth", 1.04f);
                });

            Add(defs, SpecialType.FiberNeedle, "Fiber_10", "섬유 회수", "경험치 획득량 +5%, 섬유 피해 +4%", 8,
                c =>
                {
                    c.Player.AddExpMultiplier(0.05f);
                    c.MultiplyFloat("fiberTrailDamagePerSecond", 1.04f);
                });

            // ------------------------------------------------------------------
            // Corrosion Needle / 부식침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_01", "위벽 산화", "부식 스택당 받는 피해 증가량 +8%", 8,
                c => c.MultiplyFloat("corrosionDamageTakenBonusPerStack", 1.08f));

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_02", "부식 지속성", "부식 지속시간 +10%", 8,
                c => c.MultiplyFloat("corrosionDuration", 1.10f));

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_03", "산화 침투", "부식 최대 스택 +1", 3,
                c => c.AddInt("corrosionMaxStacks", 1, 1, 20));

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_04", "약한 껍질", "방어 관통 +5%", 8,
                c => c.Runtime.AddDefensePierce(0.05f));

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_05", "부식 표면 확대", "부식 지속시간 +6%, 부식 효과 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("corrosionDuration", 1.06f);
                    c.MultiplyFloat("corrosionDamageTakenBonusPerStack", 1.04f);
                });

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_06", "부식 약점화", "치명타 확률 +3%, 부식 효과 +3%", 8,
                c =>
                {
                    c.Player.AddCritChance(0.03f);
                    c.MultiplyFloat("corrosionDamageTakenBonusPerStack", 1.03f);
                });

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_07", "천천히 녹기", "부식 지속시간 +14%", 8,
                c => c.MultiplyFloat("corrosionDuration", 1.14f));

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_08", "산성 균열", "넉백 +6%, 방어 관통 +3%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.06f);
                    c.Runtime.AddDefensePierce(0.03f);
                });

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_09", "깊은 부식", "사거리 +5%, 부식 지속시간 +4%", 8,
                c =>
                {
                    c.Player.AddRangeBoost(0.05f);
                    c.MultiplyFloat("corrosionDuration", 1.04f);
                });

            Add(defs, SpecialType.CorrosionNeedle, "Corrosion_10", "부식 회수", "골드 드롭 확률 +3%, 부식 효과 +3%", 8,
                c =>
                {
                    c.Runtime.AddGoldDropChance(0.03f);
                    c.MultiplyFloat("corrosionDamageTakenBonusPerStack", 1.03f);
                });

            // ------------------------------------------------------------------
            // Pressure Needle / 압력침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.PressureNeedle, "Pressure_01", "압력 집중", "거리 비례 피해 증가량 +10%", 8,
                c => c.MultiplyFloat("pressureDamageBonusPerDistance", 1.10f));

            Add(defs, SpecialType.PressureNeedle, "Pressure_02", "압력 안정화", "최대 압력 피해 +8%", 8,
                c => c.MultiplyFloat("pressureMaxDamageBonus", 1.08f));

            Add(defs, SpecialType.PressureNeedle, "Pressure_03", "장거리 압력파", "사거리 +8%", 8,
                c => c.Player.AddRangeBoost(0.08f));

            Add(defs, SpecialType.PressureNeedle, "Pressure_04", "과압 침끝", "치명타 피해 +10%, 최대 압력 피해 +3%", 8,
                c =>
                {
                    c.Runtime.AddCritDamageMultiplier(0.10f);
                    c.MultiplyFloat("pressureMaxDamageBonus", 1.03f);
                });

            Add(defs, SpecialType.PressureNeedle, "Pressure_05", "위장 압력계", "거리 비례 피해 +5%, 투사체 속도 +5%", 8,
                c =>
                {
                    c.MultiplyFloat("pressureDamageBonusPerDistance", 1.05f);
                    c.Player.AddProjectileSpeed(0.05f);
                });

            Add(defs, SpecialType.PressureNeedle, "Pressure_06", "압력 보존", "최대 압력 피해 +6%, 방어 관통 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("pressureMaxDamageBonus", 1.06f);
                    c.Runtime.AddDefensePierce(0.03f);
                });

            Add(defs, SpecialType.PressureNeedle, "Pressure_07", "가벼운 침체", "투사체 속도 +8%, 거리 비례 피해 +3%", 8,
                c =>
                {
                    c.Player.AddProjectileSpeed(0.08f);
                    c.MultiplyFloat("pressureDamageBonusPerDistance", 1.03f);
                });

            Add(defs, SpecialType.PressureNeedle, "Pressure_08", "묵직한 압력", "침 피해 +8%, 투사체 속도 -2%", 8,
                c =>
                {
                    c.Player.AddDamageMultiplier(0.08f);
                    c.Player.AddProjectileSpeed(-0.02f);
                });

            Add(defs, SpecialType.PressureNeedle, "Pressure_09", "압력 밀림", "넉백 +10%, 최대 압력 피해 +3%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.10f);
                    c.MultiplyFloat("pressureMaxDamageBonus", 1.03f);
                });

            Add(defs, SpecialType.PressureNeedle, "Pressure_10", "압력 보상", "사거리 +5%, 최대 압력 피해 +4%", 8,
                c =>
                {
                    c.Player.AddRangeBoost(0.05f);
                    c.MultiplyFloat("pressureMaxDamageBonus", 1.04f);
                });

            // ------------------------------------------------------------------
            // Mark Needle / 표식침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.MarkNeedle, "Mark_01", "표식 확대", "표식 지속시간 +12%", 8,
                c => c.MultiplyFloat("markDuration", 1.12f));

            Add(defs, SpecialType.MarkNeedle, "Mark_02", "선명한 표식", "표식 추가 피해 +10%", 8,
                c => c.MultiplyFloat("markBonusDamageMultiplier", 1.10f));

            Add(defs, SpecialType.MarkNeedle, "Mark_03", "표식 감지", "치명타 확률 +3%, 표식 지속시간 +4%", 8,
                c =>
                {
                    c.Player.AddCritChance(0.03f);
                    c.MultiplyFloat("markDuration", 1.04f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_04", "약점 노출", "치명타 피해 +10%, 표식 추가 피해 +3%", 8,
                c =>
                {
                    c.Runtime.AddCritDamageMultiplier(0.10f);
                    c.MultiplyFloat("markBonusDamageMultiplier", 1.03f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_05", "두꺼운 표식", "넉백 +8%, 표식 지속시간 +3%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.08f);
                    c.MultiplyFloat("markDuration", 1.03f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_06", "짧은 표식 폭발감", "표식 추가 피해 +6%, 투사체 크기 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("markBonusDamageMultiplier", 1.06f);
                    c.Player.AddProjectileSize(0.03f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_07", "표식 회수", "표식 지속시간 +8%, 공격속도 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("markDuration", 1.08f);
                    c.Player.AddAttackSpeed(0.03f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_08", "표식 안정화", "표식 지속시간 +5%, 표식 추가 피해 +5%", 8,
                c =>
                {
                    c.MultiplyFloat("markDuration", 1.05f);
                    c.MultiplyFloat("markBonusDamageMultiplier", 1.05f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_09", "위장 문양", "투사체 크기 +4%, 표식 지속시간 +4%", 8,
                c =>
                {
                    c.Player.AddProjectileSize(0.04f);
                    c.MultiplyFloat("markDuration", 1.04f);
                });

            Add(defs, SpecialType.MarkNeedle, "Mark_10", "표식 관찰", "보스 피해 +6%, 표식 추가 피해 +4%", 8,
                c =>
                {
                    c.Runtime.AddBossDamageMultiplier(0.06f);
                    c.MultiplyFloat("markBonusDamageMultiplier", 1.04f);
                });

            // ------------------------------------------------------------------
            // Bipolar Needle / 양극침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.BipolarNeedle, "Bipolar_01", "양극 균형", "양극침 추가 발사체 +1", 4,
                c => c.AddInt("bipolarNeedleBonusProjectileCount", 1, 0, 20));

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_02", "전후방 안정화", "투사체 속도 +5%, 사거리 +3%", 8,
                c =>
                {
                    c.Player.AddProjectileSpeed(0.05f);
                    c.Player.AddRangeBoost(0.03f);
                });

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_03", "위장 극성 강화", "공격속도 +5%, 양극 추가 발사체 유지", 8,
                c => c.Player.AddAttackSpeed(0.05f));

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_04", "반대극 넉백", "넉백 +10%", 8,
                c => c.Runtime.AddKnockbackMultiplier(0.10f));

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_05", "극성 교차", "투사체 +1", 4,
                c => c.Player.AddProjectileCount(1));

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_06", "극성 집중", "침 피해 +8%, 사거리 -2%", 8,
                c =>
                {
                    c.Player.AddDamageMultiplier(0.08f);
                    c.Player.AddRangeBoost(-0.02f);
                });

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_07", "역극 집중", "넉백 +8%, 치명타 확률 +2%", 8,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.08f);
                    c.Player.AddCritChance(0.02f);
                });

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_08", "극성 사거리", "사거리 +8%", 8,
                c => c.Player.AddRangeBoost(0.08f));

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_09", "극성 감각", "공격속도 +4%, 사거리 +4%", 8,
                c =>
                {
                    c.Player.AddAttackSpeed(0.04f);
                    c.Player.AddRangeBoost(0.04f);
                });

            Add(defs, SpecialType.BipolarNeedle, "Bipolar_10", "극성 회전", "투사체 속도 +5%, 투사체 크기 +3%", 8,
                c =>
                {
                    c.Player.AddProjectileSpeed(0.05f);
                    c.Player.AddProjectileSize(0.03f);
                });

            // ------------------------------------------------------------------
            // Digestive Acid Sac Needle / 소화액낭침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_01", "낭액 압축", "소화액 웅덩이 피해 +10%", 8,
                c => c.MultiplyFloat("digestiveAcidPuddleDamagePerSecond", 1.10f));

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_02", "소화액 보존", "소화액 웅덩이 지속시간 +12%", 8,
                c => c.MultiplyFloat("digestiveAcidPuddleLifetime", 1.12f));

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_03", "넓은 낭액", "소화액 웅덩이 반경 +8%", 8,
                c => c.MultiplyFloat("digestiveAcidPuddleRadius", 1.08f));

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_04", "산성 농축", "웅덩이 피해 +16%, 반경 -4%", 8,
                c =>
                {
                    c.MultiplyFloat("digestiveAcidPuddleDamagePerSecond", 1.16f);
                    c.MultiplyFloat("digestiveAcidPuddleRadius", 0.96f, 0.1f, 999f);
                });

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_05", "낭액 확산 억제", "웅덩이 지속시간 +6%, 반경 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("digestiveAcidPuddleLifetime", 1.06f);
                    c.MultiplyFloat("digestiveAcidPuddleRadius", 1.04f);
                });

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_06", "점막 침투액", "웅덩이 피해 +6%, 상태이상 피해 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("digestiveAcidPuddleDamagePerSecond", 1.06f);
                    c.Runtime.AddStatusDamageMultiplier(0.03f);
                });

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_07", "낭액 회수", "웅덩이 피해 +5%, 경험치 획득량 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("digestiveAcidPuddleDamagePerSecond", 1.05f);
                    c.Player.AddExpMultiplier(0.04f);
                });

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_08", "소화액 반응성", "웅덩이 반경 +5%, 피해 +5%", 8,
                c =>
                {
                    c.MultiplyFloat("digestiveAcidPuddleRadius", 1.05f);
                    c.MultiplyFloat("digestiveAcidPuddleDamagePerSecond", 1.05f);
                });

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_09", "낭액 잔류", "웅덩이 지속시간 +8%, 피해 간격 -3%", 8,
                c =>
                {
                    c.MultiplyFloat("digestiveAcidPuddleLifetime", 1.08f);
                    c.MultiplyFloat("digestiveAcidPuddleTickInterval", 0.97f, 0.05f, 999f);
                });

            Add(defs, SpecialType.DigestiveAcidSacNeedle, "AcidSac_10", "낭액 안정화", "웅덩이 피해 간격 -8%", 8,
                c => c.MultiplyFloat("digestiveAcidPuddleTickInterval", 0.92f, 0.05f, 999f));

            // ------------------------------------------------------------------
            // Hunger Needle / 공복침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.HungerNeedle, "Hunger_01", "허기 증폭", "공복 스택당 공격속도 +10%", 8,
                c => c.MultiplyFloat("hungerAttackSpeedBonusPerStack", 1.10f));

            Add(defs, SpecialType.HungerNeedle, "Hunger_02", "공복 지속성", "공복 스택 지속시간 +12%", 8,
                c => c.MultiplyFloat("hungerStackDuration", 1.12f));

            Add(defs, SpecialType.HungerNeedle, "Hunger_03", "허기 적응", "공복 최대 스택 +1", 4,
                c => c.AddInt("hungerMaxStacks", 1, 1, 99));

            Add(defs, SpecialType.HungerNeedle, "Hunger_04", "빠른 공복 반응", "공격속도 +5%, 공복 지속시간 +4%", 8,
                c =>
                {
                    c.Player.AddAttackSpeed(0.05f);
                    c.MultiplyFloat("hungerStackDuration", 1.04f);
                });

            Add(defs, SpecialType.HungerNeedle, "Hunger_05", "공복 호흡", "이동속도 +5%, 공복 스택 효과 +3%", 8,
                c =>
                {
                    c.AddMoveSpeedPercent(0.05f);
                    c.MultiplyFloat("hungerAttackSpeedBonusPerStack", 1.03f);
                });

            Add(defs, SpecialType.HungerNeedle, "Hunger_06", "허기 유지", "공복 지속시간 +8%, 공격속도 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("hungerStackDuration", 1.08f);
                    c.Player.AddAttackSpeed(0.03f);
                });

            Add(defs, SpecialType.HungerNeedle, "Hunger_07", "저혈당 집중", "치명타 확률 +3%, 공복 스택 효과 +3%", 8,
                c =>
                {
                    c.Player.AddCritChance(0.03f);
                    c.MultiplyFloat("hungerAttackSpeedBonusPerStack", 1.03f);
                });

            Add(defs, SpecialType.HungerNeedle, "Hunger_08", "공복 회수", "공복 지속시간 +6%, 경험치 획득량 +3%", 8,
                c =>
                {
                    c.MultiplyFloat("hungerStackDuration", 1.06f);
                    c.Player.AddExpMultiplier(0.03f);
                });

            Add(defs, SpecialType.HungerNeedle, "Hunger_09", "허기 민감화", "공복 최대 스택 +1, 스택 효과 +2%", 3,
                c =>
                {
                    c.AddInt("hungerMaxStacks", 1, 1, 99);
                    c.MultiplyFloat("hungerAttackSpeedBonusPerStack", 1.02f);
                });

            Add(defs, SpecialType.HungerNeedle, "Hunger_10", "공복 조절", "공복 지속시간 +18%, 스택 효과 -3%", 8,
                c =>
                {
                    c.MultiplyFloat("hungerStackDuration", 1.18f);
                    c.MultiplyFloat("hungerAttackSpeedBonusPerStack", 0.97f, 0.001f, 999f);
                });

            // ------------------------------------------------------------------
            // Gut Bacteria Needle / 장내균침 조건부 일반증강 10개
            // ------------------------------------------------------------------
            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_01", "균막 성장", "장내균 스택 지속시간 +12%", 8,
                c => c.MultiplyFloat("gutBacteriaStackDuration", 1.12f));

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_02", "장내균 증식", "장내균 최대 스택 +1", 4,
                c => c.AddInt("gutBacteriaMaxStacks", 1, 1, 99));

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_03", "균총 안정화", "추가 경험치 조건 스택 -1", 2,
                c => c.AddInt("gutBacteriaRequiredStacks", -1, 1, 99));

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_04", "발효 보상", "장내균 추가 경험치 구슬 +1", 4,
                c => c.AddInt("gutBacteriaBonusGemCount", 1, 1, 20));

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_05", "균막 점착", "장내균 지속시간 +6%, 상태이상 지속시간 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("gutBacteriaStackDuration", 1.06f);
                    c.Runtime.AddStatusDurationMultiplier(0.04f);
                });

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_06", "균총 표식", "치명타 확률 +3%, 장내균 지속시간 +4%", 8,
                c =>
                {
                    c.Player.AddCritChance(0.03f);
                    c.MultiplyFloat("gutBacteriaStackDuration", 1.04f);
                });

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_07", "장내균 배양", "장내균 최대 스택 +1, 지속시간 +4%", 3,
                c =>
                {
                    c.AddInt("gutBacteriaMaxStacks", 1, 1, 99);
                    c.MultiplyFloat("gutBacteriaStackDuration", 1.04f);
                });

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_08", "균사 확장", "추가 경험치 흩어짐 반경 +12%, 지속시간 +4%", 8,
                c =>
                {
                    c.MultiplyFloat("gutBacteriaBonusGemSpawnRadius", 1.12f);
                    c.MultiplyFloat("gutBacteriaStackDuration", 1.04f);
                });

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_09", "균막 파열", "넉백 +6%, 장내균 보상 구슬 +1", 4,
                c =>
                {
                    c.Runtime.AddKnockbackMultiplier(0.06f);
                    c.AddInt("gutBacteriaBonusGemCount", 1, 1, 20);
                });

            Add(defs, SpecialType.GutBacteriaNeedle, "Gut_10", "소화균 보상", "골드 드롭 확률 +3%, 장내균 지속시간 +4%", 8,
                c =>
                {
                    c.Runtime.AddGoldDropChance(0.03f);
                    c.MultiplyFloat("gutBacteriaStackDuration", 1.04f);
                });

            return defs;
        }

        private static void Add(
            List<Definition> defs,
            SpecialType requiredSpecial,
            string id,
            string displayName,
            string effectText,
            int maxStack,
            Action<ApplyContext> apply)
        {
            defs.Add(new Definition(
                id,
                requiredSpecial,
                displayName,
                effectText,
                maxStack,
                apply));
        }

        private class Definition
        {
            public readonly string Id;
            public readonly SpecialType RequiredSpecial;
            public readonly string DisplayName;
            public readonly string EffectText;
            public readonly int MaxStack;
            public readonly Action<ApplyContext> Apply;

            public Definition(
                string id,
                SpecialType requiredSpecial,
                string displayName,
                string effectText,
                int maxStack,
                Action<ApplyContext> apply)
            {
                Id = id;
                RequiredSpecial = requiredSpecial;
                DisplayName = displayName;
                EffectText = effectText;
                MaxStack = maxStack;
                Apply = apply;
            }
        }

        private class ApplyContext
        {
            public readonly Character Player;
            public readonly SyringeDartAbility Syringe;
            public readonly PlayerGeneralStatRuntime Runtime;

            private readonly MonoBehaviour logOwner;
            private readonly bool debugLog;

            public ApplyContext(
                Character player,
                SyringeDartAbility syringe,
                PlayerGeneralStatRuntime runtime,
                MonoBehaviour logOwner,
                bool debugLog)
            {
                Player = player;
                Syringe = syringe;
                Runtime = runtime;
                this.logOwner = logOwner;
                this.debugLog = debugLog;
            }

            public void AddMoveSpeedPercent(float percent)
            {
                if (Player == null)
                {
                    return;
                }

                float baseMoveSpeed = 1f;

                if (Player.Blueprint != null)
                {
                    baseMoveSpeed = Player.Blueprint.movespeed;
                }

                Player.AddMoveSpeedBoost(baseMoveSpeed * percent);
            }

            public void MultiplyFloat(string fieldName, float multiplier)
            {
                MultiplyFloat(fieldName, multiplier, float.NegativeInfinity, float.PositiveInfinity);
            }

            public void MultiplyFloat(string fieldName, float multiplier, float min, float max)
            {
                FieldInfo fieldInfo = GetSyringeField(fieldName);

                if (fieldInfo == null)
                {
                    return;
                }

                object value = fieldInfo.GetValue(Syringe);

                if (!(value is float currentValue))
                {
                    LogWarning($"{fieldName} 필드가 float 타입이 아닙니다.");
                    return;
                }

                float nextValue = currentValue * multiplier;
                nextValue = ClampFloat(nextValue, min, max);

                fieldInfo.SetValue(Syringe, nextValue);
            }

            public void AddFloat(string fieldName, float amount)
            {
                AddFloat(fieldName, amount, float.NegativeInfinity, float.PositiveInfinity);
            }

            public void AddFloat(string fieldName, float amount, float min, float max)
            {
                FieldInfo fieldInfo = GetSyringeField(fieldName);

                if (fieldInfo == null)
                {
                    return;
                }

                object value = fieldInfo.GetValue(Syringe);

                if (!(value is float currentValue))
                {
                    LogWarning($"{fieldName} 필드가 float 타입이 아닙니다.");
                    return;
                }

                float nextValue = currentValue + amount;
                nextValue = ClampFloat(nextValue, min, max);

                fieldInfo.SetValue(Syringe, nextValue);
            }

            public void AddInt(string fieldName, int amount, int min, int max)
            {
                FieldInfo fieldInfo = GetSyringeField(fieldName);

                if (fieldInfo == null)
                {
                    return;
                }

                object value = fieldInfo.GetValue(Syringe);

                if (!(value is int currentValue))
                {
                    LogWarning($"{fieldName} 필드가 int 타입이 아닙니다.");
                    return;
                }

                int nextValue = currentValue + amount;
                nextValue = Mathf.Clamp(nextValue, min, max);

                fieldInfo.SetValue(Syringe, nextValue);
            }

            private FieldInfo GetSyringeField(string fieldName)
            {
                if (Syringe == null)
                {
                    return null;
                }

                FieldInfo fieldInfo = Syringe.GetType().GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (fieldInfo == null)
                {
                    LogWarning($"SyringeDartAbility에서 {fieldName} 필드를 찾지 못했습니다.");
                }

                return fieldInfo;
            }

            private float ClampFloat(float value, float min, float max)
            {
                if (!float.IsNegativeInfinity(min) && value < min)
                {
                    value = min;
                }

                if (!float.IsPositiveInfinity(max) && value > max)
                {
                    value = max;
                }

                return value;
            }

            private void LogWarning(string message)
            {
                if (!debugLog)
                {
                    return;
                }

                Debug.LogWarning($"[조건부 일반증강] {message}", logOwner);
            }
        }
    }
}
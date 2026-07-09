using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    // 일반 증강 카드.
    //
    // RollMode.AlwaysGeneralRandomTwo:
    // - 카드가 선택지에 뜰 때 항상 나오는 일반 증강 테이블에서 랜덤 2개를 미리 뽑아 Description에 표시.
    // - 유저가 선택하면 표시된 2개가 그대로 적용된다.
    //
    // RollMode.ConditionalRandomOne:
    // - 현재 보유한 특수/전설 증강을 기준으로 조건부 일반 증강 1개를 미리 뽑아 Description에 표시.
    // - 유저가 선택하면 표시된 1개가 그대로 적용된다.
    public class SyringeGeneralRandomAugmentAbility : Ability
    {
        public enum RollMode
        {
            AlwaysGeneralRandomTwo,
            ConditionalRandomOne
        }

        public enum GeneralAugmentType
        {
            // 항상 나오는 일반 증강
            AcupunctureMastery,
            WeakPointSense,
            DeepNeedling,
            QuickHands,
            ContinuousTreatment,
            NeedleAcceleration,
            NeedleWeightControl,
            NeedleTipPolishing,
            LongNeedleCrafting,
            NeedleExpansion,
            MultiNeedling,
            WristReinforcement,
            HealthTraining,
            Regeneration,
            ImmuneStability,
            EvasionInstinct,
            LightBody,
            InstantJudgement,
            ExtraEnergy,
            EmergencyEvasion,
            SterileTreatment,
            ConcentratedMedicine,
            ExpAbsorption,
            GoldAbsorption,
            CellActivation,
            MoneySense,
            BossDamageTraining,

            // 신규 항상 일반 증강
            LuckyMeridian,

            // 조건부 일반 증강
            ExplosionRadiusControl,
            FragmentDiffusion,
            ReturnMastery,
            CloneMastery,
            CloneSynchronization,
            ExtraClone
        }

        [Header("General Random Augment")]
        [SerializeField] private RollMode rollMode = RollMode.AlwaysGeneralRandomTwo;

        [Tooltip("이 카드가 몇 번까지 다시 등장할 수 있는지 설정합니다.")]
        [SerializeField] private int maxSelections = 999;

        [Tooltip("적용 결과를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private SyringeDartAbility syringeDartAbility;
        private PlayerGeneralStatRuntime statRuntime;

        private readonly Dictionary<GeneralAugmentType, int> stackCounts =
            new Dictionary<GeneralAugmentType, int>();

        // 이번 선택지에 표시될 미리보기 결과.
        // Description에서 미리 뽑고, Select 시점에 이 목록을 그대로 적용한다.
        private readonly List<GeneralAugmentType> previewAugments =
            new List<GeneralAugmentType>();

        private bool previewPrepared = false;

        public override string Description
        {
            get
            {
                PreparePreviewIfNeeded();
                return BuildPreviewDescription();
            }
        }

        public override void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            augmentTier = AugmentTier.General;
            maxLevel = maxSelections;

            syringeDartAbility = abilityManager.GetComponentInChildren<SyringeDartAbility>(true);

            if (syringeDartAbility == null)
            {
                Debug.LogError("[SyringeGeneralRandomAugmentAbility] SyringeDartAbility를 찾지 못했습니다.");
            }

            statRuntime = PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);
        }

        protected override void Use()
        {
            ApplyPreparedAugments();
        }

        protected override void Upgrade()
        {
            ApplyPreparedAugments();
        }

        public override bool RequirementsMet()
        {
            if (level >= maxSelections)
            {
                return false;
            }

            if (syringeDartAbility == null || playerCharacter == null)
            {
                return false;
            }

            List<GeneralAugmentType> candidates = BuildCandidateList();

            if (candidates.Count <= 0)
            {
                return false;
            }

            // 이 Ability가 레벨업 선택지 후보로 다시 검사될 때마다
            // 이전에 보여줬지만 선택되지 않은 미리보기 결과를 초기화한다.
            ClearPreview();

            return true;
        }

        private void PreparePreviewIfNeeded()
        {
            if (previewPrepared)
            {
                return;
            }

            previewAugments.Clear();

            List<GeneralAugmentType> candidates = BuildCandidateList();

            if (candidates.Count <= 0)
            {
                previewPrepared = true;
                return;
            }

            int rollCount = rollMode == RollMode.AlwaysGeneralRandomTwo ? 2 : 1;
            List<GeneralAugmentType> selected = PickRandomUnique(candidates, rollCount);

            for (int i = 0; i < selected.Count; i++)
            {
                previewAugments.Add(selected[i]);
            }

            previewPrepared = true;
        }

        private void ApplyPreparedAugments()
        {
            if (syringeDartAbility == null || playerCharacter == null)
            {
                return;
            }

            if (statRuntime == null)
            {
                statRuntime = PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);
            }

            // UI Description이 한 번도 읽히지 않은 예외 상황에서도 안전하게 작동하도록 보장.
            PreparePreviewIfNeeded();

            if (previewAugments.Count <= 0)
            {
                Debug.LogWarning("[일반 증강] 적용 가능한 미리보기 증강이 없습니다.");
                ClearPreview();
                return;
            }

            for (int i = 0; i < previewAugments.Count; i++)
            {
                GeneralAugmentType augmentType = previewAugments[i];

                ApplyAugment(augmentType);
                AddStack(augmentType);

                

                if (debugLog)
                {
                    Debug.Log($"[일반 증강] {GetDisplayName(augmentType)} 적용 | {GetEffectText(augmentType)}");
                }
            }

            ClearPreview();
        }

        private string BuildPreviewDescription()
        {
            PreparePreviewIfNeeded();

            if (previewAugments.Count <= 0)
            {
                if (rollMode == RollMode.ConditionalRandomOne)
                {
                    return "현재 보유한 특수/전설 증강 중 강화 가능한 대상이 없습니다.";
                }

                return "현재 적용 가능한 일반 증강이 없습니다.";
            }

            string description = "";
            for (int i = 0; i < previewAugments.Count; i++)
            {
                GeneralAugmentType augmentType = previewAugments[i];

                description += "- ";
                description += GetDisplayName(augmentType);
                description += ": ";
                description += GetEffectText(augmentType);

                if (i < previewAugments.Count - 1)
                {
                    description += "\n";
                }
            }

            return description;
        }

        private void ClearPreview()
        {
            previewAugments.Clear();
            previewPrepared = false;
        }

        private List<GeneralAugmentType> BuildCandidateList()
        {
            if (rollMode == RollMode.AlwaysGeneralRandomTwo)
            {
                return BuildAlwaysGeneralCandidates();
            }

            return BuildConditionalCandidates();
        }

        private List<GeneralAugmentType> BuildAlwaysGeneralCandidates()
        {
            List<GeneralAugmentType> candidates = new List<GeneralAugmentType>
            {
                GeneralAugmentType.AcupunctureMastery,
                GeneralAugmentType.WeakPointSense,
                GeneralAugmentType.DeepNeedling,
                GeneralAugmentType.QuickHands,
                GeneralAugmentType.ContinuousTreatment,
                GeneralAugmentType.NeedleAcceleration,
                GeneralAugmentType.NeedleWeightControl,
                GeneralAugmentType.NeedleTipPolishing,
                GeneralAugmentType.LongNeedleCrafting,
                GeneralAugmentType.NeedleExpansion,
                GeneralAugmentType.MultiNeedling,
                GeneralAugmentType.WristReinforcement,
                GeneralAugmentType.HealthTraining,
                GeneralAugmentType.Regeneration,
                GeneralAugmentType.ImmuneStability,
                GeneralAugmentType.EvasionInstinct,
                GeneralAugmentType.LightBody,
                GeneralAugmentType.InstantJudgement,
                GeneralAugmentType.ExtraEnergy,
                GeneralAugmentType.EmergencyEvasion,
                GeneralAugmentType.SterileTreatment,
                GeneralAugmentType.ConcentratedMedicine,
                GeneralAugmentType.ExpAbsorption,
                GeneralAugmentType.GoldAbsorption,
                GeneralAugmentType.CellActivation,
                GeneralAugmentType.MoneySense,
                GeneralAugmentType.BossDamageTraining,

                // 행운 증가 일반 증강
                GeneralAugmentType.LuckyMeridian
            };

            RemoveMaxedCandidates(candidates);
            return candidates;
        }

        private List<GeneralAugmentType> BuildConditionalCandidates()
        {
            List<GeneralAugmentType> candidates = new List<GeneralAugmentType>();

            if (syringeDartAbility.HasExplosionAugment())
            {
                candidates.Add(GeneralAugmentType.ExplosionRadiusControl);
                candidates.Add(GeneralAugmentType.FragmentDiffusion);
            }

            if (syringeDartAbility.HasReturnNeedleAugment())
            {
                candidates.Add(GeneralAugmentType.ReturnMastery);
            }

            if (syringeDartAbility.HasCloneLegendary())
            {
                candidates.Add(GeneralAugmentType.CloneMastery);
                candidates.Add(GeneralAugmentType.CloneSynchronization);
                candidates.Add(GeneralAugmentType.ExtraClone);
            }

            RemoveMaxedCandidates(candidates);
            return candidates;
        }

        private void RemoveMaxedCandidates(List<GeneralAugmentType> candidates)
        {
            for (int i = candidates.Count - 1; i >= 0; i--)
            {
                if (IsMaxed(candidates[i]))
                {
                    candidates.RemoveAt(i);
                }
            }
        }

        private List<GeneralAugmentType> PickRandomUnique(List<GeneralAugmentType> candidates, int count)
        {
            List<GeneralAugmentType> pool = new List<GeneralAugmentType>(candidates);
            List<GeneralAugmentType> result = new List<GeneralAugmentType>();

            int safeCount = Mathf.Min(count, pool.Count);

            for (int i = 0; i < safeCount; i++)
            {
                int index = Random.Range(0, pool.Count);
                result.Add(pool[index]);
                pool.RemoveAt(index);
            }

            return result;
        }

        private void ApplyAugment(GeneralAugmentType augmentType)
        {
            switch (augmentType)
            {
                case GeneralAugmentType.AcupunctureMastery:
                    playerCharacter.AddDamageMultiplier(0.10f);
                    break;

                case GeneralAugmentType.WeakPointSense:
                    playerCharacter.AddCritChance(0.05f);
                    break;

                case GeneralAugmentType.DeepNeedling:
                    statRuntime.AddCritDamageMultiplier(0.15f);
                    break;

                case GeneralAugmentType.QuickHands:
                    playerCharacter.AddAttackSpeed(0.06f);
                    break;

                case GeneralAugmentType.ContinuousTreatment:
                    playerCharacter.AddAttackSpeed(0.08f);
                    break;

                case GeneralAugmentType.NeedleAcceleration:
                    playerCharacter.AddProjectileSpeed(0.12f);
                    break;

                case GeneralAugmentType.NeedleWeightControl:
                    statRuntime.AddKnockbackMultiplier(0.15f);
                    break;

                case GeneralAugmentType.NeedleTipPolishing:
                    statRuntime.AddDefensePierce(0.05f);
                    break;

                case GeneralAugmentType.LongNeedleCrafting:
                    playerCharacter.AddRangeBoost(0.10f);
                    break;

                case GeneralAugmentType.NeedleExpansion:
                    playerCharacter.AddProjectileSize(0.08f);
                    break;

                case GeneralAugmentType.MultiNeedling:
                    playerCharacter.AddProjectileCount(1);
                    break;

                case GeneralAugmentType.WristReinforcement:
                    playerCharacter.AddAttackSpeed(0.05f);
                    break;

                case GeneralAugmentType.HealthTraining:
                    playerCharacter.AddMaxHealthBonus(10f);
                    break;

                case GeneralAugmentType.Regeneration:
                    playerCharacter.AddHealOnIdle(0.2f);
                    break;

                case GeneralAugmentType.ImmuneStability:
                    statRuntime.AddDamageReduction(0.05f);
                    break;

                case GeneralAugmentType.EvasionInstinct:
                    statRuntime.AddEvasion(0.04f);
                    break;

                case GeneralAugmentType.LightBody:
                    AddMoveSpeedPercent(0.06f);
                    break;

                case GeneralAugmentType.InstantJudgement:
                    playerCharacter.AddDashRechargeSpeed(0.08f);
                    break;

                case GeneralAugmentType.ExtraEnergy:
                    playerCharacter.AddDashCharge(1);
                    break;

                case GeneralAugmentType.EmergencyEvasion:
                    playerCharacter.AddDashDistance(0.28f);
                    break;

                case GeneralAugmentType.SterileTreatment:
                    statRuntime.AddStatusDurationMultiplier(0.08f);
                    MultiplyPrivateFloat(syringeDartAbility, "poisonDuration", 1.08f);
                    MultiplyPrivateFloat(syringeDartAbility, "honeyDuration", 1.08f);
                    break;

                case GeneralAugmentType.ConcentratedMedicine:
                    statRuntime.AddStatusDamageMultiplier(0.10f);
                    MultiplyPrivateFloat(syringeDartAbility, "poisonTickDamage", 1.10f);
                    break;

                case GeneralAugmentType.ExpAbsorption:
                    playerCharacter.AddExpMultiplier(0.08f);
                    break;

                case GeneralAugmentType.GoldAbsorption:
                    statRuntime.AddGoldGainMultiplier(0.08f);
                    break;

                case GeneralAugmentType.CellActivation:
                    statRuntime.AddPickupRangeMultiplier(0.12f);
                    break;

                case GeneralAugmentType.MoneySense:
                    statRuntime.AddGoldDropChance(0.05f);
                    break;

                case GeneralAugmentType.BossDamageTraining:
                    statRuntime.AddBossDamageMultiplier(0.10f);
                    break;

                case GeneralAugmentType.LuckyMeridian:
                    // Character.Luck = characterBlueprint.luck * luckMultiplier 구조를 그대로 사용한다.
                    // 기본 luck이 1이라면 AddLuck(1f) 후 Luck은 2가 되고,
                    // AbilityManager의 기존 공식에 의해 일반 -3%, 특수 +2%, 전설 +1% 효과가 적용된다.
                    playerCharacter.AddLuck(1f);
                    break;

                case GeneralAugmentType.ExplosionRadiusControl:
                    MultiplyPrivateFloat(syringeDartAbility, "explosionRadius", 1.08f);
                    break;

                case GeneralAugmentType.FragmentDiffusion:
                    MultiplyPrivateFloat(syringeDartAbility, "explosionDamage", 1.10f);
                    break;

                case GeneralAugmentType.ReturnMastery:
                    MultiplyPrivateFloat(syringeDartAbility, "returnNeedleDamageMultiplier", 1.10f);
                    break;

                case GeneralAugmentType.CloneMastery:
                    statRuntime.AddCloneDamageMultiplier(0.10f);
                    break;

                case GeneralAugmentType.CloneSynchronization:
                    statRuntime.AddCloneAttackSpeedMultiplier(0.08f);
                    break;

                case GeneralAugmentType.ExtraClone:
                    statRuntime.AddExtraCloneCount(1);
                    SyringeCloneController.Create(playerCharacter, entityManager, syringeDartAbility);
                    break;
            }
        }

        private void AddMoveSpeedPercent(float percent)
        {
            float baseMoveSpeed = 1f;

            if (playerCharacter != null && playerCharacter.Blueprint != null)
            {
                baseMoveSpeed = playerCharacter.Blueprint.movespeed;
            }

            playerCharacter.AddMoveSpeedBoost(baseMoveSpeed * percent);
        }

        private void MultiplyPrivateFloat(object target, string fieldName, float multiplier)
        {
            if (target == null)
            {
                return;
            }

            FieldInfo fieldInfo = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );

            if (fieldInfo == null)
            {
                Debug.LogWarning($"[일반 증강] {target.GetType().Name}에서 {fieldName} 필드를 찾지 못했습니다.");
                return;
            }

            object value = fieldInfo.GetValue(target);

            if (!(value is float currentValue))
            {
                Debug.LogWarning($"[일반 증강] {fieldName} 필드가 float 타입이 아닙니다.");
                return;
            }

            fieldInfo.SetValue(target, currentValue * multiplier);
        }

        private void AddStack(GeneralAugmentType augmentType)
        {
            if (!stackCounts.ContainsKey(augmentType))
            {
                stackCounts[augmentType] = 0;
            }

            stackCounts[augmentType]++;
        }

        private bool IsMaxed(GeneralAugmentType augmentType)
        {
            int maxStack = GetMaxStack(augmentType);

            if (maxStack <= 0)
            {
                return false;
            }

            int currentStack = stackCounts.ContainsKey(augmentType)
                ? stackCounts[augmentType]
                : 0;

            return currentStack >= maxStack;
        }

        private int GetMaxStack(GeneralAugmentType augmentType)
        {
            switch (augmentType)
            {
                case GeneralAugmentType.MultiNeedling:
                    return 5;

                case GeneralAugmentType.ExtraEnergy:
                    return 3;

                case GeneralAugmentType.EmergencyEvasion:
                    return 5;

                case GeneralAugmentType.ExtraClone:
                    return 2;

                case GeneralAugmentType.MoneySense:
                    return 10;

                case GeneralAugmentType.CellActivation:
                    return 8;

                case GeneralAugmentType.BossDamageTraining:
                    return 10;

                case GeneralAugmentType.LuckyMeridian:
                    return 10;

                default:
                    return 0;
            }
        }

        private string GetDisplayName(GeneralAugmentType augmentType)
        {
            switch (augmentType)
            {
                case GeneralAugmentType.AcupunctureMastery: return "침술 숙련";
                case GeneralAugmentType.WeakPointSense: return "급소 감각";
                case GeneralAugmentType.DeepNeedling: return "깊은 자침";
                case GeneralAugmentType.QuickHands: return "빠른 손놀림";
                case GeneralAugmentType.ContinuousTreatment: return "연속 시술";
                case GeneralAugmentType.NeedleAcceleration: return "침 가속";
                case GeneralAugmentType.NeedleWeightControl: return "침 무게 조절";
                case GeneralAugmentType.NeedleTipPolishing: return "침끝 연마";
                case GeneralAugmentType.LongNeedleCrafting: return "장침 제작";
                case GeneralAugmentType.NeedleExpansion: return "침 확대";
                case GeneralAugmentType.MultiNeedling: return "다침술";
                case GeneralAugmentType.WristReinforcement: return "손목 강화";
                case GeneralAugmentType.HealthTraining: return "체력 단련";
                case GeneralAugmentType.Regeneration: return "재생력";
                case GeneralAugmentType.ImmuneStability: return "면역 안정";
                case GeneralAugmentType.EvasionInstinct: return "회피 본능";
                case GeneralAugmentType.LightBody: return "가벼운 몸";
                case GeneralAugmentType.InstantJudgement: return "순간 판단";
                case GeneralAugmentType.ExtraEnergy: return "여분 기력";
                case GeneralAugmentType.EmergencyEvasion: return "긴급 회피";
                case GeneralAugmentType.SterileTreatment: return "무균 시술";
                case GeneralAugmentType.ConcentratedMedicine: return "농축 약효";
                case GeneralAugmentType.ExpAbsorption: return "경험 흡수";
                case GeneralAugmentType.GoldAbsorption: return "골드 흡수";
                case GeneralAugmentType.CellActivation: return "세포 활성";
                case GeneralAugmentType.MoneySense: return "금전 감각";
                case GeneralAugmentType.BossDamageTraining: return "보스 해부";
                case GeneralAugmentType.LuckyMeridian: return "행운의 맥";
                case GeneralAugmentType.ExplosionRadiusControl: return "폭심 조절";
                case GeneralAugmentType.FragmentDiffusion: return "파편 확산";
                case GeneralAugmentType.ReturnMastery: return "회수 숙련";
                case GeneralAugmentType.CloneMastery: return "분신 숙련";
                case GeneralAugmentType.CloneSynchronization: return "분신 동조";
                case GeneralAugmentType.ExtraClone: return "분신 증식";
                default: return augmentType.ToString();
            }
        }

        private string GetEffectText(GeneralAugmentType augmentType)
        {
            switch (augmentType)
            {
                case GeneralAugmentType.AcupunctureMastery: return "침 피해 +10%";
                case GeneralAugmentType.WeakPointSense: return "치명타 확률 +5%";
                case GeneralAugmentType.DeepNeedling: return "치명타 피해 +15%";
                case GeneralAugmentType.QuickHands: return "공격속도 +6%";
                case GeneralAugmentType.ContinuousTreatment: return "공격속도 +8%";
                case GeneralAugmentType.NeedleAcceleration: return "투사체 속도 +12%";
                case GeneralAugmentType.NeedleWeightControl: return "넉백 +15%";
                case GeneralAugmentType.NeedleTipPolishing: return "방어 관통 +5%";
                case GeneralAugmentType.LongNeedleCrafting: return "사거리 +10%";
                case GeneralAugmentType.NeedleExpansion: return "투사체 크기 +8%";
                case GeneralAugmentType.MultiNeedling: return "투사체 +1";
                case GeneralAugmentType.WristReinforcement: return "발사 간격 -5%";
                case GeneralAugmentType.HealthTraining: return "최대 HP +10";
                case GeneralAugmentType.Regeneration: return "정지 시 초당 회복 +0.2";
                case GeneralAugmentType.ImmuneStability: return "받는 피해 감소 +5%";
                case GeneralAugmentType.EvasionInstinct: return "회피율 +4%";
                case GeneralAugmentType.LightBody: return "이동속도 +6%";
                case GeneralAugmentType.InstantJudgement: return "대쉬 재충전 시간 감소";
                case GeneralAugmentType.ExtraEnergy: return "대쉬 충전 +1";
                case GeneralAugmentType.EmergencyEvasion: return "대쉬 거리 +8%";
                case GeneralAugmentType.SterileTreatment: return "상태이상 지속시간 +8%";
                case GeneralAugmentType.ConcentratedMedicine: return "독/지속 피해 +10%";
                case GeneralAugmentType.ExpAbsorption: return "경험치 획득량 +8%";
                case GeneralAugmentType.GoldAbsorption: return "골드 획득량 +8%";
                case GeneralAugmentType.CellActivation: return "픽업 범위 +12%";
                case GeneralAugmentType.MoneySense: return "골드 드롭 확률 +5%";
                case GeneralAugmentType.BossDamageTraining: return "보스 피해 +10%";
                case GeneralAugmentType.LuckyMeridian: return "행운 +1. 일반 -3%, 특수 +2%, 전설 +1%";
                case GeneralAugmentType.ExplosionRadiusControl: return "폭발 반경 +8%";
                case GeneralAugmentType.FragmentDiffusion: return "폭발 피해 +10%";
                case GeneralAugmentType.ReturnMastery: return "귀환 피해 +10%";
                case GeneralAugmentType.CloneMastery: return "분신 피해 +10%";
                case GeneralAugmentType.CloneSynchronization: return "분신 공격속도 +8%";
                case GeneralAugmentType.ExtraClone: return "분신 +1";
                default: return "";
            }
        }
    }
}
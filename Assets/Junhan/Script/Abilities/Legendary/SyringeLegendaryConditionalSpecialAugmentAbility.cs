using System;
using System.Collections.Generic;
using UnityEngine;

using LegendaryType = Vampire.SyringeLegendaryAugmentAbility.LegendaryAugmentType;

namespace Vampire
{
    /// <summary>
    /// 전설증강을 보유했을 때만 등장하는 조건부 특수증강 카드입니다.
    ///
    /// 규칙:
    /// - 전설증강을 먹은 뒤에만 등장합니다.
    /// - 조건부 특수증강은 업그레이드되지 않습니다.
    /// - 각 조건부 특수증강은 1회만 획득할 수 있습니다.
    /// - 카드 이미지는 조건이 되는 원본 전설증강 스프라이트를 공유합니다.
    /// </summary>
    public class SyringeLegendaryConditionalSpecialAugmentAbility : Ability
    {
        [Serializable]
        private class LegendarySourceIconEntry
        {
            [Tooltip("이 아이콘을 사용할 조건 전설증강 종류입니다.")]
            public LegendaryType legendaryType;

            [Tooltip("원본 전설증강 Ability 프리팹/컴포넌트입니다. 여기의 Image를 조건부 카드 아이콘으로 공유합니다.")]
            public Ability sourceAbility;

            [Tooltip("sourceAbility가 비어 있을 때 사용할 예비 스프라이트입니다.")]
            public Sprite fallbackSprite;
        }

        private class Definition
        {
            public readonly string id;
            public readonly LegendaryType requiredLegendary;
            public readonly string displayName;
            public readonly string effectText;
            public readonly Action<LegendaryConditionalSpecialRuntime> apply;

            public Definition(
                string id,
                LegendaryType requiredLegendary,
                string displayName,
                string effectText,
                Action<LegendaryConditionalSpecialRuntime> apply)
            {
                this.id = id;
                this.requiredLegendary = requiredLegendary;
                this.displayName = displayName;
                this.effectText = effectText;
                this.apply = apply;
            }
        }

        [Header("Legendary Conditional Special Augment")]
        [Tooltip("선택된 조건부 특수증강 적용 결과를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Header("Source Sprite Sharing")]
        [Tooltip("조건이 되는 원본 전설증강의 Ability 프리팹을 연결합니다.")]
        [SerializeField]
        private List<LegendarySourceIconEntry> sourceIconEntries =
            new List<LegendarySourceIconEntry>();

        private SyringeDartAbility syringeDartAbility;
        private LegendaryConditionalSpecialRuntime runtime;

        private Definition previewDefinition;
        private bool previewPrepared;

        public override Sprite Image
        {
            get
            {
                PreparePreviewIfNeeded();

                if (previewDefinition == null)
                {
                    return base.Image;
                }

                Sprite sourceSprite = ResolveSourceSprite(previewDefinition.requiredLegendary);
                return sourceSprite != null ? sourceSprite : base.Image;
            }
        }

        public override string Name
        {
            get
            {
                PreparePreviewIfNeeded();

                if (previewDefinition == null)
                {
                    return "전설 연계 특수증강";
                }

                return previewDefinition.displayName;
            }
        }

        public override string Description
        {
            get
            {
                PreparePreviewIfNeeded();

                if (previewDefinition == null)
                {
                    return "현재 보유한 전설증강 중 강화 가능한 조건부 특수증강이 없습니다.";
                }

                return
                    $"[{GetLegendaryDisplayName(previewDefinition.requiredLegendary)} 연계 특수증강]\n" +
                    previewDefinition.effectText;
            }
        }

        public override void Init(
    AbilityManager abilityManager,
    EntityManager entityManager,
    Character playerCharacter)
        {
            base.Init(abilityManager, entityManager, playerCharacter);

            augmentTier = AugmentTier.Special;

            // 중요:
            // 이 Ability 프리팹 자체는 여러 조건부 특수증강을 대표하는 "랜덤 카드 컨테이너"입니다.
            // 따라서 maxLevel = 1로 두면 전체 카드가 1번만 등장하고 끝납니다.
            // 개별 증강의 1회 제한은 runtime.HasAcquired(definition.id)로 처리합니다.
            maxLevel = 999;

            // 중요:
            // 첫 선택 이후 이 Ability는 owned 상태가 됩니다.
            // 이후에도 남은 조건부 특수증강을 보여주려면 owned upgrade 후보로 다시 들어와야 합니다.
            canAppearAsOwnedUpgrade = true;

            RefreshSyringeDartAbilityReference();
            runtime = LegendaryConditionalSpecialRuntime.GetOrCreate(playerCharacter);

            if (syringeDartAbility == null)
            {
                Debug.LogError("[전설 조건부 특수증강] SyringeDartAbility를 찾지 못했습니다.", this);
            }
        }

        protected override void Use()
        {
            ApplyPreparedAugment();
        }

        protected override void Upgrade()
        {
            // 이 Ability 자체는 owned 상태로 다시 등장하지만,
            // 실제로는 "업그레이드"가 아니라 아직 획득하지 않은 조건부 특수증강 1개를 새로 획득하는 구조입니다.
            ApplyPreparedAugment();
        }

        public override bool RequirementsMet()
        {
            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null || playerCharacter == null)
            {
                return false;
            }

            if (runtime == null)
            {
                runtime = LegendaryConditionalSpecialRuntime.GetOrCreate(playerCharacter);
            }

            if (runtime == null)
            {
                return false;
            }

            // level로 막지 않습니다.
            // 이 Ability 컨테이너가 몇 번 선택됐는지가 아니라,
            // 현재 보유 전설증강 기준으로 아직 안 먹은 조건부 특수증강이 남아 있는지가 핵심입니다.
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

            previewPrepared = true;
            previewDefinition = null;

            List<Definition> candidates = BuildCandidateList();

            if (candidates.Count <= 0)
            {
                return;
            }

            int index = UnityEngine.Random.Range(0, candidates.Count);
            previewDefinition = candidates[index];
        }

        private void ApplyPreparedAugment()
        {
            RefreshSyringeDartAbilityReference();

            if (syringeDartAbility == null || playerCharacter == null)
            {
                return;
            }

            if (runtime == null)
            {
                runtime = LegendaryConditionalSpecialRuntime.GetOrCreate(playerCharacter);
            }

            if (runtime == null)
            {
                return;
            }

            PreparePreviewIfNeeded();

            if (previewDefinition == null)
            {
                Debug.LogWarning("[전설 조건부 특수증강] 적용 가능한 미리보기 증강이 없습니다.", this);
                ClearPreview();
                return;
            }

            bool acquired = runtime.TryAcquire(previewDefinition.id);

            if (!acquired)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        $"[전설 조건부 특수증강] 이미 획득한 증강입니다. ID={previewDefinition.id}",
                        this);
                }

                ClearPreview();
                return;
            }

            previewDefinition.apply?.Invoke(runtime);

            if (debugLog)
            {
                Debug.Log(
                    $"[전설 조건부 특수증강] {previewDefinition.displayName} 적용 | " +
                    $"{previewDefinition.effectText} | 조건={previewDefinition.requiredLegendary}",
                    this);
            }

            ClearPreview();
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

        private List<Definition> BuildCandidateList()
        {
            List<Definition> candidates = new List<Definition>();
            List<Definition> allDefinitions = GetAllDefinitions();

            if (runtime == null)
            {
                runtime = LegendaryConditionalSpecialRuntime.GetOrCreate(playerCharacter);
            }

            if (runtime == null)
            {
                return candidates;
            }

            for (int i = 0; i < allDefinitions.Count; i++)
            {
                Definition definition = allDefinitions[i];

                if (!HasRequiredLegendary(definition.requiredLegendary))
                {
                    continue;
                }

                if (runtime.HasAcquired(definition.id))
                {
                    continue;
                }

                candidates.Add(definition);
            }

            return candidates;
        }

        private bool HasRequiredLegendary(LegendaryType legendaryType)
        {
            if (syringeDartAbility == null)
            {
                return false;
            }

            switch (legendaryType)
            {
                case LegendaryType.LifeBurn:
                    return syringeDartAbility.HasLifeBurnLegendary();

                case LegendaryType.CloneCulture:
                    return syringeDartAbility.HasCloneLegendary();

                case LegendaryType.HedgehogNeedle:
                    return syringeDartAbility.HasHedgehogNeedleLegendary();

                case LegendaryType.HeavySnipe:
                    return syringeDartAbility.HasHeavySnipeLegendary();

                case LegendaryType.CursorControl:
                    return syringeDartAbility.HasCursorControlLegendary();

                case LegendaryType.NeuralBlock:
                    return syringeDartAbility.HasNeuralBlockLegendary();

                case LegendaryType.OrganCompression:
                    return syringeDartAbility.HasOrganCompressionLegendary();

                case LegendaryType.GastricPeristalsisWave:
                    return syringeDartAbility.HasGastricPeristalsisWaveAugment();

                case LegendaryType.MucosalFortress:
                    return syringeDartAbility.HasMucosalFortressAugment();

                case LegendaryType.HungrySpirit:
                    return syringeDartAbility.HasHungrySpiritLegendary();

                case LegendaryType.NeedleShotgun:
                    return syringeDartAbility.HasNeedleShotgunLegendary();

                // 독 전염은 이번 조건부 특수증강 풀에서 제외합니다.
                case LegendaryType.PoisonContagion:
                    return false;

                default:
                    return false;
            }
        }

        private Sprite ResolveSourceSprite(LegendaryType legendaryType)
        {
            for (int i = 0; i < sourceIconEntries.Count; i++)
            {
                LegendarySourceIconEntry entry = sourceIconEntries[i];

                if (entry == null)
                {
                    continue;
                }

                if (entry.legendaryType != legendaryType)
                {
                    continue;
                }

                if (entry.sourceAbility != null && entry.sourceAbility.Image != null)
                {
                    return entry.sourceAbility.Image;
                }

                if (entry.fallbackSprite != null)
                {
                    return entry.fallbackSprite;
                }
            }

            return null;
        }

        private string GetLegendaryDisplayName(LegendaryType legendaryType)
        {
            switch (legendaryType)
            {
                case LegendaryType.LifeBurn:
                    return "생명연소";

                case LegendaryType.CloneCulture:
                    return "분신배양";

                case LegendaryType.HedgehogNeedle:
                    return "고슴도침";

                case LegendaryType.HeavySnipe:
                    return "대물침";

                case LegendaryType.CursorControl:
                    return "이기어침";

                case LegendaryType.NeuralBlock:
                    return "신경차단";

                case LegendaryType.OrganCompression:
                    return "장기압착";

                case LegendaryType.GastricPeristalsisWave:
                    return "위산 연동파";

                case LegendaryType.MucosalFortress:
                    return "점막 요새";

                case LegendaryType.HungrySpirit:
                    return "헝그리정신";

                case LegendaryType.NeedleShotgun:
                    return "샷건침";

                default:
                    return "전설증강";
            }
        }

        private void ClearPreview()
        {
            previewDefinition = null;
            previewPrepared = false;
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
            List<Definition> definitions = new List<Definition>();

            // ------------------------------------------------------------
            // LifeBurn / 생명연소 조건부 특수증강 4개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "LifeBurn_DesperateNeedleTip",
                LegendaryType.LifeBurn,
                "절박한 침끝",
                "플레이어 가까이에 있는 적에게 침 피해가 증가합니다.",
                r => r.lifeBurnDesperateNeedleTip = true));

            definitions.Add(new Definition(
                "LifeBurn_OneHitEvade",
                LegendaryType.LifeBurn,
                "한 방 회피",
                "몬스터를 100마리 처치하면 1회 피해를 무시하는 실드가 생깁니다.",
                r => r.lifeBurnOneHitEvade = true));

            definitions.Add(new Definition(
                "LifeBurn_BloodFlowBlock",
                LegendaryType.LifeBurn,
                "혈류 차단",
                "HP 회복 판정이 발생하면 회복 대신 공격속도 스택을 얻습니다. 최대 10스택.",
                r => r.lifeBurnBloodFlowBlock = true));

            definitions.Add(new Definition(
                "LifeBurn_StrongHeart",
                LegendaryType.LifeBurn,
                "강심장",
                "움직이지 않는 시간마다 데미지 스택이 증가합니다. 최대 10스택, 1~3배.",
                r => r.lifeBurnStrongHeart = true));

            // ------------------------------------------------------------
            // CloneCulture / 분신배양 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Clone_BloodFlowLink",
                LegendaryType.CloneCulture,
                "분신 혈류 연결",
                "분신이 적을 맞히면 본체 공격속도가 짧은 시간 동안 증가합니다.",
                r => r.cloneBloodFlowLink = true));

            definitions.Add(new Definition(
                "Clone_MultiShadowClone",
                LegendaryType.CloneCulture,
                "다중그림자분신술",
                "분신 수가 1 증가합니다.",
                r => r.cloneMultiShadowClone = true));

            definitions.Add(new Definition(
                "Clone_TwinNeedleCoop",
                LegendaryType.CloneCulture,
                "쌍침 협공",
                "본체와 분신이 같은 적을 공격하면 추가 피해를 줍니다.",
                r => r.cloneTwinNeedleCoop = true));

            // ------------------------------------------------------------
            // HedgehogNeedle / 고슴도침 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Hedgehog_SpineRecoil",
                LegendaryType.HedgehogNeedle,
                "가시 반동",
                "전방위 발사 시 주변 적 넉백이 증가합니다.",
                r => r.hedgehogSpineRecoil = true));

            definitions.Add(new Definition(
                "Hedgehog_NeedleDensity",
                LegendaryType.HedgehogNeedle,
                "침가시 밀도 증가",
                "전방위 발사 침 개수가 증가합니다.",
                r => r.hedgehogNeedleDensity = true));

            definitions.Add(new Definition(
                "Hedgehog_MultiLayerSpines",
                LegendaryType.HedgehogNeedle,
                "다층 가시",
                "전방위 발사가 두 겹으로 나가지만 쿨타임이 증가합니다.",
                r => r.hedgehogMultiLayerSpines = true));

            // ------------------------------------------------------------
            // HeavySnipe / 대물침 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "HeavySnipe_WeaknessPiercingLine",
                LegendaryType.HeavySnipe,
                "약점 관통선",
                "대물침 경로에 선이 남고, 선 위로 발사한 침 피해/속도가 증가합니다.",
                r => r.heavySnipeWeaknessPiercingLine = true));

            definitions.Add(new Definition(
                "HeavySnipe_OverchargeSuppress",
                LegendaryType.HeavySnipe,
                "과충전 억제",
                "최대 차지 시간이 줄고, 중간 차지 피해 효율이 증가합니다.",
                r => r.heavySnipeOverchargeSuppress = true));

            definitions.Add(new Definition(
                "HeavySnipe_SniperBreathing",
                LegendaryType.HeavySnipe,
                "저격 호흡",
                "차지 중 피격당하지 않으면 치명타 확률이 증가합니다.",
                r => r.heavySnipeSniperBreathing = true));

            // ------------------------------------------------------------
            // CursorControl / 이기어침 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Cursor_MucosalOrbitLubrication",
                LegendaryType.CursorControl,
                "점막 궤도 윤활",
                "궤도 속도가 증가하고, 같은 적 재타격 간격이 감소합니다.",
                r => r.cursorMucosalOrbitLubrication = true));

            definitions.Add(new Definition(
                "Cursor_OrbitReverse",
                LegendaryType.CursorControl,
                "궤도 반전",
                "일정 시간마다 궤도 진행 방향이 반전되고, 반전 직후 피해가 증가합니다.",
                r => r.cursorOrbitReverse = true));

            definitions.Add(new Definition(
                "Cursor_OrbitExpansion",
                LegendaryType.CursorControl,
                "궤도 확장",
                "무한궤도 좌우 반경과 침 속도가 증가합니다.",
                r => r.cursorOrbitExpansion = true));

            // ------------------------------------------------------------
            // NeuralBlock / 신경차단 조건부 특수증강 4개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Neural_BlockMark",
                LegendaryType.NeuralBlock,
                "차단 표식",
                "차단된 적이 받는 피해가 증가합니다.",
                r => r.neuralBlockMark = true));

            definitions.Add(new Definition(
                "Neural_ShortAndFrequent",
                LegendaryType.NeuralBlock,
                "짧고 자주",
                "차단 지속시간이 감소하고 발동 주기도 감소합니다.",
                r => r.neuralShortAndFrequent = true));

            definitions.Add(new Definition(
                "Neural_LongAndRare",
                LegendaryType.NeuralBlock,
                "길고 드물게",
                "차단 지속시간이 증가하고 발동 주기도 증가합니다.",
                r => r.neuralLongAndRare = true));

            definitions.Add(new Definition(
                "Neural_Overload",
                LegendaryType.NeuralBlock,
                "신경 과부하",
                "차단 종료 후 대상이 짧게 느려집니다.",
                r => r.neuralOverload = true));

            // ------------------------------------------------------------
            // OrganCompression / 장기압착 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Organ_CompressionEnhance",
                LegendaryType.OrganCompression,
                "압착 강화",
                "압착 범위와 피해가 증가합니다.",
                r => r.organCompressionEnhance = true));

            definitions.Add(new Definition(
                "Organ_CompressionRecovery",
                LegendaryType.OrganCompression,
                "압착 회수",
                "압착으로 다수 처치 시 즉시 압착이 재발동됩니다.",
                r => r.organCompressionRecovery = true));

            definitions.Add(new Definition(
                "Organ_CompressionCrack",
                LegendaryType.OrganCompression,
                "압착균열",
                "장기압착 쿨타임이 감소합니다.",
                r => r.organCompressionCrack = true));

            // ------------------------------------------------------------
            // GastricPeristalsisWave / 위산 연동파 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Gastric_InnerWaveEnhance",
                LegendaryType.GastricPeristalsisWave,
                "안쪽 파동 강화",
                "가까운 적에게 파동 피해가 증가합니다.",
                r => r.gastricInnerWaveEnhance = true));

            definitions.Add(new Definition(
                "Gastric_WaveRecovery",
                LegendaryType.GastricPeristalsisWave,
                "연동 회수",
                "파동으로 처치 시 다음 파동 쿨타임이 감소합니다.",
                r => r.gastricWaveRecovery = true));

            definitions.Add(new Definition(
                "Gastric_DoubleWave",
                LegendaryType.GastricPeristalsisWave,
                "이중 파동",
                "위산 연동파 발생 후 작은 위산 연동파가 1회 추가 발생합니다.",
                r => r.gastricDoubleWave = true));

            // ------------------------------------------------------------
            // MucosalFortress / 점막 요새 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Mucosal_CounterNeedle",
                LegendaryType.MucosalFortress,
                "점막 반격침",
                "실드가 깨질 때 사방으로 침을 발사합니다.",
                r => r.mucosalCounterNeedle = true));

            definitions.Add(new Definition(
                "Mucosal_FortressFocus",
                LegendaryType.MucosalFortress,
                "요새 집중",
                "실드 최대치일 때 피해가 증가하고, 피격 시 해제됩니다.",
                r => r.mucosalFortressFocus = true));

            definitions.Add(new Definition(
                "Mucosal_DefenseInstinct",
                LegendaryType.MucosalFortress,
                "방어 본능",
                "실드가 없을 때 이동속도가 증가합니다.",
                r => r.mucosalDefenseInstinct = true));

            // ------------------------------------------------------------
            // HungrySpirit / 헝그리정신 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Hungry_DigestionHold",
                LegendaryType.HungrySpirit,
                "소화 보류",
                "픽업 초기화 시 스택 일부를 보존합니다.",
                r => r.hungryDigestionHold = true));

            definitions.Add(new Definition(
                "Hungry_FastingSense",
                LegendaryType.HungrySpirit,
                "공복 감각",
                "최대 스택 도달 후 10초간 픽업 불가, 대신 공격력이 증가합니다.",
                r => r.hungryFastingSense = true));

            definitions.Add(new Definition(
                "Hungry_FastingFocus",
                LegendaryType.HungrySpirit,
                "공복 집중",
                "픽업을 안 먹은 시간이 길수록 치명타 확률이 증가합니다. 최대 30%.",
                r => r.hungryFastingFocus = true));

            // ------------------------------------------------------------
            // NeedleShotgun / 샷건침 조건부 특수증강 3개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Shotgun_ReloadReflux",
                LegendaryType.NeedleShotgun,
                "재장전 역류",
                "0발 상태에서 대쉬 시 2발 장전합니다.",
                r => r.shotgunReloadReflux = true));

            definitions.Add(new Definition(
                "Shotgun_MagazineExpansion",
                LegendaryType.NeedleShotgun,
                "탄창 확장",
                "최대 장전 수가 증가합니다.",
                r => r.shotgunMagazineExpansion = true));

            definitions.Add(new Definition(
                "Shotgun_TacticalReload",
                LegendaryType.NeedleShotgun,
                "전술 장전",
                "장전속도가 증가합니다.",
                r => r.shotgunTacticalReload = true));

            return definitions;
        }
    }
}
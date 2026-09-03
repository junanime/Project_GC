using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using SpecialType = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;

namespace Vampire
{
    /// <summary>
    /// 특수증강을 보유했을 때만 등장하는 조건부 일반증강 카드입니다.
    ///
    /// 카드 아이콘 규칙:
    /// - 조건부 일반증강마다 별도 스프라이트를 만들지 않습니다.
    /// - 조건이 되는 원본 특수증강 카드의 스프라이트를 공유합니다.
    /// - 예: 독성 농축, 느린 부식독, 빠른 침투독은 모두 독침 스프라이트를 사용합니다.
    /// </summary>
    public class SyringeSpecialConditionalGeneralAugmentAbility : Ability
    {
        [Serializable]
        private class SpecialSourceIconEntry
        {
            [Tooltip("이 아이콘을 사용할 조건 특수증강 종류입니다.")]
            public SpecialType specialType;

            [Tooltip("원본 특수증강 Ability 프리팹/컴포넌트입니다. 여기의 Image를 조건부 카드 아이콘으로 공유합니다.")]
            public Ability sourceAbility;

            [Tooltip("sourceAbility가 비어 있을 때 사용할 예비 스프라이트입니다.")]
            public Sprite fallbackSprite;
        }

        private class Definition
        {
            public readonly string id;
            public readonly SpecialType requiredSpecial;
            public readonly string displayName;
            public readonly string effectText;
            public readonly int maxStack;
            public readonly Action<ApplyContext> apply;

            public Definition(
                string id,
                SpecialType requiredSpecial,
                string displayName,
                string effectText,
                int maxStack,
                Action<ApplyContext> apply)
            {
                this.id = id;
                this.requiredSpecial = requiredSpecial;
                this.displayName = displayName;
                this.effectText = effectText;
                this.maxStack = maxStack;
                this.apply = apply;
            }
        }

        private class ApplyContext
        {
            public readonly Character player;
            public readonly SyringeDartAbility syringe;
            public readonly PlayerGeneralStatRuntime statRuntime;

            private readonly MonoBehaviour logOwner;
            private readonly bool debugLog;

            public ApplyContext(
                Character player,
                SyringeDartAbility syringe,
                PlayerGeneralStatRuntime statRuntime,
                MonoBehaviour logOwner,
                bool debugLog)
            {
                this.player = player;
                this.syringe = syringe;
                this.statRuntime = statRuntime;
                this.logOwner = logOwner;
                this.debugLog = debugLog;
            }

            public void MultiplySyringeFloat(string fieldName, float multiplier)
            {
                MultiplySyringeFloat(fieldName, multiplier, float.NegativeInfinity, float.PositiveInfinity);
            }

            public void MultiplySyringeFloat(string fieldName, float multiplier, float min, float max)
            {
                FieldInfo fieldInfo = GetSyringeField(fieldName);

                if (fieldInfo == null)
                {
                    return;
                }

                object currentObject = fieldInfo.GetValue(syringe);

                if (!(currentObject is float currentValue))
                {
                    LogWarning($"{fieldName} 필드가 float 타입이 아닙니다.");
                    return;
                }

                float nextValue = currentValue * multiplier;
                nextValue = ClampFloat(nextValue, min, max);

                fieldInfo.SetValue(syringe, nextValue);
            }

            public void AddSyringeFloat(string fieldName, float amount)
            {
                AddSyringeFloat(fieldName, amount, float.NegativeInfinity, float.PositiveInfinity);
            }

            public void AddSyringeFloat(string fieldName, float amount, float min, float max)
            {
                FieldInfo fieldInfo = GetSyringeField(fieldName);

                if (fieldInfo == null)
                {
                    return;
                }

                object currentObject = fieldInfo.GetValue(syringe);

                if (!(currentObject is float currentValue))
                {
                    LogWarning($"{fieldName} 필드가 float 타입이 아닙니다.");
                    return;
                }

                float nextValue = currentValue + amount;
                nextValue = ClampFloat(nextValue, min, max);

                fieldInfo.SetValue(syringe, nextValue);
            }

            public void AddSyringeInt(string fieldName, int amount, int min, int max)
            {
                FieldInfo fieldInfo = GetSyringeField(fieldName);

                if (fieldInfo == null)
                {
                    return;
                }

                object currentObject = fieldInfo.GetValue(syringe);

                if (!(currentObject is int currentValue))
                {
                    LogWarning($"{fieldName} 필드가 int 타입이 아닙니다.");
                    return;
                }

                int nextValue = Mathf.Clamp(currentValue + amount, min, max);
                fieldInfo.SetValue(syringe, nextValue);
            }

            public void PlayerFloat(string methodName, float value)
            {
                if (player == null)
                {
                    return;
                }

                MethodInfo methodInfo = player.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(float) },
                    null);

                if (methodInfo == null)
                {
                    LogWarning($"Character에서 {methodName}(float) 메서드를 찾지 못했습니다.");
                    return;
                }

                methodInfo.Invoke(player, new object[] { value });
            }

            private FieldInfo GetSyringeField(string fieldName)
            {
                if (syringe == null)
                {
                    return null;
                }

                FieldInfo fieldInfo = syringe.GetType().GetField(
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
                if (!float.IsNegativeInfinity(min))
                {
                    value = Mathf.Max(min, value);
                }

                if (!float.IsPositiveInfinity(max))
                {
                    value = Mathf.Min(max, value);
                }

                return value;
            }

            private void LogWarning(string message)
            {
                if (!debugLog)
                {
                    return;
                }

                Debug.LogWarning($"[특수 조건부 일반증강] {message}", logOwner);
            }
        }

        [Header("Special Conditional General Augment")]
        [Tooltip("이 조건부 일반증강 카드가 몇 번까지 다시 등장할 수 있는지 설정합니다.")]
        [SerializeField] private int maxSelections = 999;

        [Tooltip("선택된 조건부 일반증강 적용 결과를 Console에 출력합니다.")]
        [SerializeField] private bool conditionalGeneralDebugLog = true;

        [Header("Source Sprite Sharing")]
        [Tooltip("조건이 되는 원본 특수증강의 Ability 프리팹을 연결합니다.")]
        [SerializeField] private List<SpecialSourceIconEntry> sourceIconEntries = new List<SpecialSourceIconEntry>();

        private SyringeDartAbility syringeDartAbility;
        private PlayerGeneralStatRuntime statRuntime;

        private readonly Dictionary<string, int> stackCounts = new Dictionary<string, int>();

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

                Sprite sourceSprite = ResolveSourceSprite(previewDefinition.requiredSpecial);
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
                    return "조건부 일반증강";
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
                    return "현재 보유한 특수증강 중 강화 가능한 조건부 일반증강이 없습니다.";
                }

                return
                    $"[{GetSpecialDisplayName(previewDefinition.requiredSpecial)} 연계 일반증강]\n" +
                    previewDefinition.effectText;
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

            syringeDartAbility = abilityManager.GetComponentInChildren<SyringeDartAbility>(true);

            if (syringeDartAbility == null)
            {
                Debug.LogError("[특수 조건부 일반증강] SyringeDartAbility를 찾지 못했습니다.", this);
            }

            statRuntime = PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);
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
                Debug.LogWarning("[특수 조건부 일반증강] 적용 가능한 미리보기 증강이 없습니다.", this);
                ClearPreview();
                return;
            }

            ApplyContext context = new ApplyContext(
                playerCharacter,
                syringeDartAbility,
                statRuntime,
                this,
                conditionalGeneralDebugLog);

            previewDefinition.apply?.Invoke(context);

            AddStack(previewDefinition.id);

            if (conditionalGeneralDebugLog)
            {
                Debug.Log(
                    $"[특수 조건부 일반증강] {previewDefinition.displayName} 적용 | " +
                    $"{previewDefinition.effectText} | 조건={previewDefinition.requiredSpecial}",
                    this);
            }

            ClearPreview();
        }

        private List<Definition> BuildCandidateList()
        {
            List<Definition> candidates = new List<Definition>();
            List<Definition> allDefinitions = GetAllDefinitions();

            for (int i = 0; i < allDefinitions.Count; i++)
            {
                Definition definition = allDefinitions[i];

                if (!HasRequiredSpecial(definition.requiredSpecial))
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

            if (definition.maxStack <= 0)
            {
                return false;
            }

            int currentStack = stackCounts.ContainsKey(definition.id)
                ? stackCounts[definition.id]
                : 0;

            return currentStack >= definition.maxStack;
        }

        private void ClearPreview()
        {
            previewDefinition = null;
            previewPrepared = false;
        }

        private Sprite ResolveSourceSprite(SpecialType specialType)
        {
            for (int i = 0; i < sourceIconEntries.Count; i++)
            {
                SpecialSourceIconEntry entry = sourceIconEntries[i];

                if (entry == null)
                {
                    continue;
                }

                if (entry.specialType != specialType)
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

        private string GetSpecialDisplayName(SpecialType specialType)
        {
            switch (specialType)
            {
                case SpecialType.Poison:
                    return "독침";

                case SpecialType.Explosion:
                    return "폭발침";

                case SpecialType.Homing:
                    return "유도침";

                case SpecialType.Pierce:
                    return "관통침";

                case SpecialType.Honey:
                    return "꿀침";

                case SpecialType.Mosquito:
                    return "모기침";

                case SpecialType.ReturnNeedle:
                    return "귀환침";

                case SpecialType.AcupunctureFormation:
                    return "침술진";

                case SpecialType.FiberNeedle:
                    return "섬유침";

                case SpecialType.CorrosionNeedle:
                    return "부식침";

                case SpecialType.PressureNeedle:
                    return "압력침";

                case SpecialType.MarkNeedle:
                    return "표식침";

                case SpecialType.BipolarNeedle:
                    return "양극침";

                case SpecialType.DigestiveAcidSacNeedle:
                    return "소화액낭침";

                case SpecialType.HungerNeedle:
                    return "공복침";

                case SpecialType.GutBacteriaNeedle:
                    return "장내균침";

                default:
                    return "특수증강";
            }
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
            // Poison / 독침 조건부 일반증강 10개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Poison_01_ToxicConcentration",
                SpecialType.Poison,
                "독성 농축",
                "독 틱 피해가 12% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("poisonTickDamage", 1.12f)));

            definitions.Add(new Definition(
                "Poison_02_SlowCorrosivePoison",
                SpecialType.Poison,
                "느린 부식독",
                "독 지속시간이 12% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("poisonDuration", 1.12f)));

            definitions.Add(new Definition(
                "Poison_03_FastPenetratingPoison",
                SpecialType.Poison,
                "빠른 침투독",
                "독 피해 간격이 8% 감소합니다.",
                8,
                c => c.MultiplySyringeFloat("poisonTickInterval", 0.92f, 0.08f, 999f)));

            definitions.Add(new Definition(
                "Poison_04_AcidicPoisonMembrane",
                SpecialType.Poison,
                "위산성 독막",
                "독 피해가 5% 증가하고, 상태이상 피해가 5% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("poisonTickDamage", 1.05f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDamageMultiplier(0.05f);
                    }
                }));

            definitions.Add(new Definition(
                "Poison_05_ToxicMucosalReaction",
                SpecialType.Poison,
                "독성 점막 반응",
                "독 지속시간이 5% 증가하고, 경험치 획득량이 5% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("poisonDuration", 1.05f);

                    if (c.player != null)
                    {
                        c.player.AddExpMultiplier(0.05f);
                    }
                }));

            definitions.Add(new Definition(
                "Poison_06_PoisonStability",
                SpecialType.Poison,
                "독 지속 안정화",
                "독 지속시간이 6% 증가하고, 상태이상 지속시간이 6% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("poisonDuration", 1.06f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDurationMultiplier(0.06f);
                    }
                }));

            definitions.Add(new Definition(
                "Poison_07_PoisonNeedlePreheating",
                SpecialType.Poison,
                "독 바늘 예열",
                "독 피해가 4% 증가하고, 치명타 확률이 3% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("poisonTickDamage", 1.04f);

                    if (c.player != null)
                    {
                        c.player.AddCritChance(0.03f);
                    }
                }));

            definitions.Add(new Definition(
                "Poison_08_ConcentrationMaintenance",
                SpecialType.Poison,
                "농도 유지",
                "독 지속시간이 8% 증가하고, 독 피해 간격이 3% 감소합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("poisonDuration", 1.08f);
                    c.MultiplySyringeFloat("poisonTickInterval", 0.97f, 0.08f, 999f);
                }));

            definitions.Add(new Definition(
                "Poison_09_AcidPoisonNeedling",
                SpecialType.Poison,
                "위산 독침질",
                "독 피해가 4% 증가하고, 넉백이 6% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("poisonTickDamage", 1.04f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddKnockbackMultiplier(0.06f);
                    }
                }));

            definitions.Add(new Definition(
                "Poison_10_ToxicEcho",
                SpecialType.Poison,
                "독성 잔향",
                "상태이상 피해가 6% 증가하고, 상태이상 지속시간이 4% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDamageMultiplier(0.06f);
                        c.statRuntime.AddStatusDurationMultiplier(0.04f);
                    }
                }));

            // ------------------------------------------------------------
            // Explosion / 폭발침 조건부 일반증강 10개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Explosion_01_StableExplosion",
                SpecialType.Explosion,
                "폭발 안정화",
                "폭발 발동 확률이 5% 증가합니다.",
                6,
                c => c.AddSyringeFloat("specialExplosionChance", 0.05f, 0f, 1f)));

            definitions.Add(new Definition(
                "Explosion_02_AcidBlastPressure",
                SpecialType.Explosion,
                "위산 폭압",
                "폭발 반경이 8% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("explosionRadius", 1.08f)));

            definitions.Add(new Definition(
                "Explosion_03_MucosalFragments",
                SpecialType.Explosion,
                "점막 파편",
                "폭발 피해가 10% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("explosionDamage", 1.10f)));

            definitions.Add(new Definition(
                "Explosion_04_ShortShockwave",
                SpecialType.Explosion,
                "짧은 충격파",
                "넉백이 10% 증가하고, 폭발 반경이 3% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddKnockbackMultiplier(0.10f);
                    }

                    c.MultiplySyringeFloat("explosionRadius", 1.03f);
                }));

            definitions.Add(new Definition(
                "Explosion_05_ColdExplosion",
                SpecialType.Explosion,
                "저온 폭발",
                "폭발 반경이 5% 증가하고, 상태이상 지속시간이 4% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("explosionRadius", 1.05f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDurationMultiplier(0.04f);
                    }
                }));

            definitions.Add(new Definition(
                "Explosion_06_FocusedBlastCore",
                SpecialType.Explosion,
                "폭심 집중",
                "폭발 피해가 15% 증가하지만, 폭발 반경이 4% 감소합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("explosionDamage", 1.15f);
                    c.MultiplySyringeFloat("explosionRadius", 0.96f, 0.1f, 999f);
                }));

            definitions.Add(new Definition(
                "Explosion_07_GasCompression",
                SpecialType.Explosion,
                "가스 압축",
                "폭발 피해가 18% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("explosionDamage", 1.18f)));

            definitions.Add(new Definition(
                "Explosion_08_ScatteredExplosion",
                SpecialType.Explosion,
                "분산 폭발",
                "폭발 반경이 12% 증가하지만, 폭발 피해가 3% 감소합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("explosionRadius", 1.12f);
                    c.MultiplySyringeFloat("explosionDamage", 0.97f, 0.1f, 999f);
                }));

            definitions.Add(new Definition(
                "Explosion_09_ChainPreheat",
                SpecialType.Explosion,
                "연쇄 예열",
                "폭발 피해가 5% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("explosionDamage", 1.05f)));

            definitions.Add(new Definition(
                "Explosion_10_DigestiveGasReaction",
                SpecialType.Explosion,
                "소화 가스 반응",
                "폭발 반경이 4% 증가하고, 상태이상 지속시간이 3% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("explosionRadius", 1.04f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDurationMultiplier(0.03f);
                    }
                }));

            // ------------------------------------------------------------
            // Homing / 유도침 조건부 일반증강 10개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Homing_01_ScentTracking",
                SpecialType.Homing,
                "후각 추적",
                "유도 탐지 범위가 10% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingRange", 1.10f)));

            definitions.Add(new Definition(
                "Homing_02_MucosalDirectionSense",
                SpecialType.Homing,
                "점막 방향감각",
                "유도 회전 속도가 10% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingLerpSpeed", 1.10f)));

            definitions.Add(new Definition(
                "Homing_03_GastricScentMarking",
                SpecialType.Homing,
                "위장 냄새 각인",
                "유도 탐지 범위가 6% 증가하고, 유도 회전 속도가 4% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("homingRange", 1.06f);
                    c.MultiplySyringeFloat("homingLerpSpeed", 1.04f);
                }));

            definitions.Add(new Definition(
                "Homing_04_ShortRetarget",
                SpecialType.Homing,
                "짧은 재탐색",
                "유도 회전 속도가 6% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingLerpSpeed", 1.06f)));

            definitions.Add(new Definition(
                "Homing_05_CloseTrackingCorrection",
                SpecialType.Homing,
                "근접 추적 보정",
                "유도 회전 속도가 12% 증가하지만, 유도 범위가 3% 감소합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("homingLerpSpeed", 1.12f);
                    c.MultiplySyringeFloat("homingRange", 0.97f, 0.1f, 999f);
                }));

            definitions.Add(new Definition(
                "Homing_06_LongRangeTrackingCorrection",
                SpecialType.Homing,
                "원거리 추적 보정",
                "유도 범위가 14% 증가하지만, 유도 회전 속도가 2% 감소합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("homingRange", 1.14f);
                    c.MultiplySyringeFloat("homingLerpSpeed", 0.98f, 0.1f, 999f);
                }));

            definitions.Add(new Definition(
                "Homing_07_WobbleSuppression",
                SpecialType.Homing,
                "흔들림 억제",
                "유도 회전 속도가 8% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingLerpSpeed", 1.08f)));

            definitions.Add(new Definition(
                "Homing_08_WeakPointTracking",
                SpecialType.Homing,
                "약점 추적",
                "유도 범위가 4% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingRange", 1.04f)));

            definitions.Add(new Definition(
                "Homing_09_ClusterTracking",
                SpecialType.Homing,
                "군집 추적",
                "유도 범위가 8% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingRange", 1.08f)));

            definitions.Add(new Definition(
                "Homing_10_AcidScentResidue",
                SpecialType.Homing,
                "위산 냄새 잔류",
                "유도 회전 속도가 5% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("homingLerpSpeed", 1.05f)));

            // ------------------------------------------------------------
            // Pierce / 관통침 조건부 일반증강 10개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Pierce_01_MucosalCutting",
                SpecialType.Pierce,
                "점막 절삭",
                "관통 횟수가 1 증가합니다.",
                5,
                c => c.AddSyringeInt("pierceCount", 1, 0, 999)));

            definitions.Add(new Definition(
                "Pierce_02_PierceStability",
                SpecialType.Pierce,
                "관통 안정성",
                "방어 관통이 5% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddDefensePierce(0.05f);
                    }
                }));

            definitions.Add(new Definition(
                "Pierce_03_ThinNeedleTip",
                SpecialType.Pierce,
                "얇은 침끝",
                "침 피해가 4% 증가하고, 관통 횟수가 1 증가합니다.",
                4,
                c =>
                {
                    c.PlayerFloat("AddDamageMultiplier", 0.04f);
                    c.AddSyringeInt("pierceCount", 1, 0, 999);
                }));

            definitions.Add(new Definition(
                "Pierce_04_StraightIncision",
                SpecialType.Pierce,
                "직선 절개",
                "침 피해가 6% 증가합니다.",
                8,
                c => c.PlayerFloat("AddDamageMultiplier", 0.06f)));

            definitions.Add(new Definition(
                "Pierce_05_PiercePathMemory",
                SpecialType.Pierce,
                "관통 경로 기억",
                "관통 횟수가 1 증가하고, 방어 관통이 2% 증가합니다.",
                4,
                c =>
                {
                    c.AddSyringeInt("pierceCount", 1, 0, 999);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddDefensePierce(0.02f);
                    }
                }));

            definitions.Add(new Definition(
                "Pierce_06_InnerWallScratch",
                SpecialType.Pierce,
                "내벽 긁기",
                "넉백이 8% 증가하고, 관통 횟수가 1 증가합니다.",
                4,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddKnockbackMultiplier(0.08f);
                    }

                    c.AddSyringeInt("pierceCount", 1, 0, 999);
                }));

            definitions.Add(new Definition(
                "Pierce_07_DeepStab",
                SpecialType.Pierce,
                "깊은 찌르기",
                "침 피해가 9% 증가합니다.",
                8,
                c => c.PlayerFloat("AddDamageMultiplier", 0.09f)));

            definitions.Add(new Definition(
                "Pierce_08_ContinuousPierceSense",
                SpecialType.Pierce,
                "연속 관통 감각",
                "공격속도가 5% 증가합니다.",
                8,
                c => c.PlayerFloat("AddAttackSpeed", 0.05f)));

            definitions.Add(new Definition(
                "Pierce_09_MucosalCutSurface",
                SpecialType.Pierce,
                "점막 절단면",
                "치명타 피해가 8% 증가하고, 방어 관통이 3% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddCritDamageMultiplier(0.08f);
                        c.statRuntime.AddDefensePierce(0.03f);
                    }
                }));

            definitions.Add(new Definition(
                "Pierce_10_PierceRecoveryTraining",
                SpecialType.Pierce,
                "관통 회수 훈련",
                "공격속도가 4% 증가하고, 관통 횟수가 1 증가합니다.",
                4,
                c =>
                {
                    c.PlayerFloat("AddAttackSpeed", 0.04f);
                    c.AddSyringeInt("pierceCount", 1, 0, 999);
                }));

            // ------------------------------------------------------------
            // Honey / 꿀침 조건부 일반증강 10개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Honey_01_SugarStickiness",
                SpecialType.Honey,
                "당분 점착",
                "감속 지속시간이 12% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("honeyDuration", 1.12f)));

            definitions.Add(new Definition(
                "Honey_02_HighDensityHoneyFilm",
                SpecialType.Honey,
                "고농도 꿀막",
                "감속 강도가 6% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("honeySlowMultiplier", 0.94f, 0.15f, 1f)));

            definitions.Add(new Definition(
                "Honey_03_StickyFeet",
                SpecialType.Honey,
                "끈적한 발밑",
                "넉백이 8% 증가하고, 감속 지속시간이 4% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddKnockbackMultiplier(0.08f);
                    }

                    c.MultiplySyringeFloat("honeyDuration", 1.04f);
                }));

            definitions.Add(new Definition(
                "Honey_04_HoneyFilmMaintenance",
                SpecialType.Honey,
                "꿀막 유지",
                "감속 지속시간이 8% 증가하고, 상태이상 지속시간이 4% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("honeyDuration", 1.08f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDurationMultiplier(0.04f);
                    }
                }));

            definitions.Add(new Definition(
                "Honey_05_SlowTargeting",
                SpecialType.Honey,
                "저속 표적화",
                "감속 지속시간이 5% 증가하고, 침 피해가 3% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("honeyDuration", 1.05f);
                    c.PlayerFloat("AddDamageMultiplier", 0.03f);
                }));

            definitions.Add(new Definition(
                "Honey_06_SugarHardening",
                SpecialType.Honey,
                "당분 굳힘",
                "치명타 피해가 8% 증가하고, 감속 지속시간이 3% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddCritDamageMultiplier(0.08f);
                    }

                    c.MultiplySyringeFloat("honeyDuration", 1.03f);
                }));

            definitions.Add(new Definition(
                "Honey_07_StickySpread",
                SpecialType.Honey,
                "점착 확산",
                "감속 지속시간이 5% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("honeyDuration", 1.05f)));

            definitions.Add(new Definition(
                "Honey_08_HoneyTrace",
                SpecialType.Honey,
                "꿀 흔적",
                "감속 강도가 4% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("honeySlowMultiplier", 0.96f, 0.15f, 1f)));

            definitions.Add(new Definition(
                "Honey_09_HeavySugarFilm",
                SpecialType.Honey,
                "무거운 당막",
                "감속 지속시간이 10% 증가하고, 넉백이 4% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("honeyDuration", 1.10f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddKnockbackMultiplier(0.04f);
                    }
                }));

            definitions.Add(new Definition(
                "Honey_10_AgedHoneyNeedle",
                SpecialType.Honey,
                "꿀침 숙성",
                "골드 드롭 확률이 3% 증가하고, 감속 지속시간이 4% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddGoldDropChance(0.03f);
                    }

                    c.MultiplySyringeFloat("honeyDuration", 1.04f);
                }));

            // ------------------------------------------------------------
            // Mosquito / 모기침 조건부 일반증강 10개
            // ------------------------------------------------------------
            definitions.Add(new Definition(
                "Mosquito_01_LifestealEfficiency",
                SpecialType.Mosquito,
                "흡혈 효율",
                "흡혈 회복량이 12% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("mosquitoHealPerHit", 1.12f)));

            definitions.Add(new Definition(
                "Mosquito_02_ThinBloodVesselSearch",
                SpecialType.Mosquito,
                "얇은 혈관 탐색",
                "공격속도가 5% 증가하고, 흡혈량이 4% 증가합니다.",
                8,
                c =>
                {
                    c.PlayerFloat("AddAttackSpeed", 0.05f);
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.04f);
                }));

            definitions.Add(new Definition(
                "Mosquito_03_DelayedCoagulation",
                SpecialType.Mosquito,
                "응고 지연",
                "흡혈량이 6% 증가하고, 상태이상 지속시간이 3% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.06f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddStatusDurationMultiplier(0.03f);
                    }
                }));

            definitions.Add(new Definition(
                "Mosquito_04_BloodPreservation",
                SpecialType.Mosquito,
                "혈액 보존",
                "받는 피해 감소가 3% 증가하고, 흡혈량이 5% 증가합니다.",
                8,
                c =>
                {
                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddDamageReduction(0.03f);
                    }

                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.05f);
                }));

            definitions.Add(new Definition(
                "Mosquito_05_GastricBloodFlowSense",
                SpecialType.Mosquito,
                "위장 혈류 감각",
                "최대 체력이 증가하고, 흡혈량이 4% 증가합니다.",
                8,
                c =>
                {
                    c.PlayerFloat("AddMaxHealthBonus", 5f);
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.04f);
                }));

            definitions.Add(new Definition(
                "Mosquito_06_BloodConcentration",
                SpecialType.Mosquito,
                "혈액 농축",
                "침 피해가 5% 증가하고, 흡혈량이 5% 증가합니다.",
                8,
                c =>
                {
                    c.PlayerFloat("AddDamageMultiplier", 0.05f);
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.05f);
                }));

            definitions.Add(new Definition(
                "Mosquito_07_MosquitoNeedleStability",
                SpecialType.Mosquito,
                "모기침 안정화",
                "보스 흡혈 배율이 10% 증가합니다.",
                8,
                c => c.MultiplySyringeFloat("mosquitoBossHealMultiplier", 1.10f)));

            definitions.Add(new Definition(
                "Mosquito_08_SmallTransfusion",
                SpecialType.Mosquito,
                "소량 수혈",
                "흡혈량이 4% 증가하고, 받는 피해 감소가 1% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.04f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddDamageReduction(0.01f);
                    }
                }));

            definitions.Add(new Definition(
                "Mosquito_09_BloodRecovery",
                SpecialType.Mosquito,
                "혈액 회수",
                "흡혈량이 8% 증가하고, 골드 드롭 확률이 2% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.08f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddGoldDropChance(0.02f);
                    }
                }));

            definitions.Add(new Definition(
                "Mosquito_10_OverdrainSuppression",
                SpecialType.Mosquito,
                "과흡혈 억제",
                "흡혈량이 5% 증가하고, 받는 피해 감소가 2% 증가합니다.",
                8,
                c =>
                {
                    c.MultiplySyringeFloat("mosquitoHealPerHit", 1.05f);

                    if (c.statRuntime != null)
                    {
                        c.statRuntime.AddDamageReduction(0.02f);
                    }
                }));

            return definitions;
        }
    }
}
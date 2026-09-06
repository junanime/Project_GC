using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// BossController가 사용하는 모든 보스 패턴의 공통 베이스입니다.
    ///
    /// 현재 단계의 원칙:
    /// - 패턴별 스크립트 분리 구조 유지
    /// - 패턴별 쿨타임 유지
    /// - 담당 파츠가 파괴되면 사용 불가
    /// - 코어 속성 기반 패턴 필터링 준비
    ///
    /// 기존 Distance Weight 필드는 각 파생 패턴의 Reset() 호환을 위해
    /// 이번 단계에서는 숨겨 둔 채 유지합니다.
    /// BossController가 코어 기반 선택으로 교체된 뒤 각 패턴의 레거시 코드를
    /// 정리하면서 최종 삭제합니다.
    /// </summary>
    public abstract class BossPatternBase : MonoBehaviour
    {
        [Header("Pattern Info")]
        [Tooltip("패턴 이름입니다. BossController Debug Pattern을 켰을 때 Console에 표시됩니다.")]
        [SerializeField]
        protected string patternName;

        [Tooltip(
            "이 패턴 자체의 기본 쿨타임입니다. " +
            "실제 쿨타임은 BossController의 현재 페이즈 배율이 곱해집니다.")]
        [SerializeField, Min(0f)]
        protected float cooldown = 3f;

        [Header("Core Pattern Rule")]
        [Tooltip(
            "이 패턴을 사용할 수 있는 코어 속성입니다. " +
            "예: Red 패턴이면 Red, Yellow 패턴이면 Yellow. " +
            "All은 모든 코어에서 사용할 수 있습니다.")]
        [SerializeField]
        private BossCoreTrait coreTraits = BossCoreTrait.All;

        [Tooltip(
            "Any: 지정 속성 중 하나라도 현재 코어에 있으면 사용 가능. " +
            "All: 지정 속성이 모두 현재 코어에 있어야 사용 가능. " +
            "Dual 전용 패턴 등을 만들 때 All을 사용합니다.")]
        [SerializeField]
        private BossPatternCoreMatchMode coreMatchMode = BossPatternCoreMatchMode.Any;

        // ============================================================
        // Legacy Distance Weights
        // ============================================================
        // 기존 파생 패턴 Reset()이 이 필드들을 직접 설정하고 있으므로
        // 1단계에서는 숨겨서 유지합니다.
        // 다음 단계에서 각 패턴 Reset()을 정리한 뒤 완전히 제거합니다.

        [HideInInspector, SerializeField]
        protected int nearWeightPhase1 = 10;

        [HideInInspector, SerializeField]
        protected int midWeightPhase1 = 10;

        [HideInInspector, SerializeField]
        protected int farWeightPhase1 = 10;

        [HideInInspector, SerializeField]
        protected int nearWeightPhase2 = 10;

        [HideInInspector, SerializeField]
        protected int midWeightPhase2 = 10;

        [HideInInspector, SerializeField]
        protected int farWeightPhase2 = 10;

        [HideInInspector, SerializeField]
        protected int nearWeightPhase3 = 10;

        [HideInInspector, SerializeField]
        protected int midWeightPhase3 = 10;

        [HideInInspector, SerializeField]
        protected int farWeightPhase3 = 10;

        [Header("Boss Part Binding")]
        [Tooltip(
            "체크하면 이 패턴의 부모 계층에서 BossPartDamageTestPart를 자동으로 찾아 " +
            "담당 파츠로 지정합니다.")]
        [SerializeField]
        private bool autoFindOwnerPartFromParent = true;

        [Tooltip(
            "이 패턴을 담당하는 보스 파츠입니다. " +
            "패턴을 파츠의 자식에 배치하면 비워 두어도 자동 탐색됩니다.")]
        [SerializeField]
        private BossPartDamageTestPart ownerPart;

        [Tooltip(
            "Owner Part 외에 추가로 살아 있어야 하는 파츠가 있다면 등록합니다.")]
        [SerializeField]
        private BossPartDamageTestPart[] additionalRequiredParts;

        [Tooltip(
            "체크하면 Additional Required Parts가 모두 살아 있어야 패턴을 사용할 수 있습니다. " +
            "끄면 등록된 파츠 중 하나 이상 살아 있으면 됩니다.")]
        [SerializeField]
        private bool requireAllAdditionalParts = true;

        [Tooltip(
            "체크하면 투사체 패턴의 기준 위치로 담당 파츠 위치를 사용합니다. " +
            "각 패턴 스크립트에서 PatternOriginPosition을 사용해야 적용됩니다.")]
        [SerializeField]
        private bool useOwnerPartAsPatternOrigin = true;

        [Header("Debug")]
        [Tooltip("파츠 연결 관련 디버그 로그를 출력합니다.")]
        [SerializeField]
        private bool debugPartBinding = false;

        protected BossController bossController;
        protected float lastUseTime = -999f;

        public string PatternName =>
            string.IsNullOrWhiteSpace(patternName)
                ? GetType().Name
                : patternName;

        public float BaseCooldown => cooldown;
        public BossCoreTrait CoreTraits => coreTraits;
        public BossPatternCoreMatchMode CoreMatchMode => coreMatchMode;
        public BossPartDamageTestPart OwnerPart => ownerPart;

        public bool IsOwnerPartAvailable =>
            ownerPart == null || !ownerPart.IsBroken;

        protected Vector3 PatternOriginPosition
        {
            get
            {
                if (useOwnerPartAsPatternOrigin && ownerPart != null)
                {
                    return ownerPart.transform.position;
                }

                if (bossController != null)
                {
                    return bossController.BossCenterPosition;
                }

                return transform.position;
            }
        }

        public virtual void Init(BossController controller)
        {
            bossController = controller;
            ResolveOwnerPart();

            if (debugPartBinding)
            {
                Debug.Log(
                    $"[BossPattern] Init | Pattern={PatternName}, " +
                    $"Core={coreTraits}, Owner=" +
                    $"{(ownerPart != null ? ownerPart.PartType.ToString() : "None")}",
                    this);
            }
        }

        /// <summary>
        /// 현재 코어가 이 패턴을 허용하는지 확인합니다.
        /// BossController의 새 패턴 선택기가 이 메서드를 사용합니다.
        /// </summary>
        public bool SupportsCore(BossCoreTrait activeCoreTraits)
        {
            return BossCoreTraitUtility.Matches(
                activeCoreTraits,
                coreTraits,
                coreMatchMode);
        }

        /// <summary>
        /// 쿨타임 + 담당 파츠 생존 조건을 확인합니다.
        /// 기존 파생 클래스의 override 호환을 위해 시그니처를 유지합니다.
        /// </summary>
        public virtual bool CanUse()
        {
            if (!AreRequiredPartsAvailable())
            {
                return false;
            }

            float finalCooldown = cooldown;

            if (bossController != null)
            {
                finalCooldown = bossController.GetModifiedPatternCooldown(cooldown);
            }

            return Time.time >= lastUseTime + finalCooldown;
        }

        /// <summary>
        /// 레거시 거리 가중치 API입니다.
        /// 다음 단계에서 BossController의 거리 선택 로직과 함께 제거합니다.
        /// </summary>
        public int GetWeight(float distanceToPlayer, int phase)
        {
            if (bossController == null)
            {
                return 0;
            }

            if (phase <= 1)
            {
                if (distanceToPlayer <= bossController.NearDistanceThreshold)
                {
                    return nearWeightPhase1;
                }

                if (distanceToPlayer <= bossController.MidDistanceThreshold)
                {
                    return midWeightPhase1;
                }

                return farWeightPhase1;
            }

            if (phase == 2)
            {
                if (distanceToPlayer <= bossController.NearDistanceThreshold)
                {
                    return nearWeightPhase2;
                }

                if (distanceToPlayer <= bossController.MidDistanceThreshold)
                {
                    return midWeightPhase2;
                }

                return farWeightPhase2;
            }

            if (distanceToPlayer <= bossController.NearDistanceThreshold)
            {
                return nearWeightPhase3;
            }

            if (distanceToPlayer <= bossController.MidDistanceThreshold)
            {
                return midWeightPhase3;
            }

            return farWeightPhase3;
        }

        public IEnumerator Execute()
        {
            // 패턴 선택 이후 실행 직전에 파츠가 파괴되는 경우도 방지합니다.
            if (!AreRequiredPartsAvailable())
            {
                yield break;
            }

            lastUseTime = Time.time;
            yield return ExecutePattern();
        }

        protected bool AreRequiredPartsAvailable()
        {
            ResolveOwnerPart();

            if (ownerPart != null && ownerPart.IsBroken)
            {
                return false;
            }

            if (additionalRequiredParts == null || additionalRequiredParts.Length == 0)
            {
                return true;
            }

            if (requireAllAdditionalParts)
            {
                for (int i = 0; i < additionalRequiredParts.Length; i++)
                {
                    BossPartDamageTestPart part = additionalRequiredParts[i];

                    if (part == null)
                    {
                        continue;
                    }

                    if (part.IsBroken)
                    {
                        return false;
                    }
                }

                return true;
            }

            bool hasValidRequiredPart = false;

            for (int i = 0; i < additionalRequiredParts.Length; i++)
            {
                BossPartDamageTestPart part = additionalRequiredParts[i];

                if (part == null)
                {
                    continue;
                }

                hasValidRequiredPart = true;

                if (!part.IsBroken)
                {
                    return true;
                }
            }

            return !hasValidRequiredPart;
        }

        private void ResolveOwnerPart()
        {
            if (ownerPart != null || !autoFindOwnerPartFromParent)
            {
                return;
            }

            ownerPart = GetComponentInParent<BossPartDamageTestPart>();

            if (debugPartBinding && ownerPart != null)
            {
                Debug.Log(
                    $"[BossPattern] 자동 Owner 연결 | {PatternName} -> {ownerPart.PartType}",
                    this);
            }
        }

        protected abstract IEnumerator ExecutePattern();
    }
}

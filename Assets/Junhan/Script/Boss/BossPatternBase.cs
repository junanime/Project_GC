using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// BossController가 사용하는 모든 보스 패턴의 공통 베이스입니다.
    ///
    /// 규칙:
    /// - 패턴별 스크립트 분리 구조 유지
    /// - 패턴별 쿨타임 유지
    /// - 담당 파츠가 파괴되면 사용 불가
    /// - 현재 코어 속성과 맞는 패턴만 사용 가능
    /// - 거리 기반 패턴 가중치는 더 이상 사용하지 않음
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
            "Red / Yellow / Blue 중 필요한 속성을 체크합니다. " +
            "듀얼코어에서는 현재 두 속성 중 하나라도 맞으면 해당 싱글코어 패턴이 사용됩니다.")]
        [SerializeField]
        private BossCoreTrait coreTraits = BossCoreTrait.All;

        [Tooltip(
            "Any: 지정 속성 중 하나라도 현재 코어에 포함되면 사용 가능. " +
            "All: 지정한 속성이 모두 현재 코어에 포함되어야 사용 가능. " +
            "현재 기존 패턴은 대부분 Any를 사용하고, 이후 듀얼 전용 패턴은 All을 사용할 수 있습니다.")]
        [SerializeField]
        private BossPatternCoreMatchMode coreMatchMode =
            BossPatternCoreMatchMode.Any;

        [Header("Boss Part Binding")]
        [Tooltip(
            "체크하면 이 패턴의 부모 계층에서 BossPartDamageTestPart를 자동으로 찾아 " +
            "담당 파츠로 지정합니다.")]
        [SerializeField]
        private bool autoFindOwnerPartFromParent = true;

        [Tooltip(
            "이 패턴을 담당하는 보스 파츠입니다. " +
            "담당 파츠가 파괴되면 이 패턴은 자동으로 사용 불가가 됩니다.")]
        [SerializeField]
        private BossPartDamageTestPart ownerPart;

        [Tooltip(
            "Owner Part 외에 추가로 살아 있어야 하는 파츠가 있다면 등록합니다. " +
            "예: 돌진은 양 다리 중 하나 이상이 살아 있어야 하는 식으로 사용할 수 있습니다.")]
        [SerializeField]
        private BossPartDamageTestPart[] additionalRequiredParts;

        [Tooltip(
            "체크하면 Additional Required Parts가 모두 살아 있어야 합니다. " +
            "끄면 등록된 파츠 중 하나 이상 살아 있으면 됩니다.")]
        [SerializeField]
        private bool requireAllAdditionalParts = true;

        [Tooltip(
            "체크하면 투사체/이펙트 생성 기준 위치로 담당 파츠 위치를 사용합니다. " +
            "각 패턴 스크립트가 PatternOriginPosition을 사용해야 적용됩니다.")]
        [SerializeField]
        private bool useOwnerPartAsPatternOrigin = true;

        [Header("Debug")]
        [Tooltip("패턴 초기화 및 파츠 연결 관련 로그를 출력합니다.")]
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
                if (useOwnerPartAsPatternOrigin &&
                    ownerPart != null)
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
        /// </summary>
        public virtual bool SupportsCore(
            BossCoreTrait activeCoreTraits)
        {
            return BossCoreTraitUtility.Matches(
                activeCoreTraits,
                coreTraits,
                coreMatchMode);
        }

        /// <summary>
        /// 쿨타임과 파츠 생존 조건을 확인합니다.
        /// 이후 개별 패턴에서 추가 조건이 필요하면 override할 수 있습니다.
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
                finalCooldown =
                    bossController.GetModifiedPatternCooldown(
                        cooldown);
            }

            return
                Time.time >=
                lastUseTime + finalCooldown;
        }

        /// <summary>
        /// BossController가 선택한 뒤 실제 패턴을 실행합니다.
        /// </summary>
        public IEnumerator Execute()
        {
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

            if (ownerPart != null &&
                ownerPart.IsBroken)
            {
                return false;
            }

            if (additionalRequiredParts == null ||
                additionalRequiredParts.Length == 0)
            {
                return true;
            }

            if (requireAllAdditionalParts)
            {
                for (int i = 0;
                     i < additionalRequiredParts.Length;
                     i++)
                {
                    BossPartDamageTestPart part =
                        additionalRequiredParts[i];

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

            for (int i = 0;
                 i < additionalRequiredParts.Length;
                 i++)
            {
                BossPartDamageTestPart part =
                    additionalRequiredParts[i];

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

            // 유효한 추가 파츠가 하나도 지정되지 않았다면 제한하지 않습니다.
            return !hasValidRequiredPart;
        }

        private void ResolveOwnerPart()
        {
            if (ownerPart != null ||
                !autoFindOwnerPartFromParent)
            {
                return;
            }

            ownerPart =
                GetComponentInParent
                <
                    BossPartDamageTestPart
                >();

            if (debugPartBinding &&
                ownerPart != null)
            {
                Debug.Log(
                    $"[BossPattern] 자동 Owner 연결 | " +
                    $"{PatternName} -> {ownerPart.PartType}",
                    this);
            }
        }

        protected abstract IEnumerator ExecutePattern();
    }
}

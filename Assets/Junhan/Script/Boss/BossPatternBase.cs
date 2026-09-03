using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// BossController가 사용하는 모든 보스 패턴의 공통 베이스입니다.
    ///
    /// 기존 거리/페이즈/쿨타임 구조를 그대로 유지하면서
    /// 테스트용 파츠 소유권 기능을 추가합니다.
    ///
    /// 담당 파츠가 파괴되면 CanUse()가 false가 되어
    /// BossController의 기존 패턴 선택 대상에서 자동 제외됩니다.
    /// </summary>
    public abstract class BossPatternBase : MonoBehaviour
    {
        [Header("Pattern Info")]

        [Tooltip(
            "패턴 이름입니다. BossController Debug Pattern을 켰을 때 Console에 표시됩니다.")]
        [SerializeField]
        protected string patternName;

        [Tooltip(
            "이 패턴 자체의 기본 쿨타임입니다. " +
            "실제 쿨타임은 보스 현재 페이즈의 Pattern Cooldown Multiplier가 곱해집니다.")]
        [SerializeField]
        protected float cooldown = 3f;

        [Header("Distance Weights - Phase 1")]

        [Tooltip(
            "1페이즈에서 플레이어가 근거리일 때 이 패턴이 선택될 가중치입니다. " +
            "0이면 근거리에서 선택되지 않습니다.")]
        [SerializeField]
        protected int nearWeightPhase1 = 10;

        [Tooltip(
            "1페이즈에서 플레이어가 중거리일 때 이 패턴이 선택될 가중치입니다. " +
            "0이면 중거리에서 선택되지 않습니다.")]
        [SerializeField]
        protected int midWeightPhase1 = 10;

        [Tooltip(
            "1페이즈에서 플레이어가 원거리일 때 이 패턴이 선택될 가중치입니다. " +
            "0이면 원거리에서 선택되지 않습니다.")]
        [SerializeField]
        protected int farWeightPhase1 = 10;

        [Header("Distance Weights - Phase 2")]

        [Tooltip(
            "2페이즈에서 플레이어가 근거리일 때 이 패턴이 선택될 가중치입니다.")]
        [SerializeField]
        protected int nearWeightPhase2 = 10;

        [Tooltip(
            "2페이즈에서 플레이어가 중거리일 때 이 패턴이 선택될 가중치입니다.")]
        [SerializeField]
        protected int midWeightPhase2 = 10;

        [Tooltip(
            "2페이즈에서 플레이어가 원거리일 때 이 패턴이 선택될 가중치입니다.")]
        [SerializeField]
        protected int farWeightPhase2 = 10;

        [Header("Distance Weights - Phase 3")]

        [Tooltip(
            "3페이즈에서 플레이어가 근거리일 때 이 패턴이 선택될 가중치입니다. " +
            "단 BossController의 Ignore Distance가 켜져 있으면 무시됩니다.")]
        [SerializeField]
        protected int nearWeightPhase3 = 10;

        [Tooltip(
            "3페이즈에서 플레이어가 중거리일 때 이 패턴이 선택될 가중치입니다.")]
        [SerializeField]
        protected int midWeightPhase3 = 10;

        [Tooltip(
            "3페이즈에서 플레이어가 원거리일 때 이 패턴이 선택될 가중치입니다.")]
        [SerializeField]
        protected int farWeightPhase3 = 10;

        // ============================================================
        // 파츠 시스템 추가
        // ============================================================

        [Header("Boss Part Binding - Test")]

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
            "Owner Part 외에 추가로 살아 있어야 하는 파츠가 있다면 등록합니다. " +
            "현재 기본 테스트에서는 비워 둡니다.")]
        [SerializeField]
        private BossPartDamageTestPart[] additionalRequiredParts;

        [Tooltip(
            "체크하면 Additional Required Parts가 모두 살아 있어야 패턴을 사용할 수 있습니다. " +
            "끄면 등록된 파츠 중 하나 이상 살아 있으면 됩니다.")]
        [SerializeField]
        private bool requireAllAdditionalParts = true;

        [Tooltip(
            "체크하면 투사체 패턴의 기준 위치로 담당 파츠 위치를 사용할 수 있습니다. " +
            "각 패턴 스크립트에서 PatternOriginPosition을 사용해야 적용됩니다.")]
        [SerializeField]
        private bool useOwnerPartAsPatternOrigin = true;

        [Tooltip(
            "파츠 연결 관련 디버그 로그를 출력합니다.")]
        [SerializeField]
        private bool debugPartBinding = false;

        protected BossController bossController;

        protected float lastUseTime = -999f;

        public string PatternName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(patternName))
                {
                    return GetType().Name;
                }

                return patternName;
            }
        }

        /// <summary>
        /// 이 패턴의 담당 파츠입니다.
        /// </summary>
        public BossPartDamageTestPart OwnerPart =>
            ownerPart;

        /// <summary>
        /// 담당 파츠가 현재 살아 있는지 확인합니다.
        /// </summary>
        public bool IsOwnerPartAvailable =>
            ownerPart == null ||
            !ownerPart.IsBroken;

        /// <summary>
        /// 이 패턴이 투사체 등을 생성할 기준 위치입니다.
        ///
        /// 담당 파츠를 사용하도록 설정했다면 파츠 위치,
        /// 그렇지 않으면 기존 BossController 중심 위치를 반환합니다.
        /// </summary>
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

        public virtual void Init(
            BossController controller)
        {
            bossController = controller;

            ResolveOwnerPart();

            if (debugPartBinding)
            {
                Debug.Log(
                    $"[BossPatternPart] Init | " +
                    $"Pattern={PatternName}, " +
                    $"Owner=" +
                    $"{(ownerPart != null ? ownerPart.PartType.ToString() : "None")}",
                    this);
            }
        }

        /// <summary>
        /// BossController가 패턴을 선택하기 전에 호출합니다.
        ///
        /// 기존 쿨타임 조건에 파츠 생존 조건을 추가합니다.
        /// </summary>
        public virtual bool CanUse()
        {
            if (!AreRequiredPartsAvailable())
            {
                return false;
            }

            float finalCooldown =
                cooldown;

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
        /// 플레이어 거리와 현재 페이즈를 바탕으로
        /// 기존 패턴 가중치를 반환합니다.
        /// </summary>
        public int GetWeight(
            float distanceToPlayer,
            int phase)
        {
            if (bossController == null)
            {
                return 0;
            }

            if (phase <= 1)
            {
                if (distanceToPlayer <=
                    bossController.NearDistanceThreshold)
                {
                    return nearWeightPhase1;
                }

                if (distanceToPlayer <=
                    bossController.MidDistanceThreshold)
                {
                    return midWeightPhase1;
                }

                return farWeightPhase1;
            }

            if (phase == 2)
            {
                if (distanceToPlayer <=
                    bossController.NearDistanceThreshold)
                {
                    return nearWeightPhase2;
                }

                if (distanceToPlayer <=
                    bossController.MidDistanceThreshold)
                {
                    return midWeightPhase2;
                }

                return farWeightPhase2;
            }

            if (distanceToPlayer <=
                bossController.NearDistanceThreshold)
            {
                return nearWeightPhase3;
            }

            if (distanceToPlayer <=
                bossController.MidDistanceThreshold)
            {
                return midWeightPhase3;
            }

            return farWeightPhase3;
        }

        /// <summary>
        /// BossController에서 실제 패턴을 실행할 때 호출합니다.
        /// </summary>
        public IEnumerator Execute()
        {
            // 선택 이후 실행 직전에 파츠가 파괴되는 경우도 방지.
            if (!AreRequiredPartsAvailable())
            {
                yield break;
            }

            lastUseTime =
                Time.time;

            yield return ExecutePattern();
        }

        /// <summary>
        /// Owner + 추가 요구 파츠의 생존 상태를 확인합니다.
        /// </summary>
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

            bool hasValidRequiredPart =
                false;

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

            // 유효한 파츠가 하나도 등록되지 않았다면
            // 제한하지 않습니다.
            return !hasValidRequiredPart;
        }

        /// <summary>
        /// 부모 계층의 BossPartDamageTestPart를 자동 탐색합니다.
        /// </summary>
        private void ResolveOwnerPart()
        {
            if (ownerPart != null)
            {
                return;
            }

            if (!autoFindOwnerPartFromParent)
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
                    $"[BossPatternPart] 자동 Owner 연결 | " +
                    $"{PatternName} -> {ownerPart.PartType}",
                    this);
            }
        }

        protected abstract IEnumerator ExecutePattern();
    }
}
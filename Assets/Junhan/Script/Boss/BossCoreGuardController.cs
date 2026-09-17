using System;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 한쪽 팔이 파괴되었을 때 살아남은 팔이 Core를 가리고,
    /// 해당 팔의 담당 패턴을 사용할 때 다시 원래 위치로 열어주는 제어기입니다.
    ///
    /// 현재 테스트용 파츠 프리팹 기준:
    /// - LeftArmPart : Homing / Bomb
    /// - RightArmPart : Fan Shot
    ///
    /// 규칙:
    /// - 양팔 생존: 양팔 원래 위치 유지
    /// - 한쪽 팔 파괴: 살아남은 팔이 Guard Anchor로 이동
    /// - Guard 중인 팔의 패턴 시작: 살아남은 팔이 원래 위치로 이동하여 Core Open
    /// - 패턴 종료: 다시 Guard
    /// - 양팔 파괴: Core 영구 노출
    /// - 외부 Groggy/기믹에서 SetExternalCoreOpen(true)를 호출하면 강제 Core Open 가능
    ///
    /// 주의:
    /// 이 단계에서는 Core Open 피해 배율을 실제 TakeDamage에 적용하지 않습니다.
    /// CurrentCoreDamageMultiplier만 제공하고,
    /// 다음 보스 Damage Pipeline 단계에서 보스 전용 증강/아이템과 함께 적용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossCoreGuardController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip(
            "패턴 시작/종료 이벤트를 제공하는 BossController입니다. " +
            "비워 두면 현재 오브젝트/부모/자식에서 자동 탐색합니다.")]
        [SerializeField]
        private BossController bossController;

        [Tooltip(
            "왼팔 파츠입니다. 비워 두면 PartType.LeftArm을 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestPart leftArmPart;

        [Tooltip(
            "오른팔 파츠입니다. 비워 두면 PartType.RightArm을 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestPart rightArmPart;

        [Tooltip(
            "가슴/Core 파츠입니다. 비워 두면 IsCore 또는 Torso 파츠를 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestPart corePart;

        [Header("Guard Anchors")]
        [Tooltip(
            "왼팔이 살아남았을 때 Core를 가리기 위해 이동할 위치입니다. " +
            "BossPartDamageTestRoot 아래에 빈 GameObject로 만들어 배치하는 것을 권장합니다.")]
        [SerializeField]
        private Transform leftArmGuardAnchor;

        [Tooltip(
            "오른팔이 살아남았을 때 Core를 가리기 위해 이동할 위치입니다. " +
            "BossPartDamageTestRoot 아래에 빈 GameObject로 만들어 배치하는 것을 권장합니다.")]
        [SerializeField]
        private Transform rightArmGuardAnchor;

        [Header("Guard Movement")]
        [Tooltip(
            "팔이 원래 위치와 Guard 위치 사이를 이동하는 속도입니다. " +
            "0이면 즉시 위치를 변경합니다.")]
        [SerializeField, Min(0f)]
        private float armMoveSpeed = 6f;

        [Tooltip(
            "팔이 원래 회전과 Guard Anchor 회전 사이를 이동하는 속도입니다. " +
            "0이면 즉시 회전합니다.")]
        [SerializeField, Min(0f)]
        private float armRotateSpeed = 540f;

        [Tooltip(
            "체크하면 Guard Anchor의 회전도 팔에 적용합니다. " +
            "테스트 스프라이트가 회전하면 어색할 경우 끄세요.")]
        [SerializeField]
        private bool applyGuardAnchorRotation = false;

        [Tooltip(
            "목표 위치와 이 거리 이하이면 정확히 목표 위치에 스냅합니다.")]
        [SerializeField, Min(0f)]
        private float positionSnapDistance = 0.01f;

        [Header("Core Exposure")]
        [Tooltip(
            "평상시 Core 피해 배율입니다. " +
            "이번 단계에서는 값만 제공하고 실제 파츠 피해에는 아직 적용하지 않습니다.")]
        [SerializeField, Min(0f)]
        private float normalCoreDamageMultiplier = 1f;

        [Tooltip(
            "Core가 Open 상태일 때 사용할 피해 배율입니다. " +
            "다음 Damage Pipeline 단계에서 실제 피해 계산에 연결합니다.")]
        [SerializeField, Min(0f)]
        private float openCoreDamageMultiplier = 1.5f;

        [Header("Runtime - Read Only")]
        [Tooltip("현재 한쪽 팔이 Core를 가리고 있는 상태인지 표시합니다.")]
        [SerializeField]
        private bool coreGuarded;

        [Tooltip("현재 Core가 Open 상태인지 표시합니다.")]
        [SerializeField]
        private bool coreOpen;

        [Tooltip(
            "Groggy, 전멸기 성공 등 외부 시스템이 Core Open을 강제하고 있는지 표시합니다.")]
        [SerializeField]
        private bool externalCoreOpen;

        [Tooltip(
            "현재 Guard를 담당하는 팔입니다. 양팔 생존/양팔 파괴 시 None입니다.")]
        [SerializeField]
        private BossPartDamageTestType currentGuardArmType =
            BossPartDamageTestType.Custom;

        [Tooltip(
            "현재 Guard 팔이 자기 담당 패턴을 사용 중인지 표시합니다.")]
        [SerializeField]
        private bool guardArmUsingPattern;

        [Tooltip("현재 적용 예정인 Core 피해 배율입니다.")]
        [SerializeField]
        private float currentCoreDamageMultiplier = 1f;

        [Header("Debug")]
        [Tooltip(
            "팔 파괴 감지, Guard/Open 전환, 패턴 연동 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private Transform leftArmTransform;
        private Transform rightArmTransform;

        private Vector3 leftArmOriginalLocalPosition;
        private Quaternion leftArmOriginalLocalRotation;
        private Vector3 rightArmOriginalLocalPosition;
        private Quaternion rightArmOriginalLocalRotation;

        private bool cachedOriginalPose;
        private bool subscribed;

        private bool lastLeftBroken;
        private bool lastRightBroken;

        private BossPartDamageTestPart activeGuardArm;

        public bool IsCoreGuarded => coreGuarded;
        public bool IsCoreOpen => coreOpen;
        public bool IsExternalCoreOpen => externalCoreOpen;
        public BossPartDamageTestPart ActiveGuardArm => activeGuardArm;
        public BossPartDamageTestPart CorePart => corePart;
        public float CurrentCoreDamageMultiplier => currentCoreDamageMultiplier;

        /// <summary>
        /// Core Open 상태가 바뀌었을 때 호출됩니다.
        /// bool = Open 여부
        /// float = 현재 Core 피해 배율
        /// </summary>
        public event Action<bool, float> CoreExposureChanged;

        private void Awake()
        {
            ResolveReferences();
            CacheOriginalArmPose();
            RefreshArmBrokenState(true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeBossEvents();
            CacheOriginalArmPose();
            RefreshArmBrokenState(true);
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeBossEvents();
            CacheOriginalArmPose();
            RefreshArmBrokenState(true);
            RefreshCoreGuardState(true);
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeBossEvents();

            RefreshArmBrokenState(false);
            UpdateArmPose();
        }

        private void OnDisable()
        {
            UnsubscribeBossEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeBossEvents();
        }

        /// <summary>
        /// Groggy, 카페인 주입기 성공, 압력 역분사 성공 등
        /// 외부 시스템이 Core를 강제로 열 때 사용합니다.
        /// </summary>
        public void SetExternalCoreOpen(bool open)
        {
            if (externalCoreOpen == open)
            {
                return;
            }

            externalCoreOpen = open;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCoreGuard] External Core Open = {externalCoreOpen}",
                    this);
            }

            RefreshCoreGuardState(true);
        }

        /// <summary>
        /// 현재 파츠 파괴 상태와 Core Guard 상태를 강제로 다시 계산합니다.
        /// Play Mode 디버깅용입니다.
        /// </summary>
        [ContextMenu("Refresh Core Guard State")]
        private void RefreshCoreGuardContextMenu()
        {
            ResolveReferences();
            CacheOriginalArmPose();
            RefreshArmBrokenState(true);
            RefreshCoreGuardState(true);
        }

        private void ResolveReferences()
        {
            if (bossController == null)
            {
                bossController =
                    GetComponent<BossController>();
            }

            if (bossController == null)
            {
                bossController =
                    GetComponentInParent<BossController>(true);
            }

            if (bossController == null)
            {
                bossController =
                    GetComponentInChildren<BossController>(true);
            }

            if (leftArmPart != null &&
                rightArmPart != null &&
                corePart != null)
            {
                ResolveArmTransforms();
                return;
            }

            BossPartDamageTestPart[] parts =
                GetComponentsInChildren<BossPartDamageTestPart>(true);

            if (parts == null || parts.Length == 0)
            {
                Transform topRoot = transform.root;

                if (topRoot != null)
                {
                    parts =
                        topRoot.GetComponentsInChildren
                        <BossPartDamageTestPart>(true);
                }
            }

            if (parts == null)
            {
                ResolveArmTransforms();
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                BossPartDamageTestPart part = parts[i];

                if (part == null)
                {
                    continue;
                }

                if (leftArmPart == null &&
                    part.PartType == BossPartDamageTestType.LeftArm)
                {
                    leftArmPart = part;
                }

                if (rightArmPart == null &&
                    part.PartType == BossPartDamageTestType.RightArm)
                {
                    rightArmPart = part;
                }

                if (corePart == null &&
                    (part.IsCore ||
                     part.PartType == BossPartDamageTestType.Torso))
                {
                    corePart = part;
                }
            }

            ResolveArmTransforms();
        }

        private void ResolveArmTransforms()
        {
            if (leftArmTransform == null &&
                leftArmPart != null)
            {
                leftArmTransform =
                    leftArmPart.transform;
            }

            if (rightArmTransform == null &&
                rightArmPart != null)
            {
                rightArmTransform =
                    rightArmPart.transform;
            }
        }

        private void CacheOriginalArmPose()
        {
            ResolveArmTransforms();

            if (cachedOriginalPose)
            {
                return;
            }

            if (leftArmTransform == null ||
                rightArmTransform == null)
            {
                return;
            }

            leftArmOriginalLocalPosition =
                leftArmTransform.localPosition;

            leftArmOriginalLocalRotation =
                leftArmTransform.localRotation;

            rightArmOriginalLocalPosition =
                rightArmTransform.localPosition;

            rightArmOriginalLocalRotation =
                rightArmTransform.localRotation;

            cachedOriginalPose = true;

            if (debugLog)
            {
                Debug.Log(
                    "[BossCoreGuard] 양팔 원래 Pose 저장 완료",
                    this);
            }
        }

        private void SubscribeBossEvents()
        {
            if (subscribed ||
                bossController == null)
            {
                return;
            }

            bossController.PatternStarted +=
                HandlePatternStarted;

            bossController.PatternEnded +=
                HandlePatternEnded;

            subscribed = true;
        }

        private void UnsubscribeBossEvents()
        {
            if (!subscribed ||
                bossController == null)
            {
                subscribed = false;
                return;
            }

            bossController.PatternStarted -=
                HandlePatternStarted;

            bossController.PatternEnded -=
                HandlePatternEnded;

            subscribed = false;
        }

        private void RefreshArmBrokenState(bool force)
        {
            bool leftBroken =
                leftArmPart == null ||
                leftArmPart.IsBroken;

            bool rightBroken =
                rightArmPart == null ||
                rightArmPart.IsBroken;

            if (!force &&
                leftBroken == lastLeftBroken &&
                rightBroken == lastRightBroken)
            {
                return;
            }

            lastLeftBroken = leftBroken;
            lastRightBroken = rightBroken;

            // 파괴 상태가 바뀌면 이전 팔 패턴 상태는 무효화합니다.
            guardArmUsingPattern = false;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCoreGuard] 팔 상태 변경 | " +
                    $"LeftBroken={leftBroken}, " +
                    $"RightBroken={rightBroken}",
                    this);
            }

            RefreshCoreGuardState(true);
        }

        private void RefreshCoreGuardState(bool forceEvent)
        {
            bool leftAlive =
                leftArmPart != null &&
                !leftArmPart.IsBroken;

            bool rightAlive =
                rightArmPart != null &&
                !rightArmPart.IsBroken;

            BossPartDamageTestPart newGuardArm = null;

            if (leftAlive && !rightAlive)
            {
                newGuardArm = leftArmPart;
            }
            else if (!leftAlive && rightAlive)
            {
                newGuardArm = rightArmPart;
            }

            activeGuardArm = newGuardArm;

            if (activeGuardArm == null)
            {
                currentGuardArmType =
                    BossPartDamageTestType.Custom;
            }
            else
            {
                currentGuardArmType =
                    activeGuardArm.PartType;
            }

            bool previousOpen = coreOpen;
            float previousMultiplier =
                currentCoreDamageMultiplier;

            // 양팔 모두 부서졌다면 영구 Open.
            bool bothArmsBroken =
                !leftAlive &&
                !rightAlive;

            // 한쪽 팔만 살아 있을 때 해당 팔이 자기 패턴을 사용하는 동안 Open.
            bool guardArmPatternOpen =
                activeGuardArm != null &&
                guardArmUsingPattern;

            coreOpen =
                bothArmsBroken ||
                externalCoreOpen ||
                guardArmPatternOpen;

            coreGuarded =
                activeGuardArm != null &&
                !coreOpen;

            currentCoreDamageMultiplier =
                coreOpen
                    ? openCoreDamageMultiplier
                    : normalCoreDamageMultiplier;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCoreGuard] 상태 | " +
                    $"GuardArm=" +
                    $"{(activeGuardArm != null ? activeGuardArm.PartType.ToString() : "None")}, " +
                    $"Guarded={coreGuarded}, " +
                    $"Open={coreOpen}, " +
                    $"Multiplier={currentCoreDamageMultiplier:0.##}",
                    this);
            }

            if (forceEvent ||
                previousOpen != coreOpen ||
                !Mathf.Approximately(
                    previousMultiplier,
                    currentCoreDamageMultiplier))
            {
                CoreExposureChanged?.Invoke(
                    coreOpen,
                    currentCoreDamageMultiplier);
            }
        }

        private void HandlePatternStarted(
            BossPatternBase pattern)
        {
            if (pattern == null)
            {
                return;
            }

            RefreshCoreGuardState(false);

            if (activeGuardArm == null)
            {
                return;
            }

            // 해당 Pattern의 OwnerPart가 현재 Guard 중인 팔일 때만 팔을 엽니다.
            if (pattern.OwnerPart != activeGuardArm)
            {
                return;
            }

            guardArmUsingPattern = true;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCoreGuard] Guard 팔 패턴 시작 → Core Open | " +
                    $"Pattern={pattern.PatternName}, " +
                    $"Arm={activeGuardArm.PartType}",
                    this);
            }

            RefreshCoreGuardState(true);
        }

        private void HandlePatternEnded(
            BossPatternBase pattern)
        {
            if (!guardArmUsingPattern)
            {
                return;
            }

            if (activeGuardArm == null)
            {
                guardArmUsingPattern = false;
                RefreshCoreGuardState(true);
                return;
            }

            // 현재 Guard 팔의 패턴 종료일 때만 Guard 복귀.
            if (pattern != null &&
                pattern.OwnerPart != activeGuardArm)
            {
                return;
            }

            guardArmUsingPattern = false;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCoreGuard] Guard 팔 패턴 종료 → Core Guard 복귀 | " +
                    $"Pattern={(pattern != null ? pattern.PatternName : "NULL")}",
                    this);
            }

            RefreshCoreGuardState(true);
        }

        private void UpdateArmPose()
        {
            if (!cachedOriginalPose)
            {
                CacheOriginalArmPose();

                if (!cachedOriginalPose)
                {
                    return;
                }
            }

            UpdateSingleArmPose(
                leftArmPart,
                leftArmTransform,
                leftArmGuardAnchor,
                leftArmOriginalLocalPosition,
                leftArmOriginalLocalRotation);

            UpdateSingleArmPose(
                rightArmPart,
                rightArmTransform,
                rightArmGuardAnchor,
                rightArmOriginalLocalPosition,
                rightArmOriginalLocalRotation);
        }

        private void UpdateSingleArmPose(
            BossPartDamageTestPart armPart,
            Transform armTransform,
            Transform guardAnchor,
            Vector3 originalLocalPosition,
            Quaternion originalLocalRotation)
        {
            if (armPart == null ||
                armTransform == null ||
                armPart.IsBroken)
            {
                return;
            }

            bool shouldGuard =
                coreGuarded &&
                activeGuardArm == armPart;

            Vector3 targetLocalPosition =
                originalLocalPosition;

            Quaternion targetLocalRotation =
                originalLocalRotation;

            if (shouldGuard &&
                guardAnchor != null)
            {
                Transform parent =
                    armTransform.parent;

                if (parent != null)
                {
                    targetLocalPosition =
                        parent.InverseTransformPoint(
                            guardAnchor.position);

                    if (applyGuardAnchorRotation)
                    {
                        targetLocalRotation =
                            Quaternion.Inverse(
                                parent.rotation) *
                            guardAnchor.rotation;
                    }
                }
                else
                {
                    targetLocalPosition =
                        guardAnchor.position;

                    if (applyGuardAnchorRotation)
                    {
                        targetLocalRotation =
                            guardAnchor.rotation;
                    }
                }
            }

            if (armMoveSpeed <= 0f)
            {
                armTransform.localPosition =
                    targetLocalPosition;
            }
            else
            {
                armTransform.localPosition =
                    Vector3.MoveTowards(
                        armTransform.localPosition,
                        targetLocalPosition,
                        armMoveSpeed * Time.deltaTime);

                if (Vector3.Distance(
                        armTransform.localPosition,
                        targetLocalPosition) <=
                    positionSnapDistance)
                {
                    armTransform.localPosition =
                        targetLocalPosition;
                }
            }

            if (!applyGuardAnchorRotation)
            {
                targetLocalRotation =
                    originalLocalRotation;
            }

            if (armRotateSpeed <= 0f)
            {
                armTransform.localRotation =
                    targetLocalRotation;
            }
            else
            {
                armTransform.localRotation =
                    Quaternion.RotateTowards(
                        armTransform.localRotation,
                        targetLocalRotation,
                        armRotateSpeed * Time.deltaTime);
            }
        }
    }
}

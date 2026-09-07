using System;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피의 전역 압력 게이지를 관리합니다.
    ///
    /// Step 9A 범위:
    /// - 보스 스폰 즉시 화면 오른쪽에 코드 생성 게이지 표시
    /// - BossPartDamageRules.DamageApplied 구독
    /// - 타격 횟수가 아니라 실제 플레이어 누적 피해량(ActualDamage)으로 충전
    /// - PlayerProjectile / PlayerAbility만 집계
    /// - BossSelf / Environment / Scripted / Unknown은 집계하지 않음
    /// - 기본 발동 기준 = 보스 전체 Max HP의 25%
    /// - 페이즈가 바뀌면 기본적으로 0으로 초기화
    /// - 100%가 되면 PRESSURE FULL 상태로 잠금
    ///
    /// Step 9B에서:
    /// - 100% 도달 시 임시 빛나는 압력 밸브 생성
    /// - 제한 시간
    /// - 성공 시 Core 역분사 + Groggy
    /// - 실패 시 압력 전멸 공격
    /// 을 연결합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPressureGaugeController :
        MonoBehaviour
    {
        [Header("References")]

        [Tooltip(
            "플레이어/BossSelf 등 피해 출처와 실제 적용 피해를 기록하는 BossPartDamageRules입니다. " +
            "비워 두면 현재 보스 루트에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossPartDamageRules damageRules;

        [Tooltip(
            "보스 전체 Max HP를 파츠 합산으로 제공하는 BossPartDamageTestRootController입니다. " +
            "비워 두면 현재 보스 루트에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossPartDamageTestRootController partRootController;

        [Tooltip(
            "보스 페이즈/사망 상태를 제공하는 BossController입니다. " +
            "비워 두면 현재 보스 루트에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossController bossController;

        [Header("Pressure Rule / 압력 충전 규칙")]

        [Tooltip(
            "압력 게이지가 가득 차는 데 필요한 누적 플레이어 피해입니다. " +
            "보스 전체 Max HP 비율이며 0.25는 Max HP의 25% 실제 피해를 의미합니다."
        )]
        [SerializeField, Range(0.01f, 1f)]
        private float pressureThresholdMaxHpPercent =
            0.25f;

        [Tooltip(
            "체크하면 페이즈가 변경될 때 현재 압력 누적량을 0으로 초기화합니다. " +
            "P1/P2/P3마다 독립된 압력 사이클을 테스트하기 위해 ON을 권장합니다."
        )]
        [SerializeField]
        private bool resetPressureOnPhaseChange =
            true;

        [Tooltip(
            "체크하면 실제 HP에서 감소한 ActualDamage를 사용합니다. " +
            "파츠 남은 HP보다 큰 과잉 피해가 들어와도 남은 HP만큼만 압력에 반영되므로 ON을 권장합니다."
        )]
        [SerializeField]
        private bool useActualDamage =
            true;

        [Header("Runtime UI / 런타임 게이지")]

        [Tooltip(
            "체크하면 보스가 활성화되는 즉시 별도 프리팹 없이 화면 오른쪽에 압력 게이지를 자동 생성합니다."
        )]
        [SerializeField]
        private bool autoCreateGaugeUI =
            true;

        [Tooltip(
            "보스 사망 시 자동 생성된 압력 게이지를 제거합니다."
        )]
        [SerializeField]
        private bool destroyGaugeOnBossDeath =
            true;

        [Header("Runtime - Read Only")]

        [Tooltip("현재 압력으로 누적된 플레이어 피해량입니다.")]
        [SerializeField]
        private float accumulatedPressureDamage;

        [Tooltip("현재 보스 Max HP 기준으로 계산된 압력 발동 피해량입니다.")]
        [SerializeField]
        private float pressureThresholdDamage;

        [Tooltip("현재 압력 게이지 0~1 값입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float pressureNormalized;

        [Tooltip("현재 압력 게이지가 100%인지 표시합니다.")]
        [SerializeField]
        private bool pressureFull;

        [Tooltip("현재 감지된 보스 페이즈입니다.")]
        [SerializeField]
        private int currentPhase;

        [Tooltip("BossPartDamageRules.DamageApplied 이벤트를 구독 중인지 표시합니다.")]
        [SerializeField]
        private bool damageEventSubscribed;

        [Tooltip("BossController.PhaseChanged 이벤트를 구독 중인지 표시합니다.")]
        [SerializeField]
        private bool phaseEventSubscribed;

        [Tooltip("런타임에 자동 생성된 압력 게이지 UI입니다.")]
        [SerializeField]
        private BossPressureGaugeUI gaugeUI;

        [Header("Debug")]

        [Tooltip(
            "압력 충전/리셋/100% 도달 로그를 출력합니다."
        )]
        [SerializeField]
        private bool debugLog =
            true;

        private BossPartDamageRules
            subscribedDamageRules;

        private BossController
            subscribedBossController;

        private bool bossDeathUiHandled;

        public float AccumulatedPressureDamage =>
            accumulatedPressureDamage;

        public float PressureThresholdDamage =>
            pressureThresholdDamage;

        public float PressureNormalized =>
            pressureNormalized;

        public bool IsPressureFull =>
            pressureFull;

        public int CurrentPhase =>
            currentPhase;

        /// <summary>
        /// 압력이 처음 100%에 도달하는 순간 한 번 호출됩니다.
        /// Step 9B의 압력 과부하 기믹 연결점입니다.
        /// </summary>
        public event Action PressureFilled;

        private void Awake()
        {
            ResolveReferences();

            ResetRuntimeState();

            // 요구사항:
            // 보스 스폰/활성화 즉시 오른쪽 게이지가 보여야 하므로
            // Start까지 기다리지 않고 Awake 단계에서 우선 생성합니다.
            EnsureGaugeUI();
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeEvents();
            EnsureGaugeUI();
            RefreshThresholdAndUI();
        }

        private void Start()
        {
            ResolveReferences();
            SubscribeEvents();

            if (bossController != null &&
                currentPhase <= 0)
            {
                currentPhase =
                    bossController.CurrentPhase;
            }

            RefreshThresholdAndUI();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPressureGauge] START | " +
                    $"Phase={currentPhase}, " +
                    $"BossMaxHP=" +
                    $"{(partRootController != null ? partRootController.TotalMaxHealth : 0f):0.##}, " +
                    $"Threshold={pressureThresholdDamage:0.##}",
                    this
                );
            }
        }

        private void Update()
        {
            ResolveReferences();
            SubscribeEvents();

            if (currentPhase <= 0 &&
                bossController != null)
            {
                currentPhase =
                    bossController.CurrentPhase;
            }

            if (pressureThresholdDamage <= 0f)
            {
                RefreshThresholdAndUI();
            }

            bool bossDead =
                IsBossDead();

            if (bossDead &&
                !bossDeathUiHandled)
            {
                bossDeathUiHandled =
                    true;

                if (destroyGaugeOnBossDeath)
                {
                    DestroyGaugeUI();
                }
            }
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            DestroyGaugeUI();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
            DestroyGaugeUI();
        }

        private void ResolveReferences()
        {
            Transform topRoot =
                transform.root != null
                    ? transform.root
                    : transform;

            if (damageRules == null)
            {
                damageRules =
                    GetComponent
                    <
                        BossPartDamageRules
                    >();
            }

            if (damageRules == null)
            {
                damageRules =
                    GetComponentInParent
                    <
                        BossPartDamageRules
                    >(true);
            }

            if (damageRules == null)
            {
                damageRules =
                    topRoot.GetComponentInChildren
                    <
                        BossPartDamageRules
                    >(true);
            }

            if (partRootController == null)
            {
                partRootController =
                    GetComponent
                    <
                        BossPartDamageTestRootController
                    >();
            }

            if (partRootController == null)
            {
                partRootController =
                    GetComponentInParent
                    <
                        BossPartDamageTestRootController
                    >(true);
            }

            if (partRootController == null)
            {
                partRootController =
                    topRoot.GetComponentInChildren
                    <
                        BossPartDamageTestRootController
                    >(true);
            }

            if (bossController == null)
            {
                bossController =
                    GetComponent
                    <
                        BossController
                    >();
            }

            if (bossController == null)
            {
                bossController =
                    GetComponentInParent
                    <
                        BossController
                    >(true);
            }

            if (bossController == null)
            {
                bossController =
                    topRoot.GetComponentInChildren
                    <
                        BossController
                    >(true);
            }
        }

        private void SubscribeEvents()
        {
            SubscribeDamageEvent();
            SubscribePhaseEvent();
        }

        private void SubscribeDamageEvent()
        {
            if (subscribedDamageRules ==
                damageRules &&
                damageEventSubscribed)
            {
                return;
            }

            if (subscribedDamageRules != null &&
                damageEventSubscribed)
            {
                subscribedDamageRules.DamageApplied -=
                    HandleBossDamageApplied;
            }

            subscribedDamageRules =
                damageRules;

            damageEventSubscribed =
                false;

            if (subscribedDamageRules == null)
            {
                return;
            }

            subscribedDamageRules.DamageApplied +=
                HandleBossDamageApplied;

            damageEventSubscribed =
                true;
        }

        private void SubscribePhaseEvent()
        {
            if (subscribedBossController ==
                bossController &&
                phaseEventSubscribed)
            {
                return;
            }

            if (subscribedBossController != null &&
                phaseEventSubscribed)
            {
                subscribedBossController.PhaseChanged -=
                    HandlePhaseChanged;
            }

            subscribedBossController =
                bossController;

            phaseEventSubscribed =
                false;

            if (subscribedBossController == null)
            {
                return;
            }

            subscribedBossController.PhaseChanged +=
                HandlePhaseChanged;

            phaseEventSubscribed =
                true;

            if (currentPhase <= 0)
            {
                currentPhase =
                    subscribedBossController.CurrentPhase;
            }
        }

        private void UnsubscribeEvents()
        {
            if (subscribedDamageRules != null &&
                damageEventSubscribed)
            {
                subscribedDamageRules.DamageApplied -=
                    HandleBossDamageApplied;
            }

            if (subscribedBossController != null &&
                phaseEventSubscribed)
            {
                subscribedBossController.PhaseChanged -=
                    HandlePhaseChanged;
            }

            subscribedDamageRules =
                null;

            subscribedBossController =
                null;

            damageEventSubscribed =
                false;

            phaseEventSubscribed =
                false;
        }

        private void HandleBossDamageApplied(
            BossDamageEventData eventData)
        {
            if (pressureFull ||
                IsBossDead())
            {
                return;
            }

            if (!BossPartDamageRules.IsPlayerDamage(
                    eventData.DamageSource))
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[BossPressureGauge] 압력 미집계 | " +
                        $"Source={eventData.DamageSource}, " +
                        $"Actual={eventData.ActualDamage:0.##}",
                        this
                    );
                }

                return;
            }

            float damageForPressure =
                useActualDamage
                    ? eventData.ActualDamage
                    : eventData.ModifiedDamage;

            damageForPressure =
                Mathf.Max(
                    0f,
                    damageForPressure);

            if (damageForPressure <= 0f)
            {
                return;
            }

            RecalculateThreshold();

            if (pressureThresholdDamage <= 0f)
            {
                return;
            }

            accumulatedPressureDamage =
                Mathf.Min(
                    pressureThresholdDamage,
                    accumulatedPressureDamage +
                    damageForPressure);

            RefreshNormalized();

            UpdateGaugeUI();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPressureGauge] PLAYER DAMAGE +{damageForPressure:0.##} | " +
                    $"{accumulatedPressureDamage:0.##}/" +
                    $"{pressureThresholdDamage:0.##} " +
                    $"({pressureNormalized * 100f:0.0}%) | " +
                    $"Part={(eventData.Part != null ? eventData.Part.PartType.ToString() : "NULL")}",
                    this
                );
            }

            if (!pressureFull &&
                pressureNormalized >= 1f)
            {
                SetPressureFull();
            }
        }

        private void HandlePhaseChanged(
            int newPhase)
        {
            int previousPhase =
                currentPhase;

            currentPhase =
                Mathf.Max(
                    1,
                    newPhase);

            // BossController.Start에서 발생하는 최초 PhaseChanged(1)는
            // 초기화 이벤트이므로 불필요한 리셋 로그를 만들지 않습니다.
            bool isRealPhaseChange =
                previousPhase > 0 &&
                previousPhase !=
                currentPhase;

            if (resetPressureOnPhaseChange &&
                isRealPhaseChange)
            {
                ResetPressureInternal(
                    $"Phase {previousPhase} -> {currentPhase}");
            }
            else
            {
                RefreshThresholdAndUI();
            }
        }

        private void SetPressureFull()
        {
            pressureFull =
                true;

            pressureNormalized =
                1f;

            accumulatedPressureDamage =
                Mathf.Max(
                    accumulatedPressureDamage,
                    pressureThresholdDamage);

            UpdateGaugeUI();

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[BossPressureGauge] PRESSURE FULL | " +
                    $"Phase={currentPhase}, " +
                    $"Threshold={pressureThresholdDamage:0.##} | " +
                    $"Step 9B 압력 과부하 기믹 연결 대기",
                    this
                );
            }

            PressureFilled?.Invoke();
        }

        private void RecalculateThreshold()
        {
            if (partRootController == null)
            {
                ResolveReferences();
            }

            float bossMaxHp =
                partRootController != null
                    ? partRootController.TotalMaxHealth
                    : 0f;

            if (bossMaxHp <= 0f)
            {
                pressureThresholdDamage =
                    0f;

                return;
            }

            pressureThresholdDamage =
                bossMaxHp *
                Mathf.Clamp(
                    pressureThresholdMaxHpPercent,
                    0.01f,
                    1f);
        }

        private void RefreshThresholdAndUI()
        {
            RecalculateThreshold();
            RefreshNormalized();
            EnsureGaugeUI();

            if (pressureThresholdDamage <= 0f)
            {
                if (gaugeUI != null)
                {
                    gaugeUI.ShowWaitingForBossHealth(
                        currentPhase);
                }

                return;
            }

            UpdateGaugeUI();
        }

        private void RefreshNormalized()
        {
            pressureNormalized =
                pressureThresholdDamage > 0f
                    ? Mathf.Clamp01(
                        accumulatedPressureDamage /
                        pressureThresholdDamage)
                    : 0f;

            if (pressureFull)
            {
                pressureNormalized =
                    1f;
            }
        }

        private void EnsureGaugeUI()
        {
            if (!autoCreateGaugeUI ||
                gaugeUI != null)
            {
                return;
            }

            gaugeUI =
                BossPressureGaugeUI
                    .CreateTemporaryGauge();

            gaugeUI.Initialize(
                pressureThresholdDamage,
                currentPhase);
        }

        private void UpdateGaugeUI()
        {
            EnsureGaugeUI();

            if (gaugeUI == null)
            {
                return;
            }

            gaugeUI.UpdateGauge(
                pressureNormalized,
                accumulatedPressureDamage,
                pressureThresholdDamage,
                pressureFull,
                currentPhase);
        }

        private bool IsBossDead()
        {
            if (partRootController != null &&
                partRootController.IsBossDead)
            {
                return true;
            }

            if (bossController != null &&
                bossController.IsDead)
            {
                return true;
            }

            return false;
        }

        private void ResetRuntimeState()
        {
            accumulatedPressureDamage =
                0f;

            pressureThresholdDamage =
                0f;

            pressureNormalized =
                0f;

            pressureFull =
                false;

            currentPhase =
                bossController != null
                    ? bossController.CurrentPhase
                    : 0;

            bossDeathUiHandled =
                false;
        }

        private void ResetPressureInternal(
            string reason)
        {
            accumulatedPressureDamage =
                0f;

            pressureFull =
                false;

            RecalculateThreshold();
            RefreshNormalized();
            UpdateGaugeUI();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPressureGauge] RESET | " +
                    $"Reason={reason}, Phase={currentPhase}",
                    this
                );
            }
        }

        private void DestroyGaugeUI()
        {
            if (gaugeUI == null)
            {
                return;
            }

            BossPressureGaugeUI uiToDestroy =
                gaugeUI;

            gaugeUI =
                null;

            uiToDestroy.DestroyGauge();
        }

        [ContextMenu("Debug/Add 10% Boss Max HP Pressure")]
        private void DebugAddTenPercentPressure()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] Play Mode에서 실행하세요.",
                    this);

                return;
            }

            RecalculateThreshold();

            if (partRootController == null ||
                partRootController.TotalMaxHealth <= 0f)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] 보스 Max HP가 아직 준비되지 않았습니다.",
                    this);

                return;
            }

            if (pressureFull)
            {
                return;
            }

            float debugDamage =
                partRootController.TotalMaxHealth *
                0.1f;

            accumulatedPressureDamage =
                Mathf.Min(
                    pressureThresholdDamage,
                    accumulatedPressureDamage +
                    debugDamage);

            RefreshNormalized();
            UpdateGaugeUI();

            if (pressureNormalized >= 1f)
            {
                SetPressureFull();
            }
        }

        [ContextMenu("Debug/Fill Pressure Gauge")]
        private void DebugFillPressureGauge()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] Play Mode에서 실행하세요.",
                    this);

                return;
            }

            RecalculateThreshold();

            if (pressureThresholdDamage <= 0f)
            {
                return;
            }

            accumulatedPressureDamage =
                pressureThresholdDamage;

            RefreshNormalized();
            SetPressureFull();
        }

        [ContextMenu("Debug/Reset Pressure Gauge")]
        private void DebugResetPressureGauge()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] Play Mode에서 실행하세요.",
                    this);

                return;
            }

            ResetPressureInternal(
                "Debug Context Menu");
        }
    }
}

using System;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피의 전역 압력 게이지를 관리합니다.
    ///
    /// 기능:
    /// - 보스 스폰 즉시 화면 오른쪽에 압력 게이지 UI 생성
    /// - BossPartDamageRules.DamageApplied 이벤트 구독
    /// - 타격 횟수가 아니라 실제 플레이어 누적 피해량으로 충전
    /// - PlayerProjectile / PlayerAbility만 집계
    /// - BossSelf / Environment / Scripted / Unknown은 집계하지 않음
    /// - 기본 발동 기준 = 보스 전체 Max HP의 25%
    /// - 페이즈 변경 시 기본적으로 압력 초기화
    /// - 100% 도달 시 PressureFilled 이벤트 발생
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPressureGaugeController :
        MonoBehaviour
    {
        [Header("References")]

        [Tooltip(
            "플레이어/BossSelf 등 피해 출처와 실제 적용 피해를 기록하는 " +
            "BossPartDamageRules입니다. 비워 두면 현재 보스 루트에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossPartDamageRules damageRules;

        [Tooltip(
            "보스 전체 Max HP를 파츠 합산으로 제공하는 " +
            "BossPartDamageTestRootController입니다. " +
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
            "보스 전체 Max HP 비율이며 0.25는 Max HP의 25%입니다."
        )]
        [SerializeField, Range(0.01f, 1f)]
        private float pressureThresholdMaxHpPercent = 0.25f;

        [Tooltip(
            "체크하면 페이즈가 변경될 때 압력 게이지를 0으로 초기화합니다."
        )]
        [SerializeField]
        private bool resetPressureOnPhaseChange = true;

        [Tooltip(
            "체크하면 실제 HP에서 감소한 ActualDamage를 압력 계산에 사용합니다. " +
            "과잉 피해가 게이지에 과도하게 반영되는 것을 막기 위해 ON을 권장합니다."
        )]
        [SerializeField]
        private bool useActualDamage = true;


        [Header("Runtime UI / 런타임 게이지")]

        [Tooltip(
            "체크하면 보스가 활성화되는 즉시 화면 오른쪽에 압력 게이지를 자동 생성합니다."
        )]
        [SerializeField]
        private bool autoCreateGaugeUI = true;

        [Tooltip(
            "보스 사망 시 자동 생성된 압력 게이지 UI를 제거합니다."
        )]
        [SerializeField]
        private bool destroyGaugeOnBossDeath = true;


        [Header("Runtime - Read Only")]

        [Tooltip("현재 압력으로 누적된 플레이어 피해량입니다.")]
        [SerializeField]
        private float accumulatedPressureDamage;

        [Tooltip("현재 보스 Max HP 기준 압력 게이지 발동 피해량입니다.")]
        [SerializeField]
        private float pressureThresholdDamage;

        [Tooltip("현재 압력 게이지의 0~1 값입니다.")]
        [SerializeField, Range(0f, 1f)]
        private float pressureNormalized;

        [Tooltip("현재 압력 게이지가 100%인지 표시합니다.")]
        [SerializeField]
        private bool pressureFull;

        [Tooltip("현재 보스 페이즈입니다.")]
        [SerializeField]
        private int currentPhase;

        [Tooltip(
            "BossPartDamageRules.DamageApplied 이벤트를 현재 구독 중인지 표시합니다."
        )]
        [SerializeField]
        private bool damageEventSubscribed;

        [Tooltip(
            "BossController.PhaseChanged 이벤트를 현재 구독 중인지 표시합니다."
        )]
        [SerializeField]
        private bool phaseEventSubscribed;

        [Tooltip("런타임에 자동 생성된 압력 게이지 UI입니다.")]
        [SerializeField]
        private BossPressureGaugeUI gaugeUI;


        [Header("Debug")]

        [Tooltip(
            "압력 피해 누적, 리셋, 100% 도달 등의 로그를 출력합니다."
        )]
        [SerializeField]
        private bool debugLog = true;


        private BossPartDamageRules subscribedDamageRules;
        private BossController subscribedBossController;

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
        /// 압력 게이지가 처음 100%에 도달한 순간 호출됩니다.
        /// Step 9B 압력 과부하 패턴에서 사용합니다.
        /// </summary>
        public event Action PressureFilled;


        private void Awake()
        {
            ResolveReferences();

            ResetRuntimeState();

            // 보스가 생성되자마자 UI가 보여야 하므로
            // Start를 기다리지 않고 생성합니다.
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
                    $"Threshold={pressureThresholdDamage:0.##}, " +
                    $"DamageRules={(damageRules != null ? damageRules.gameObject.name : "NULL")}",
                    this
                );
            }
        }


        private void Update()
        {
            // 보스 프리팹의 Awake/Start 순서에 따라
            // 다른 컴포넌트가 늦게 준비될 수도 있으므로 재탐색합니다.
            ResolveReferences();

            SubscribeEvents();

            if (currentPhase <= 0 &&
                bossController != null)
            {
                currentPhase =
                    bossController.CurrentPhase;
            }

            // 파츠 Root가 초기화되기 전에는 Max HP가 0일 수 있습니다.
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


        // =========================================================
        // References
        // =========================================================

        private void ResolveReferences()
        {
            Transform topRoot =
                transform.root != null
                    ? transform.root
                    : transform;


            if (damageRules == null)
            {
                damageRules =
                    GetComponent<BossPartDamageRules>();
            }

            if (damageRules == null)
            {
                damageRules =
                    GetComponentInParent
                    <
                        BossPartDamageRules
                    >(true);
            }

            if (damageRules == null &&
                topRoot != null)
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

            if (partRootController == null &&
                topRoot != null)
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
                    GetComponent<BossController>();
            }

            if (bossController == null)
            {
                bossController =
                    GetComponentInParent
                    <
                        BossController
                    >(true);
            }

            if (bossController == null &&
                topRoot != null)
            {
                bossController =
                    topRoot.GetComponentInChildren
                    <
                        BossController
                    >(true);
            }
        }


        // =========================================================
        // Event Subscription
        // =========================================================

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


            if (debugLog)
            {
                Debug.Log(
                    $"[BossPressureGauge] DamageApplied 이벤트 연결 | " +
                    $"Rules={subscribedDamageRules.gameObject.name}",
                    this
                );
            }
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


        // =========================================================
        // Damage
        // =========================================================

        /// <summary>
        /// 실제 BossPartDamageRules의 이벤트 타입은
        /// Action&lt;BossDamageEventData&gt; 입니다.
        /// </summary>
        private void HandleBossDamageApplied(
            BossDamageEventData eventData)
        {
            if (pressureFull ||
                IsBossDead())
            {
                return;
            }


            // BossSelf / Environment 등의 피해는
            // 압력 게이지에 포함하지 않습니다.
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
                if (debugLog)
                {
                    Debug.LogWarning(
                        "[BossPressureGauge] " +
                        "Threshold가 아직 0입니다. " +
                        "BossPartDamageTestRootController.TotalMaxHealth를 확인하세요.",
                        this
                    );
                }

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
                    $"Part={(eventData.Part != null ? eventData.Part.PartType.ToString() : "NULL")} | " +
                    $"Source={eventData.DamageSource}",
                    this
                );
            }


            if (!pressureFull &&
                pressureNormalized >= 1f)
            {
                SetPressureFull();
            }
        }


        // =========================================================
        // Phase
        // =========================================================

        private void HandlePhaseChanged(
            int newPhase)
        {
            int previousPhase =
                currentPhase;


            currentPhase =
                Mathf.Max(
                    1,
                    newPhase);


            bool isRealPhaseChange =
                previousPhase > 0 &&
                previousPhase != currentPhase;


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


        // =========================================================
        // Gauge
        // =========================================================

        private void SetPressureFull()
        {
            if (pressureFull)
            {
                return;
            }


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
                    $"Threshold={pressureThresholdDamage:0.##}",
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


        // =========================================================
        // UI
        // =========================================================

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


            if (gaugeUI == null)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] BossPressureGaugeUI 생성 실패.",
                    this
                );

                return;
            }


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


        // =========================================================
        // Boss State
        // =========================================================

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
                    $"Reason={reason}, " +
                    $"Phase={currentPhase}",
                    this
                );
            }
        }

        public void ConsumePressure(
    string reason)
        {
            ResetPressureInternal(
                string.IsNullOrEmpty(reason)
                    ? "Pressure Overload Resolved"
                    : reason);
        }

        // =========================================================
        // Debug
        // =========================================================

        [ContextMenu("Debug/Add 10% Boss Max HP Pressure")]
        private void DebugAddTenPercentPressure()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] Play Mode에서 실행하세요.",
                    this
                );

                return;
            }


            RecalculateThreshold();


            if (partRootController == null ||
                partRootController.TotalMaxHealth <= 0f)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] 보스 Max HP가 아직 준비되지 않았습니다.",
                    this
                );

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


            if (debugLog)
            {
                Debug.Log(
                    $"[BossPressureGauge] DEBUG +10% MaxHP | " +
                    $"{accumulatedPressureDamage:0.##}/" +
                    $"{pressureThresholdDamage:0.##}",
                    this
                );
            }


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
                    this
                );

                return;
            }


            RecalculateThreshold();


            if (pressureThresholdDamage <= 0f)
            {
                Debug.LogWarning(
                    "[BossPressureGauge] Threshold가 아직 준비되지 않았습니다.",
                    this
                );

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
                    this
                );

                return;
            }


            ResetPressureInternal(
                "Debug Context Menu");
        }
    }
}
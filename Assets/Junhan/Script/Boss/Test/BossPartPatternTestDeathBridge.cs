using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 파츠 보스의 합산 HP / 사망 상태를 기존 BossController에 전달하는 Bridge입니다.
    ///
    /// 역할:
    /// - BossPartDamageTestRootController의 합산 HP를 BossController에 초기화
    /// - 파츠 피해로 합산 HP가 변할 때 BossController HP를 동기화
    /// - BossController의 HP 기반 Phase 1 / 2 / 3 전환을 실제 파츠 HP와 연결
    /// - Core 파괴 등으로 파츠 보스가 사망하면 BossController 패턴 루프도 중지
    ///
    /// 현재 테스트 클래스 이름은 유지하지만,
    /// 이후 정식 파츠 시스템으로 승격할 때 동일 역할을 Runtime Bridge로 이전할 수 있습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BossPartPatternTestDeathBridge :
        MonoBehaviour
    {
        [Header("References")]
        [Tooltip(
            "6개 파츠의 합산 HP와 Core 사망을 관리하는 RootController입니다. " +
            "비워 두면 같은 GameObject 또는 자식에서 자동 탐색합니다.")]
        [SerializeField]
        private BossPartDamageTestRootController partRootController;

        [Tooltip(
            "패턴 / 페이즈 / 코어 상태를 관리하는 BossController입니다. " +
            "비워 두면 같은 GameObject 또는 부모/자식에서 자동 탐색합니다.")]
        [SerializeField]
        private BossController bossController;

        [Header("Health Sync")]
        [Tooltip(
            "체크하면 파츠 합산 HP를 BossController에 동기화합니다. " +
            "이 기능이 켜져 있어야 파츠를 때렸을 때 60%/30% 기준 페이즈 전환이 동작합니다.")]
        [SerializeField]
        private bool synchronizePartHealthToBossController = true;

        [Tooltip(
            "HP 변화 감지 시 이 값보다 작은 차이는 무시합니다. " +
            "부동소수점 비교용이므로 기본값 유지 권장입니다.")]
        [SerializeField, Min(0f)]
        private float healthChangeEpsilon = 0.001f;

        [Header("Death Sync")]
        [Tooltip(
            "Core 파괴 등으로 파츠 보스가 사망하면 BossController의 사망 처리를 호출합니다.")]
        [SerializeField]
        private bool stopBossControllerOnPartBossDeath = true;

        [Header("Runtime")]
        [Tooltip("파츠 HP를 BossController에 최초 초기화했는지 표시합니다.")]
        [SerializeField]
        private bool healthInitialized;

        [Tooltip("마지막으로 BossController에 전달한 현재 HP입니다.")]
        [SerializeField]
        private float lastForwardedCurrentHealth;

        [Tooltip("마지막으로 BossController에 전달한 최대 HP입니다.")]
        [SerializeField]
        private float lastForwardedMaxHealth;

        [Tooltip("파츠 보스 사망을 BossController에 이미 전달했는지 표시합니다.")]
        [SerializeField]
        private bool deathForwarded;

        [Header("Debug")]
        [Tooltip(
            "HP 초기화/동기화, 페이즈 연동, 사망 전달 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = true;

        private void Awake()
        {
            ResolveReferences();
            ResetRuntimeState();
        }

        private void OnEnable()
        {
            ResolveReferences();
            ResetRuntimeState();
        }

        private void Start()
        {
            ResolveReferences();
            TrySynchronizeHealth(true);
            TryForwardDeath();
        }

        private void Update()
        {
            ResolveReferences();

            TrySynchronizeHealth(false);
            TryForwardDeath();
        }

        /// <summary>
        /// Inspector Context Menu에서 강제로 현재 파츠 HP를 BossController에 다시 전달합니다.
        /// Play Mode 디버깅용입니다.
        /// </summary>
        [ContextMenu("Force Sync Part HP To BossController")]
        private void ForceSyncHealthContextMenu()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[BossPartPatternBridge] Play Mode에서 실행하세요.",
                    this);

                return;
            }

            healthInitialized = false;
            TrySynchronizeHealth(true);
        }

        private void TrySynchronizeHealth(
            bool force)
        {
            if (!synchronizePartHealthToBossController)
            {
                return;
            }

            if (partRootController == null ||
                bossController == null)
            {
                return;
            }

            float maximumHealth =
                partRootController.TotalMaxHealth;

            // Root/Part의 Awake-OnEnable-Start 순서상
            // 최초 프레임에는 아직 파츠 수집 전이라 Max HP가 0일 수 있습니다.
            // 이 경우 잘못된 1 HP 초기화를 하지 않고 다음 프레임에 재시도합니다.
            if (maximumHealth <= 0f)
            {
                return;
            }

            float currentHealth =
                Mathf.Clamp(
                    partRootController.CurrentBossHealth,
                    0f,
                    maximumHealth);

            if (!healthInitialized)
            {
                bossController.NotifyBossHealthInitialized(
                    currentHealth,
                    maximumHealth);

                healthInitialized = true;

                lastForwardedCurrentHealth =
                    currentHealth;

                lastForwardedMaxHealth =
                    maximumHealth;

                if (debugLog)
                {
                    Debug.Log(
                        $"[BossPartPatternBridge] " +
                        $"Boss HP 최초 동기화 | " +
                        $"{currentHealth:0.##}/{maximumHealth:0.##}",
                        this);
                }

                return;
            }

            bool currentChanged =
                Mathf.Abs(
                    currentHealth -
                    lastForwardedCurrentHealth) >
                healthChangeEpsilon;

            bool maximumChanged =
                Mathf.Abs(
                    maximumHealth -
                    lastForwardedMaxHealth) >
                healthChangeEpsilon;

            if (!force &&
                !currentChanged &&
                !maximumChanged)
            {
                return;
            }

            bossController.NotifyBossHealthChanged(
                currentHealth,
                maximumHealth);

            lastForwardedCurrentHealth =
                currentHealth;

            lastForwardedMaxHealth =
                maximumHealth;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossPartPatternBridge] " +
                    $"Boss HP 동기화 | " +
                    $"{currentHealth:0.##}/{maximumHealth:0.##} | " +
                    $"Normalized=" +
                    $"{(maximumHealth > 0f ? currentHealth / maximumHealth : 0f):0.000} | " +
                    $"BossPhase={bossController.CurrentPhase}",
                    this);
            }
        }

        private void TryForwardDeath()
        {
            if (deathForwarded ||
                !stopBossControllerOnPartBossDeath)
            {
                return;
            }

            if (partRootController == null ||
                bossController == null)
            {
                return;
            }

            if (!partRootController.IsBossDead)
            {
                return;
            }

            deathForwarded = true;

            // 사망 직전 HP도 마지막으로 0에 맞춰둡니다.
            if (synchronizePartHealthToBossController &&
                partRootController.TotalMaxHealth > 0f)
            {
                bossController.NotifyBossHealthChanged(
                    0f,
                    partRootController.TotalMaxHealth);

                lastForwardedCurrentHealth = 0f;
                lastForwardedMaxHealth =
                    partRootController.TotalMaxHealth;
            }

            if (!bossController.IsDead)
            {
                bossController.NotifyBossDeathStarted();
            }

            if (debugLog)
            {
                Debug.Log(
                    "[BossPartPatternBridge] " +
                    "파츠 보스 사망 감지 → " +
                    "BossController 사망/패턴 정지 전달",
                    this);
            }
        }

        private void ResolveReferences()
        {
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
                    GetComponentInChildren
                    <
                        BossPartDamageTestRootController
                    >(true);
            }

            if (partRootController == null)
            {
                partRootController =
                    GetComponentInParent
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
                    GetComponentInChildren
                    <
                        BossController
                    >(true);
            }

            if (bossController == null)
            {
                bossController =
                    GetComponentInParent
                    <
                        BossController
                    >(true);
            }
        }

        private void ResetRuntimeState()
        {
            healthInitialized = false;
            deathForwarded = false;

            lastForwardedCurrentHealth = -1f;
            lastForwardedMaxHealth = -1f;
        }
    }
}

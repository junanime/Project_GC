using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    [Serializable]
    public class BossPhaseSettings
    {
        [Header("Phase Identity")]
        [Tooltip("인스펙터에서 구분하기 위한 페이즈 이름입니다.")]
        public string phaseName = "Phase";

        [Tooltip(
            "이 페이즈가 시작되는 HP 비율입니다. " +
            "0.6이면 보스 HP가 60% 이하일 때 진입합니다.")]
        [Range(0f, 1f)]
        public float enterHpRatio = 1f;

        [Header("Power Multipliers")]
        [Tooltip("보스의 기본 공격, 접촉 피해, 패턴 피해에 곱해지는 배율입니다.")]
        public float damageMultiplier = 1f;

        [Tooltip("보스의 기본 공격 탄환과 패턴 발사체 속도에 곱해지는 배율입니다.")]
        public float projectileSpeedMultiplier = 1f;

        [Tooltip("보스의 추적 이동과 돌진 이동속도에 곱해지는 배율입니다.")]
        public float movementSpeedMultiplier = 1f;

        [Header("Attack Timing Multipliers")]
        [Tooltip("기본 공격 쿨타임에 곱해지는 배율입니다.")]
        public float basicAttackCooldownMultiplier = 1f;

        [Tooltip("각 패턴 자체 쿨타임에 곱해지는 배율입니다.")]
        public float patternCooldownMultiplier = 1f;

        [Tooltip("다음 특수 패턴까지의 전체 간격에 곱해지는 배율입니다.")]
        public float patternIntervalMultiplier = 1f;

        [Tooltip("패턴 종료 뒤 짧은 후딜 시간에 곱해지는 배율입니다.")]
        public float patternGapMultiplier = 1f;

        [Header("Phase Transition")]
        [Tooltip("이 페이즈로 넘어갈 때 보스가 정지하는 시간입니다.")]
        public float transitionPauseSeconds = 2f;

        [Tooltip("체크하면 페이즈 전환 중 보스가 피해를 받지 않습니다.")]
        public bool invincibleDuringTransition = true;

        [Tooltip(
            "이 페이즈 시작 시 교체할 스프라이트입니다. " +
            "비워두면 기존 스프라이트를 유지합니다.")]
        public Sprite phaseSprite;

        [Tooltip(
            "이 페이즈 시작 시 생성할 이펙트입니다. " +
            "비워두면 생성하지 않습니다.")]
        public GameObject phaseTransitionEffectPrefab;

        public BossPhaseSettings()
        {
        }

        public BossPhaseSettings(
            string phaseName,
            float enterHpRatio,
            float damageMultiplier,
            float projectileSpeedMultiplier,
            float movementSpeedMultiplier,
            float basicAttackCooldownMultiplier,
            float patternCooldownMultiplier,
            float patternIntervalMultiplier,
            float patternGapMultiplier,
            float transitionPauseSeconds,
            bool invincibleDuringTransition)
        {
            this.phaseName = phaseName;
            this.enterHpRatio = enterHpRatio;
            this.damageMultiplier = damageMultiplier;
            this.projectileSpeedMultiplier = projectileSpeedMultiplier;
            this.movementSpeedMultiplier = movementSpeedMultiplier;
            this.basicAttackCooldownMultiplier = basicAttackCooldownMultiplier;
            this.patternCooldownMultiplier = patternCooldownMultiplier;
            this.patternIntervalMultiplier = patternIntervalMultiplier;
            this.patternGapMultiplier = patternGapMultiplier;
            this.transitionPauseSeconds = transitionPauseSeconds;
            this.invincibleDuringTransition = invincibleDuringTransition;
        }
    }

    /// <summary>
    /// 크리피커피 보스의 공통 전투 제어기입니다.
    ///
    /// 담당:
    /// - 이동
    /// - 기본 연발탄
    /// - 페이즈
    /// - 패턴 스케줄링
    /// - 현재 코어에 따른 패턴 후보 필터링
    ///
    /// 개별 공격 로직은 BossPatternBase 파생 클래스에 둡니다.
    /// </summary>
    public class BossController : MonoBehaviour
    {
        [Header("Boss References")]
        [Tooltip(
            "플레이어 캐릭터입니다. " +
            "비워두면 시작 시 씬에서 자동으로 찾습니다.")]
        [SerializeField]
        private Character playerCharacter;

        [Tooltip(
            "보스 Rigidbody2D입니다. " +
            "비워두면 현재 오브젝트에서 자동 탐색합니다.")]
        [SerializeField]
        private Rigidbody2D rb;

        [Tooltip(
            "보스 대표 SpriteRenderer입니다. " +
            "페이즈 스프라이트 교체에 사용합니다.")]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "보스의 코어 상태 관리자입니다. " +
            "비워두면 현재 오브젝트/자식에서 찾고, 없으면 옵션에 따라 자동 추가합니다.")]
        [SerializeField]
        private BossCoreStateController coreStateController;

        [Tooltip(
            "BossCoreStateController가 없을 때 BossController 오브젝트에 자동 추가합니다.")]
        [SerializeField]
        private bool autoCreateCoreStateController = true;

        [Tooltip(
            "보스가 사용할 특수 패턴 목록입니다. " +
            "Auto Collect가 켜져 있으면 자식까지 자동 수집합니다.")]
        [SerializeField]
        private List<BossPatternBase> patterns =
            new List<BossPatternBase>();

        [Tooltip(
            "체크하면 시작 시 보스 본체와 자식에서 " +
            "BossPatternBase 파생 패턴을 자동 수집합니다.")]
        [SerializeField]
        private bool autoCollectPatternsFromChildren = true;

        [Header("Boss HP")]
        [Tooltip(
            "보스 최대 체력입니다. " +
            "기존 BossMonster/파츠 시스템에서 값이 전달되면 동기화됩니다.")]
        [SerializeField]
        private float maxHp = 500f;

        [Tooltip("현재 보스 체력입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private float currentHp = 500f;

        [Header("Contact Damage")]
        [Tooltip("보스 몸과 접촉했을 때의 기본 피해입니다.")]
        [SerializeField]
        private float contactDamage = 10f;

        [Tooltip("접촉 피해 재적용 간격입니다.")]
        [SerializeField]
        private float contactDamageCooldown = 0.6f;

        [Header("Basic Attack")]
        [Tooltip("체크하면 보스가 기본 연발탄을 사용합니다.")]
        [SerializeField]
        private bool enableBasicAttack = true;

        [Tooltip("기본 공격 탄환 프리팹입니다.")]
        [SerializeField]
        private GameObject basicAttackBulletPrefab;

        [Tooltip("기본 공격 묶음 사이 쿨타임입니다.")]
        [SerializeField]
        private float basicAttackCooldown = 1.6f;

        [Tooltip("기본 공격 피해입니다.")]
        [SerializeField]
        private float basicAttackDamage = 5f;

        [Tooltip("기본 공격 탄속입니다.")]
        [SerializeField]
        private float basicAttackBulletSpeed = 4.5f;

        [Tooltip("보스 중심에서 기본 탄환을 생성할 앞쪽 오프셋입니다.")]
        [SerializeField]
        private float basicAttackMuzzleOffset = 1.2f;

        [Tooltip(
            "호환용 옵션입니다. 현재는 패턴과 연발탄이 겹치지 않도록 " +
            "특수 패턴 실행 중 기본 공격을 보류합니다.")]
        [SerializeField]
        private bool basicAttackWhileUsingPattern = false;

        [Header("Basic Attack Core Rule")]
        [Tooltip(
            "기본 연발탄이 사용되는 코어 속성입니다. " +
            "현재 기획 기준 Red입니다.")]
        [SerializeField]
        private BossCoreTrait basicAttackCoreTraits =
            BossCoreTrait.Red;

        [Tooltip("기본 연발탄의 코어 속성 비교 방식입니다.")]
        [SerializeField]
        private BossPatternCoreMatchMode basicAttackCoreMatchMode =
            BossPatternCoreMatchMode.Any;

        [Header("Basic Attack Burst")]
        [Tooltip("1페이즈 기본 공격 한 묶음 탄환 수입니다.")]
        [SerializeField]
        private int basicAttackBurstCountPhase1 = 4;

        [Tooltip("2페이즈 기본 공격 한 묶음 탄환 수입니다.")]
        [SerializeField]
        private int basicAttackBurstCountPhase2 = 5;

        [Tooltip("3페이즈 기본 공격 한 묶음 탄환 수입니다.")]
        [SerializeField]
        private int basicAttackBurstCountPhase3 = 7;

        [Tooltip("연발탄 내부 발사 간격입니다.")]
        [SerializeField]
        private float basicAttackBurstInterval = 0.2f;

        [Tooltip("체크하면 각 연발탄마다 플레이어 위치를 다시 조준합니다.")]
        [SerializeField]
        private bool reAimEachBasicBurstShot = true;

        [Header("Basic Attack Aim")]
        [Tooltip("체크하면 플레이어 이동을 예측해서 앞쪽을 조준합니다.")]
        [SerializeField]
        private bool usePredictiveBasicAim = true;

        [Tooltip("몇 초 뒤 플레이어 위치를 예측할지 결정합니다.")]
        [SerializeField]
        private float basicAimLeadTime = 0.45f;

        [Tooltip("기본 조준 랜덤 오차 각도입니다.")]
        [SerializeField]
        private float basicAimInaccuracyAngle = 5f;

        [Tooltip(
            "플레이어 속도가 이 값보다 낮으면 예측 조준을 사용하지 않습니다.")]
        [SerializeField]
        private float minPlayerVelocityForPrediction = 0.1f;

        [Header("Basic Attack Visual Sorting")]
        [Tooltip("체크하면 기본 탄환 Sorting Order를 강제합니다.")]
        [SerializeField]
        private bool forceBasicBulletSortingOrder = true;

        [Tooltip("기본 탄환 Sorting Order입니다.")]
        [SerializeField]
        private int basicBulletSortingOrder = 500;

        [Header("Movement")]
        [Tooltip("체크하면 보스가 플레이어를 추적합니다.")]
        [SerializeField]
        private bool enableMovement = true;

        [Tooltip("보스 기본 이동속도입니다.")]
        [SerializeField]
        private float baseMoveSpeed = 0.8f;

        [Tooltip("플레이어와 이 거리 이하가 되면 추적 이동을 멈춥니다.")]
        [SerializeField]
        private float stopDistanceFromPlayer = 3f;

        [Tooltip("체크하면 특수 패턴 실행 중에도 천천히 이동합니다.")]
        [SerializeField]
        private bool moveWhileUsingPattern = true;

        [Tooltip("특수 패턴 실행 중 이동속도 배율입니다.")]
        [SerializeField]
        private float patternMoveSpeedMultiplier = 0.35f;

        [Tooltip("보스 추적 이동 보간 시간입니다.")]
        [SerializeField]
        private float movementSmoothTime = 0.18f;

        [Tooltip("체크하면 대표 스프라이트를 플레이어 방향으로 Flip합니다.")]
        [SerializeField]
        private bool flipSpriteToPlayer = true;

        [Header("Special Pattern Timing")]
        [Tooltip("보스 등장 후 첫 특수 패턴까지의 대기시간입니다.")]
        [SerializeField]
        private float firstPatternDelay = 3f;

        [Tooltip("특수 패턴 사용 간격 최소값입니다.")]
        [SerializeField]
        private float patternIntervalMin = 4f;

        [Tooltip("특수 패턴 사용 간격 최대값입니다.")]
        [SerializeField]
        private float patternIntervalMax = 7f;

        [Tooltip("패턴 사용 가능 여부를 다시 확인하는 간격입니다.")]
        [SerializeField]
        private float thinkInterval = 0.3f;

        [Tooltip("패턴 종료 후 다음 행동까지의 후딜입니다.")]
        [SerializeField]
        private float patternGap = 1.2f;

        [Header("Phase Settings")]
        [Tooltip(
            "1페이즈 / Single Core 설정입니다. " +
            "기본 배율은 100%를 권장합니다.")]
        [SerializeField]
        private BossPhaseSettings phase1Settings =
            new BossPhaseSettings(
                "Phase 1 - Single Core",
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                1f,
                0f,
                false);

        [Tooltip(
            "2페이즈 / Dual Core 설정입니다. " +
            "최종 기획 기준 HP 60% 이하 진입을 권장합니다.")]
        [SerializeField]
        private BossPhaseSettings phase2Settings =
            new BossPhaseSettings(
                "Phase 2 - Dual Core",
                0.6f,
                1.15f,
                1.10f,
                1.10f,
                0.95f,
                0.95f,
                0.90f,
                0.95f,
                2f,
                true);

        [Tooltip(
            "3페이즈 / Rainbow Core 설정입니다. " +
            "최종 기획 기준 HP 30% 이하 진입을 권장합니다. " +
            "기존 영구 2배 광폭 대신 완만한 기본 강화만 사용합니다.")]
        [SerializeField]
        private BossPhaseSettings phase3Settings =
            new BossPhaseSettings(
                "Phase 3 - Rainbow Core",
                0.3f,
                1.20f,
                1.15f,
                1.15f,
                0.90f,
                0.85f,
                0.85f,
                0.90f,
                2f,
                true);

        [Header("Debug")]
        [Tooltip("보스 이동 로그를 출력합니다.")]
        [SerializeField]
        private bool debugMovement = false;

        [Tooltip("보스 패턴 선택/실행 로그를 출력합니다.")]
        [SerializeField]
        private bool debugPattern = false;

        [Tooltip("기본 공격 로그를 출력합니다.")]
        [SerializeField]
        private bool debugBasicAttack = false;

        [Tooltip("페이즈 전환 로그를 출력합니다.")]
        [SerializeField]
        private bool debugPhase = true;

        [Tooltip("코어 상태/필터링 로그를 출력합니다.")]
        [SerializeField]
        private bool debugCore = false;

        private bool isDead;
        private bool isUsingPattern;
        private bool isPhaseTransitioning;
        private bool isInvincibleByPhase;
        private bool healthInitializedFromMonster;

        private int currentPhase = 1;
        private float lastContactDamageTime = -999f;

        private Vector2 smoothMoveVelocity;
        private bool externalMovementLock;
        private bool suppressContactDamage;

        private float basicAttackTimer;
        private bool isBasicAttackBursting;

        private float nextPatternTime;

        private Vector2 lastPlayerPosition;
        private Vector2 estimatedPlayerVelocity;

        private BossPatternBase currentPattern;
        private bool patternLifecycleActive;

        private Coroutine patternLoopCoroutine;
        private Coroutine activePatternCoroutine;
        private Coroutine phaseTransitionCoroutine;
        private Coroutine basicAttackBurstCoroutine;

        public int CurrentPhase => currentPhase;
        public Character PlayerCharacter => playerCharacter;
        public Vector3 BossCenterPosition => transform.position;
        public Rigidbody2D Rigidbody => rb;
        public BossCoreStateController CoreStateController => coreStateController;
        public BossCoreTrait ActiveCoreTraits =>
            coreStateController != null
                ? coreStateController.ActiveTraits
                : BossCoreTrait.All;

        public BossPatternBase CurrentPattern => currentPattern;
        public bool IsDead => isDead;
        public bool IsUsingPattern => isUsingPattern;
        public bool IsPhaseTransitioning => isPhaseTransitioning;
        public bool IsInvincibleToDamage =>
            isDead ||
            isPhaseTransitioning ||
            isInvincibleByPhase;

        /// <summary>
        /// 패턴 시작 시 호출됩니다.
        /// 이후 Core Guard가 팔을 열거나, 보스 UI/VFX가 반응할 때 사용합니다.
        /// </summary>
        public event Action<BossPatternBase> PatternStarted;

        /// <summary>
        /// 패턴 종료 시 호출됩니다.
        /// </summary>
        public event Action<BossPatternBase> PatternEnded;

        /// <summary>
        /// 페이즈가 변경됐을 때 호출됩니다.
        /// </summary>
        public event Action<int> PhaseChanged;

        /// <summary>
        /// BossController 자체 HP가 피해를 받았을 때 호출됩니다.
        /// 파츠 기반 보스는 다음 파츠 정식화 단계에서 동일 Damage Source 구조로 연결합니다.
        /// </summary>
        public event Action<float, BossDamageSourceType> DamageTaken;

        private void Awake()
        {
            ResolveReferences();

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void Start()
        {
            if (playerCharacter == null)
            {
                playerCharacter =
                    FindObjectOfType<Character>();
            }

            if (playerCharacter != null)
            {
                lastPlayerPosition =
                    playerCharacter.transform.position;
            }

            CollectPatternsIfNeeded();

            if (!healthInitializedFromMonster)
            {
                currentHp = maxHp;
            }

            currentPhase = 1;
            ApplyPhaseVisual(phase1Settings);

            for (int i = 0;
                 i < patterns.Count;
                 i++)
            {
                BossPatternBase pattern = patterns[i];

                if (pattern != null)
                {
                    pattern.Init(this);
                }
            }

            InitializeCoreState();

            basicAttackTimer = 0f;
            isBasicAttackBursting = false;

            ScheduleNextPattern(firstPatternDelay);
            StartPatternLoop();

            PhaseChanged?.Invoke(currentPhase);

            if (debugPattern)
            {
                Debug.Log(
                    $"[BossController] Pattern Count={patterns.Count}, " +
                    $"ActiveCore={ActiveCoreTraits}",
                    this);
            }
        }

        private void OnDisable()
        {
            StopPatternLoop();
            StopBasicAttackBurst();

            if (coreStateController != null)
            {
                coreStateController.SetPatternLocked(false);
            }
        }

        private void Update()
        {
            UpdatePlayerVelocityEstimate();
            UpdateSpriteFlip();
            UpdateBasicAttack();
        }

        private void FixedUpdate()
        {
            UpdateMovement();
        }

        private void ResolveReferences()
        {
            if (rb == null)
            {
                rb =
                    GetComponent<Rigidbody2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren
                    <
                        SpriteRenderer
                    >(true);
            }

            ResolveCoreStateController();
        }

        private void ResolveCoreStateController()
        {
            if (coreStateController != null)
            {
                return;
            }

            coreStateController =
                GetComponent
                <
                    BossCoreStateController
                >();

            if (coreStateController == null)
            {
                coreStateController =
                    GetComponentInChildren
                    <
                        BossCoreStateController
                    >(true);
            }

            if (coreStateController == null &&
                autoCreateCoreStateController)
            {
                coreStateController =
                    gameObject.AddComponent
                    <
                        BossCoreStateController
                    >();

                if (debugCore)
                {
                    Debug.Log(
                        "[BossController] BossCoreStateController 자동 추가",
                        this);
                }
            }
        }

        private void InitializeCoreState()
        {
            ResolveCoreStateController();

            if (coreStateController == null)
            {
                Debug.LogWarning(
                    "[BossController] BossCoreStateController가 없습니다. " +
                    "패턴 코어 제한 없이 All 상태로 동작합니다.",
                    this);
                return;
            }

            coreStateController.Initialize(currentPhase);

            if (debugCore)
            {
                Debug.Log(
                    $"[BossController] Core 초기화 | " +
                    $"Mode={coreStateController.CurrentMode}, " +
                    $"Traits={coreStateController.ActiveTraits}",
                    this);
            }
        }

        private void CollectPatternsIfNeeded()
        {
            if (!autoCollectPatternsFromChildren &&
                patterns.Count > 0)
            {
                return;
            }

            patterns.Clear();

            BossPatternBase[] foundPatterns =
                GetComponentsInChildren
                <
                    BossPatternBase
                >(true);

            for (int i = 0;
                 i < foundPatterns.Length;
                 i++)
            {
                BossPatternBase pattern =
                    foundPatterns[i];

                if (pattern != null &&
                    !patterns.Contains(pattern))
                {
                    patterns.Add(pattern);
                }
            }
        }

        public void SetPlayerCharacter(
            Character character)
        {
            playerCharacter = character;

            if (playerCharacter != null)
            {
                lastPlayerPosition =
                    playerCharacter.transform.position;
            }
        }

        public void NotifyBossHealthInitialized(
            float currentHealth,
            float maximumHealth)
        {
            maxHp =
                Mathf.Max(1f, maximumHealth);

            currentHp =
                Mathf.Clamp(
                    currentHealth,
                    0f,
                    maxHp);

            healthInitializedFromMonster = true;

            if (debugPhase)
            {
                Debug.Log(
                    $"[BossController] HP 초기화 | " +
                    $"{currentHp:0.##}/{maxHp:0.##}",
                    this);
            }
        }

        public void NotifyBossHealthChanged(
            float currentHealth,
            float maximumHealth)
        {
            maxHp =
                Mathf.Max(1f, maximumHealth);

            currentHp =
                Mathf.Clamp(
                    currentHealth,
                    0f,
                    maxHp);

            if (currentHp <= 0f)
            {
                return;
            }

            TryStartNextPhaseByHp();
        }

        public void NotifyBossDeathStarted()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;
            isPhaseTransitioning = false;
            isInvincibleByPhase = false;

            StopPatternLoop();
            StopBasicAttackBurst();
            StopMovementVelocity();

            if (debugPhase)
            {
                Debug.Log(
                    "[BossController] Boss death started. " +
                    "Pattern loop stopped.",
                    this);
            }
        }

        public void SetExternalMovementLock(
            bool value)
        {
            externalMovementLock = value;

            if (value)
            {
                StopMovementVelocity();
            }
        }

        public void SetSuppressContactDamage(
            bool value)
        {
            suppressContactDamage = value;
        }

        public float GetModifiedDamage(
            float baseDamage)
        {
            return
                baseDamage *
                GetCurrentPhaseSettings()
                    .damageMultiplier;
        }

        public float GetModifiedProjectileSpeed(
            float baseSpeed)
        {
            return
                baseSpeed *
                GetCurrentPhaseSettings()
                    .projectileSpeedMultiplier;
        }

        public float GetModifiedMovementSpeed(
            float baseSpeed)
        {
            return
                baseSpeed *
                GetCurrentPhaseSettings()
                    .movementSpeedMultiplier;
        }

        public float GetModifiedPatternCooldown(
            float baseCooldown)
        {
            return Mathf.Max(
                0.05f,
                baseCooldown *
                GetCurrentPhaseSettings()
                    .patternCooldownMultiplier);
        }

        /// <summary>
        /// 기존 3페이즈 상시 피해감소를 제거했습니다.
        /// 이후 코어 상태/아이템/기믹에 의한 피해 배율은 별도 Damage Pipeline에서 확장합니다.
        /// </summary>
        public float GetModifiedIncomingDamage(
            float incomingDamage)
        {
            return Mathf.Max(
                0f,
                incomingDamage);
        }

        public bool IsCoreTraitActive(
            BossCoreTrait traits)
        {
            if (coreStateController == null)
            {
                return true;
            }

            return
                BossCoreTraitUtility.Matches(
                    coreStateController.ActiveTraits,
                    traits,
                    BossPatternCoreMatchMode.Any);
        }

        private bool IsBasicAttackAllowedByCore()
        {
            if (coreStateController == null)
            {
                return true;
            }

            return
                BossCoreTraitUtility.Matches(
                    coreStateController.ActiveTraits,
                    basicAttackCoreTraits,
                    basicAttackCoreMatchMode);
        }

        private void UpdatePlayerVelocityEstimate()
        {
            if (playerCharacter == null)
            {
                return;
            }

            Vector2 currentPlayerPosition =
                playerCharacter.transform.position;

            if (Time.deltaTime > 0f)
            {
                estimatedPlayerVelocity =
                    (
                        currentPlayerPosition -
                        lastPlayerPosition
                    ) /
                    Time.deltaTime;
            }

            lastPlayerPosition =
                currentPlayerPosition;
        }

        private void UpdateBasicAttack()
        {
            if (!enableBasicAttack ||
                isDead ||
                isPhaseTransitioning ||
                playerCharacter == null)
            {
                return;
            }

            if (basicAttackBulletPrefab == null)
            {
                return;
            }

            if (!IsBasicAttackAllowedByCore())
            {
                // 다른 코어에서 충전된 타이머를 Red 진입 직후 즉발시키지 않습니다.
                basicAttackTimer = 0f;
                return;
            }

            if (isBasicAttackBursting)
            {
                return;
            }

            if (isUsingPattern)
            {
                if (basicAttackWhileUsingPattern &&
                    debugBasicAttack)
                {
                    Debug.Log(
                        "[BossController] 패턴 중 기본탄은 " +
                        "충돌 방지를 위해 보류됩니다.",
                        this);
                }

                return;
            }

            float finalCooldown =
                Mathf.Max(
                    0.05f,
                    basicAttackCooldown *
                    GetCurrentPhaseSettings()
                        .basicAttackCooldownMultiplier);

            basicAttackTimer +=
                Time.deltaTime;

            if (basicAttackTimer >=
                finalCooldown)
            {
                basicAttackTimer = 0f;
                StartBasicAttackBurst();
            }
        }

        private void StartBasicAttackBurst()
        {
            if (basicAttackBurstCoroutine != null)
            {
                StopCoroutine(
                    basicAttackBurstCoroutine);
            }

            basicAttackBurstCoroutine =
                StartCoroutine(
                    BasicAttackBurstRoutine());
        }

        private IEnumerator BasicAttackBurstRoutine()
        {
            isBasicAttackBursting = true;
            LockCoreSwap(true);

            int burstCount =
                Mathf.Max(
                    1,
                    GetCurrentPhaseBasicAttackBurstCount());

            Vector2 lockedDirection =
                Vector2.right;

            if (!reAimEachBasicBurstShot)
            {
                lockedDirection =
                    GetBasicAttackDirection();
            }

            for (int i = 0;
                 i < burstCount;
                 i++)
            {
                if (isDead ||
                    isPhaseTransitioning ||
                    playerCharacter == null ||
                    basicAttackBulletPrefab == null)
                {
                    break;
                }

                Vector2 direction =
                    reAimEachBasicBurstShot
                        ? GetBasicAttackDirection()
                        : lockedDirection;

                FireSingleBasicAttackBullet(
                    direction,
                    i + 1,
                    burstCount);

                if (i < burstCount - 1)
                {
                    yield return
                        new WaitForSeconds(
                            Mathf.Max(
                                0.01f,
                                basicAttackBurstInterval));
                }
            }

            isBasicAttackBursting = false;
            basicAttackBurstCoroutine = null;
            LockCoreSwap(false);
        }

        private int GetCurrentPhaseBasicAttackBurstCount()
        {
            if (currentPhase >= 3)
            {
                return basicAttackBurstCountPhase3;
            }

            if (currentPhase == 2)
            {
                return basicAttackBurstCountPhase2;
            }

            return basicAttackBurstCountPhase1;
        }

        private Vector2 GetBasicAttackDirection()
        {
            Vector2 origin =
                BossCenterPosition;

            Vector2 aimPosition =
                GetBasicAttackAimPosition();

            Vector2 direction =
                (aimPosition - origin).normalized;

            if (direction == Vector2.zero)
            {
                direction =
                    Vector2.right;
            }

            if (basicAimInaccuracyAngle > 0f)
            {
                float randomAngle =
                    UnityEngine.Random.Range(
                        -basicAimInaccuracyAngle,
                        basicAimInaccuracyAngle);

                direction =
                    RotateVector(
                        direction,
                        randomAngle);
            }

            return direction.normalized;
        }

        private void FireSingleBasicAttackBullet(
            Vector2 direction,
            int shotIndex,
            int burstCount)
        {
            if (direction == Vector2.zero)
            {
                direction =
                    Vector2.right;
            }

            Vector3 spawnPosition =
                BossCenterPosition +
                (Vector3)(
                    direction *
                    basicAttackMuzzleOffset);

            GameObject bullet =
                Instantiate(
                    basicAttackBulletPrefab,
                    spawnPosition,
                    Quaternion.identity);

            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x) *
                Mathf.Rad2Deg;

            bullet.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle);

            ApplyBulletSortingOrder(
                bullet,
                basicBulletSortingOrder);

            BossSimpleBullet simpleBullet =
                bullet.GetComponent
                <
                    BossSimpleBullet
                >();

            if (simpleBullet == null)
            {
                simpleBullet =
                    bullet.GetComponentInChildren
                    <
                        BossSimpleBullet
                    >();
            }

            if (simpleBullet == null)
            {
                simpleBullet =
                    bullet.AddComponent
                    <
                        BossSimpleBullet
                    >();

                Debug.LogWarning(
                    "[BossController] Basic Attack Bullet에 " +
                    "BossSimpleBullet이 없어 자동 추가했습니다.",
                    bullet);
            }

            float finalSpeed =
                GetModifiedProjectileSpeed(
                    basicAttackBulletSpeed);

            float finalDamage =
                GetModifiedDamage(
                    basicAttackDamage);

            simpleBullet.Init(
                direction,
                finalSpeed,
                finalDamage);

            if (debugBasicAttack)
            {
                Debug.Log(
                    $"[BossController] Basic Burst " +
                    $"{shotIndex}/{burstCount} | " +
                    $"Phase={currentPhase}, " +
                    $"Core={ActiveCoreTraits}, " +
                    $"Speed={finalSpeed:0.##}, " +
                    $"Damage={finalDamage:0.##}",
                    this);
            }
        }

        private void StopBasicAttackBurst()
        {
            if (basicAttackBurstCoroutine != null)
            {
                StopCoroutine(
                    basicAttackBurstCoroutine);

                basicAttackBurstCoroutine = null;
            }

            if (isBasicAttackBursting)
            {
                isBasicAttackBursting = false;

                if (!patternLifecycleActive)
                {
                    LockCoreSwap(false);
                }
            }
        }

        private Vector2 GetBasicAttackAimPosition()
        {
            Vector2 currentPlayerPosition =
                playerCharacter.transform.position;

            if (!usePredictiveBasicAim)
            {
                return currentPlayerPosition;
            }

            if (estimatedPlayerVelocity.magnitude <
                minPlayerVelocityForPrediction)
            {
                return currentPlayerPosition;
            }

            return
                currentPlayerPosition +
                estimatedPlayerVelocity *
                basicAimLeadTime;
        }

        private void ApplyBulletSortingOrder(
            GameObject bullet,
            int sortingOrder)
        {
            if (!forceBasicBulletSortingOrder ||
                bullet == null)
            {
                return;
            }

            SpriteRenderer[] renderers =
                bullet.GetComponentsInChildren
                <
                    SpriteRenderer
                >(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                SpriteRenderer renderer =
                    renderers[i];

                if (renderer != null)
                {
                    renderer.sortingOrder =
                        sortingOrder;
                }
            }
        }

        private void UpdateMovement()
        {
            if (!CanMove())
            {
                StopMovementVelocity();
                return;
            }

            Vector2 bossPosition =
                rb != null
                    ? rb.position
                    : (Vector2)transform.position;

            Vector2 playerPosition =
                playerCharacter.transform.position;

            Vector2 toPlayer =
                playerPosition -
                bossPosition;

            float distance =
                toPlayer.magnitude;

            if (distance <=
                stopDistanceFromPlayer)
            {
                StopMovementVelocity();
                return;
            }

            Vector2 direction =
                toPlayer.normalized;

            Vector2 targetPosition =
                playerPosition -
                direction *
                stopDistanceFromPlayer;

            float finalSpeed =
                GetModifiedMovementSpeed(
                    baseMoveSpeed);

            if (isUsingPattern)
            {
                finalSpeed *=
                    patternMoveSpeedMultiplier;
            }

            Vector2 nextPosition =
                Vector2.SmoothDamp(
                    bossPosition,
                    targetPosition,
                    ref smoothMoveVelocity,
                    movementSmoothTime,
                    finalSpeed,
                    Time.fixedDeltaTime);

            float maxStep =
                finalSpeed *
                Time.fixedDeltaTime;

            if (Vector2.Distance(
                    bossPosition,
                    nextPosition) >
                maxStep * 1.5f)
            {
                nextPosition =
                    Vector2.MoveTowards(
                        bossPosition,
                        nextPosition,
                        maxStep);
            }

            if (rb != null)
            {
                rb.MovePosition(
                    nextPosition);
            }
            else
            {
                transform.position =
                    nextPosition;
            }

            if (debugMovement)
            {
                Debug.Log(
                    $"[BossController] Moving | " +
                    $"Phase={currentPhase}, " +
                    $"Core={ActiveCoreTraits}, " +
                    $"Distance={distance:0.00}, " +
                    $"Speed={finalSpeed:0.00}",
                    this);
            }
        }

        private bool CanMove()
        {
            if (!enableMovement ||
                isDead ||
                isPhaseTransitioning ||
                playerCharacter == null ||
                externalMovementLock)
            {
                return false;
            }

            if (isUsingPattern &&
                !moveWhileUsingPattern)
            {
                return false;
            }

            return true;
        }

        private void StopMovementVelocity()
        {
            smoothMoveVelocity =
                Vector2.zero;

            if (rb != null)
            {
                rb.velocity =
                    Vector2.zero;

                rb.angularVelocity =
                    0f;
            }
        }

        private void UpdateSpriteFlip()
        {
            if (!flipSpriteToPlayer ||
                spriteRenderer == null ||
                playerCharacter == null)
            {
                return;
            }

            float directionX =
                playerCharacter.transform.position.x -
                transform.position.x;

            spriteRenderer.flipX =
                directionX < 0f;
        }

        private void StartPatternLoop()
        {
            if (patternLoopCoroutine != null)
            {
                StopCoroutine(
                    patternLoopCoroutine);
            }

            patternLoopCoroutine =
                StartCoroutine(
                    PatternLoop());
        }

        private void StopPatternLoop()
        {
            if (activePatternCoroutine != null)
            {
                StopCoroutine(
                    activePatternCoroutine);

                activePatternCoroutine = null;
            }

            EndActivePatternLifecycle();

            if (patternLoopCoroutine != null)
            {
                StopCoroutine(
                    patternLoopCoroutine);

                patternLoopCoroutine = null;
            }

            isUsingPattern = false;
        }

        private IEnumerator PatternLoop()
        {
            while (!isDead)
            {
                if (!isPhaseTransitioning &&
                    !isUsingPattern &&
                    !isBasicAttackBursting &&
                    playerCharacter != null &&
                    Time.time >= nextPatternTime)
                {
                    BossPatternBase selectedPattern =
                        SelectPattern();

                    if (selectedPattern != null)
                    {
                        yield return
                            StartCoroutine(
                                UsePattern(
                                    selectedPattern));
                    }

                    ScheduleNextPattern();
                }

                yield return
                    new WaitForSeconds(
                        Mathf.Max(
                            0.02f,
                            thinkInterval));
            }
        }

        private IEnumerator UsePattern(
            BossPatternBase pattern)
        {
            if (pattern == null)
            {
                yield break;
            }

            isUsingPattern = true;
            BeginActivePatternLifecycle(pattern);

            if (debugPattern)
            {
                Debug.Log(
                    $"[BossController] Use Pattern | " +
                    $"{pattern.PatternName} | " +
                    $"Phase={currentPhase}, " +
                    $"Core={ActiveCoreTraits}",
                    this);
            }

            activePatternCoroutine =
                StartCoroutine(
                    pattern.Execute());

            yield return
                activePatternCoroutine;

            activePatternCoroutine = null;
            EndActivePatternLifecycle();

            float finalPatternGap =
                Mathf.Max(
                    0f,
                    patternGap *
                    GetCurrentPhaseSettings()
                        .patternGapMultiplier);

            if (finalPatternGap > 0f)
            {
                yield return
                    new WaitForSeconds(
                        finalPatternGap);
            }

            isUsingPattern = false;
        }

        private void BeginActivePatternLifecycle(
            BossPatternBase pattern)
        {
            if (patternLifecycleActive)
            {
                EndActivePatternLifecycle();
            }

            patternLifecycleActive = true;
            currentPattern = pattern;

            LockCoreSwap(true);
            PatternStarted?.Invoke(pattern);
        }

        private void EndActivePatternLifecycle()
        {
            if (!patternLifecycleActive)
            {
                return;
            }

            BossPatternBase endedPattern =
                currentPattern;

            patternLifecycleActive = false;
            currentPattern = null;

            // PatternEnded를 먼저 발생시켜 Core Guard 등이 닫힌 뒤
            // Pending Core Swap이 처리되도록 합니다.
            PatternEnded?.Invoke(
                endedPattern);

            if (!isBasicAttackBursting)
            {
                LockCoreSwap(false);
            }
        }

        private void LockCoreSwap(
            bool locked)
        {
            if (coreStateController != null)
            {
                coreStateController.SetPatternLocked(
                    locked);
            }
        }

        private void ScheduleNextPattern(
            float overrideDelay = -1f)
        {
            float delay;

            if (overrideDelay >= 0f)
            {
                delay =
                    overrideDelay;
            }
            else
            {
                float min =
                    Mathf.Min(
                        patternIntervalMin,
                        patternIntervalMax);

                float max =
                    Mathf.Max(
                        patternIntervalMin,
                        patternIntervalMax);

                delay =
                    UnityEngine.Random.Range(
                        min,
                        max);

                delay *=
                    GetCurrentPhaseSettings()
                        .patternIntervalMultiplier;

                delay =
                    Mathf.Max(
                        0.05f,
                        delay);
            }

            nextPatternTime =
                Time.time +
                delay;

            if (debugPattern)
            {
                Debug.Log(
                    $"[BossController] Next Pattern In " +
                    $"{delay:0.00}s | " +
                    $"Phase={currentPhase}, " +
                    $"Core={ActiveCoreTraits}",
                    this);
            }
        }

        /// <summary>
        /// 거리 가중치를 사용하지 않습니다.
        /// 현재 코어 + 파츠 생존 + 개별 쿨타임만 확인하고
        /// 사용 가능한 후보 중 완전 랜덤으로 하나를 고릅니다.
        /// </summary>
        private BossPatternBase SelectPattern()
        {
            List<BossPatternBase> validPatterns =
                new List<BossPatternBase>();

            BossCoreTrait activeCore =
                ActiveCoreTraits;

            for (int i = 0;
                 i < patterns.Count;
                 i++)
            {
                BossPatternBase pattern =
                    patterns[i];

                if (pattern == null)
                {
                    continue;
                }

                if (!pattern.CanUse())
                {
                    continue;
                }

                if (!pattern.SupportsCore(
                        activeCore))
                {
                    continue;
                }

                validPatterns.Add(
                    pattern);
            }

            if (validPatterns.Count == 0)
            {
                if (debugPattern)
                {
                    Debug.Log(
                        $"[BossController] 사용 가능한 패턴 없음 | " +
                        $"Phase={currentPhase}, Core={activeCore}",
                        this);
                }

                return null;
            }

            BossPatternBase selected =
                validPatterns[
                    UnityEngine.Random.Range(
                        0,
                        validPatterns.Count)];

            if (debugPattern)
            {
                Debug.Log(
                    $"[BossController] Pattern Selected | " +
                    $"{selected.PatternName} | " +
                    $"Core={activeCore} | " +
                    $"Candidates={validPatterns.Count}",
                    this);
            }

            return selected;
        }

        public void TakeDamage(
            float damage)
        {
            TakeDamage(
                damage,
                BossDamageSourceType.Player);
        }

        /// <summary>
        /// 피해 출처를 구분할 수 있는 확장용 진입점입니다.
        /// 현재 기존 호출부와 호환되도록 TakeDamage(float)는 유지합니다.
        /// </summary>
        public void TakeDamage(
            float damage,
            BossDamageSourceType sourceType)
        {
            if (IsInvincibleToDamage)
            {
                return;
            }

            float appliedDamage =
                GetModifiedIncomingDamage(
                    damage);

            if (appliedDamage <= 0f)
            {
                return;
            }

            float before =
                currentHp;

            currentHp =
                Mathf.Max(
                    0f,
                    currentHp -
                    appliedDamage);

            float actualDamage =
                Mathf.Max(
                    0f,
                    before -
                    currentHp);

            if (actualDamage > 0f)
            {
                DamageTaken?.Invoke(
                    actualDamage,
                    sourceType);
            }

            NotifyBossHealthChanged(
                currentHp,
                maxHp);

            if (currentHp <= 0f)
            {
                NotifyBossDeathStarted();
            }
        }

        private void TryStartNextPhaseByHp()
        {
            if (isDead ||
                isPhaseTransitioning)
            {
                return;
            }

            float hpRatio =
                maxHp > 0f
                    ? currentHp / maxHp
                    : 1f;

            if (currentPhase < 2 &&
                hpRatio <=
                phase2Settings.enterHpRatio)
            {
                StartPhaseTransition(2);
                return;
            }

            if (currentPhase < 3 &&
                hpRatio <=
                phase3Settings.enterHpRatio)
            {
                StartPhaseTransition(3);
            }
        }

        private void StartPhaseTransition(
            int targetPhase)
        {
            if (phaseTransitionCoroutine != null)
            {
                StopCoroutine(
                    phaseTransitionCoroutine);
            }

            phaseTransitionCoroutine =
                StartCoroutine(
                    PhaseTransitionRoutine(
                        targetPhase));
        }

        private IEnumerator PhaseTransitionRoutine(
            int targetPhase)
        {
            BossPhaseSettings targetSettings =
                GetPhaseSettings(
                    targetPhase);

            isPhaseTransitioning = true;
            isInvincibleByPhase =
                targetSettings.invincibleDuringTransition;

            StopPatternLoop();
            StopBasicAttackBurst();
            StopMovementVelocity();

            currentPhase =
                Mathf.Clamp(
                    targetPhase,
                    1,
                    3);

            externalMovementLock = true;
            suppressContactDamage = true;

            ResolveCoreStateController();

            if (coreStateController != null)
            {
                coreStateController.SetPhase(
                    currentPhase,
                    true);
            }

            ApplyPhaseVisual(
                targetSettings);

            PhaseChanged?.Invoke(
                currentPhase);

            if (debugPhase)
            {
                Debug.Log(
                    $"[BossController] {targetSettings.phaseName} Start | " +
                    $"Core={ActiveCoreTraits}, " +
                    $"Pause={targetSettings.transitionPauseSeconds:0.##}, " +
                    $"Invincible={isInvincibleByPhase}",
                    this);
            }

            float transitionPause =
                Mathf.Max(
                    0f,
                    targetSettings.transitionPauseSeconds);

            if (transitionPause > 0f)
            {
                yield return
                    new WaitForSeconds(
                        transitionPause);
            }

            isInvincibleByPhase = false;
            isPhaseTransitioning = false;
            externalMovementLock = false;
            suppressContactDamage = false;

            basicAttackTimer = 0f;

            ScheduleNextPattern(0.2f);
            StartPatternLoop();

            phaseTransitionCoroutine = null;

            // 한 번의 큰 피해로 두 임계치를 동시에 넘긴 경우
            // 다음 페이즈 진입도 이어서 확인합니다.
            TryStartNextPhaseByHp();
        }

        private void ApplyPhaseVisual(
            BossPhaseSettings settings)
        {
            if (settings == null)
            {
                return;
            }

            if (settings.phaseSprite != null &&
                spriteRenderer != null)
            {
                spriteRenderer.sprite =
                    settings.phaseSprite;
            }

            if (settings.phaseTransitionEffectPrefab != null)
            {
                Instantiate(
                    settings.phaseTransitionEffectPrefab,
                    BossCenterPosition,
                    Quaternion.identity);
            }
        }

        private BossPhaseSettings GetCurrentPhaseSettings()
        {
            return
                GetPhaseSettings(
                    currentPhase);
        }

        private BossPhaseSettings GetPhaseSettings(
            int phase)
        {
            if (phase >= 3)
            {
                return phase3Settings;
            }

            if (phase == 2)
            {
                return phase2Settings;
            }

            return phase1Settings;
        }

        private void TryDealContactDamage(
            Collider2D other)
        {
            if (isDead ||
                playerCharacter == null)
            {
                return;
            }

            if (suppressContactDamage ||
                isPhaseTransitioning)
            {
                return;
            }

            Character character =
                other.GetComponentInParent
                <
                    Character
                >();

            if (character == null ||
                character != playerCharacter)
            {
                return;
            }

            if (Time.time <
                lastContactDamageTime +
                contactDamageCooldown)
            {
                return;
            }

            lastContactDamageTime =
                Time.time;

            float finalDamage =
                GetModifiedDamage(
                    contactDamage);

            playerCharacter.TakeDamage(
                finalDamage);
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            TryDealContactDamage(
                other);
        }

        private void OnTriggerStay2D(
            Collider2D other)
        {
            TryDealContactDamage(
                other);
        }

        private Vector2 RotateVector(
            Vector2 vector,
            float angleDegrees)
        {
            float rad =
                angleDegrees *
                Mathf.Deg2Rad;

            float cos =
                Mathf.Cos(rad);

            float sin =
                Mathf.Sin(rad);

            return new Vector2(
                vector.x * cos -
                vector.y * sin,
                vector.x * sin +
                vector.y * cos
            ).normalized;
        }
    }
}

using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Vampire
{
    public class Character : IDamageable, ISpatialHashGridClient
    {
        [Header("Dependencies")]
        [SerializeField] protected Transform centerTransform;
        [SerializeField] protected Transform lookIndicator;
        [SerializeField] protected float lookIndicatorRadius;
        [SerializeField] protected TextMeshProUGUI levelText;
        [SerializeField] protected AbilitySelectionDialog abilitySelectionDialog;
        [SerializeField] protected PointBar healthBar;
        [SerializeField] protected PointBar expBar;
        [SerializeField] protected Collider2D collectableCollider;
        [SerializeField] protected Collider2D meleeHitboxCollider;
        [SerializeField] protected ParticleSystem dustParticles;
        [SerializeField] protected Material defaultMaterial, hitMaterial, deathMaterial;
        [SerializeField] protected ParticleSystem deathParticles;
        [SerializeField] protected TextMeshProUGUI thermometerText;

        [Header("Character Data")]
        [SerializeField] protected CharacterBlueprint characterBlueprint;

        [Header("Runtime Stats")]
        [SerializeField] protected bool alive = true;
        [SerializeField] protected int currentLevel = 1;
        [SerializeField] protected float currentExp = 0;
        [SerializeField] protected float nextLevelExp = 5;
        [SerializeField] protected float expToNextLevel = 5;
        [SerializeField] protected float currentHealth;

        [Header("Upgradeable Systems")]
        [SerializeField] protected UpgradeableMovementSpeed movementSpeed;
        [SerializeField] protected UpgradeableArmor armor;
        [SerializeField] protected float rangeMultiplier = 1.0f;

        [Header("Combat Stat Pockets")]
        [SerializeField] protected float attackSpeedMultiplier = 1.0f;
        [SerializeField] protected float damageMultiplier = 1.0f;
        [SerializeField] protected float maxHealthBonus = 0f;
        [SerializeField] protected float projectileSpeedMultiplier = 1.0f;

        [Header("Utility Stat Pockets")]
        [SerializeField] protected float magnetRangeBonus = 0f;
        [SerializeField] protected float expMultiplier = 1.0f;
        [SerializeField] protected float critChance = 0.03f;
        [SerializeField] protected float luckMultiplier = 1.0f;
        [SerializeField] protected float lifeSteal = 0f;
        [SerializeField] protected float healOnKill = 0f;
        [SerializeField] protected float projectileSizeMultiplier = 1.0f;

        [Header("Special Ability Flags")]
        [SerializeField] protected int additionalProjectiles = 0;
        [SerializeField] protected bool hasShield = false;
        [SerializeField] protected int reviveCount = 0;
        [SerializeField] protected float invincibilityTimeBonus = 0f;
        [SerializeField] private bool hasAntibioticBomb = false;

        [Header("Dash Settings")]
        [Tooltip("체크하면 Shift 키로 대쉬를 사용할 수 있습니다.")]
        [SerializeField] protected bool enableDash = true;

        [Tooltip("대쉬로 이동하는 거리입니다. 값이 클수록 더 멀리 대쉬합니다.")]
        [SerializeField] protected float dashDistance = 3.5f;

        [Tooltip("대쉬가 완료되기까지 걸리는 시간입니다. 낮으면 순간이동처럼 보이고, 높이면 더 부드럽게 미끄러지는 느낌이 납니다.")]
        [SerializeField] protected float dashDuration = 0.22f;

        [Tooltip("대쉬 1회가 다시 충전되기까지 걸리는 시간입니다.")]
        [SerializeField] protected float dashRechargeTime = 1.2f;

        [Tooltip("최대 대쉬 보유 횟수입니다.")]
        [SerializeField] protected int maxDashCharges = 1;

        [Tooltip("체크하면 대쉬 중 피해를 받지 않습니다.")]
        [SerializeField] protected bool invincibleDuringDash = true;

        [Tooltip("대쉬가 끝난 직후에도 아주 짧게 유지되는 추가 무적 시간입니다. 적과 겹친 상태로 대쉬가 끝났을 때 바로 맞는 것을 막습니다.")]
        [SerializeField] protected float dashEndInvincibleGraceTime = 0.12f;

        [Tooltip("체크하면 대쉬 종료 시 Rigidbody 속도를 0으로 초기화합니다.")]
        [SerializeField] protected bool stopVelocityAfterDash = true;

        [Header("Dash Feel")]
        [Tooltip("대쉬 이동 곡선입니다. X축은 시간, Y축은 이동 진행도입니다. 직선에 가까우면 일정 속도, 완만한 곡선이면 더 부드러운 대쉬가 됩니다.")]
        [SerializeField] protected AnimationCurve dashMovementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("체크하면 Dash Movement Curve를 사용합니다. 꺼두면 일정한 속도로 대쉬합니다.")]
        [SerializeField] protected bool useDashMovementCurve = true;

        [Header("Dash Sprite")]
        [Tooltip("체크하면 대쉬 중 걷기 애니메이션을 멈추고 대쉬 전용 스프라이트로 교체합니다.")]
        [SerializeField] protected bool useDashSprite = true;

        [Tooltip("대쉬 중에 표시할 전용 스프라이트입니다. 앞으로 숙인 질주자세 같은 이미지를 넣으면 됩니다.")]
        [SerializeField] protected Sprite dashSprite;

        [Tooltip("체크하면 대쉬 스프라이트가 오른쪽을 바라보는 기준 이미지라고 판단합니다. 보통 오른쪽을 보고 있는 이미지를 넣고 체크해두면 됩니다.")]
        [SerializeField] protected bool dashSpriteFacesRight = true;

        [Tooltip("체크하면 대쉬가 끝난 뒤 이동 중일 때 걷기 애니메이션을 다시 시작합니다.")]
        [SerializeField] protected bool restartWalkAnimationAfterDash = true;

        [Header("Dash Collision")]
        [Tooltip("체크하면 대쉬 중 플레이어의 일반 충돌 콜라이더를 Trigger로 바꿔 몬스터를 밀지 않고 관통합니다.")]
        [SerializeField] protected bool passThroughCollidersDuringDash = true;

        [Tooltip("기존 Trigger 콜라이더까지 같이 처리할지 여부입니다. 보통은 꺼두는 것을 추천합니다.")]
        [SerializeField] protected bool includeTriggerCollidersInDashGhost = false;

        [Header("Dash Debug")]
        [Tooltip("체크하면 대쉬 관련 로그를 Console에 출력합니다.")]
        [SerializeField] protected bool debugDashLog = false;

        [Header("Recovery Stats")]
        [SerializeField] protected float healOnIdlePerSecond = 0f;

        [Header("Legendary Utility Stats")]
        [SerializeField] protected bool autoCollectItems = false;

        [Header("Debuff Stats")]
        [SerializeField] private float slowChance = 0f;

        [Header("Utility Combat Stats")]
        [SerializeField] private int additionalPierce = 0;
        [SerializeField] private float burnChance = 0f;

        [Header("Rare Item Flags")]
        [SerializeField] private bool hasGinsengStick = false;
        [SerializeField] private float antibioticBombChance = 0f;
        [SerializeField] private bool hasThermometer = false;
        [SerializeField] private int mouthwashCount = 0;
        [SerializeField] private int reflexHammerCount = 0;

        protected SpriteRenderer spriteRenderer;
        protected SpriteAnimator spriteAnimator;
        protected AbilityManager abilityManager;
        protected EntityManager entityManager;
        protected StatsManager statsManager;
        protected Rigidbody2D rb;
        protected ZPositioner zPositioner;
        protected Vector2 lookDirection = Vector2.right;
        protected CoroutineQueue coroutineQueue;
        protected Coroutine hitAnimationCoroutine = null;
        protected Vector2 moveDirection;

        protected bool isDashing = false;
        protected bool isInvincible = false;
        protected int currentDashCharges = 1;
        protected Coroutine dashCoroutine = null;
        protected Coroutine dashRechargeCoroutine = null;

        private float dashInvincibleEndTime = -1f;

        private Sprite cachedSpriteBeforeDash;
        private bool dashSpriteApplied = false;

        private readonly List<Collider2D> dashGhostedColliders = new List<Collider2D>();
        private readonly Dictionary<Collider2D, bool> originalTriggerStateByCollider = new Dictionary<Collider2D, bool>();

        public Vector2 LookDirection
        {
            get { return lookDirection; }
            set
            {
                if (value != Vector2.zero)
                {
                    lookDirection = value;
                }
            }
        }

        public Transform CenterTransform => centerTransform;
        public Collider2D CollectableCollider => collectableCollider;
        public float Luck => characterBlueprint.luck * luckMultiplier;
        public int CurrentLevel => currentLevel;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => GetMaxHealth();
        public float CurrentMoveSpeed => movementSpeed != null ? movementSpeed.Value : 0f;
        public float CurrentArmor => armor != null ? armor.Value : 0f;

        public string DisplayName =>
            characterBlueprint != null && !string.IsNullOrWhiteSpace(characterBlueprint.name)
                ? characterBlueprint.name
                : gameObject.name;

        public bool HasThermometer => hasThermometer;

        private int thermometerStacks = 0;
        private float thermometerStackTimer = 0f;
        private float thermometerMoveAccumulator = 0f;

        public float AttackSpeedMultiplier
        {
            get
            {
                float finalSpeed = attackSpeedMultiplier;

                if (hasThermometer)
                {
                    finalSpeed += thermometerStacks * 0.03f;
                }

                return finalSpeed;
            }
        }

        public int MouthwashCount => mouthwashCount;
        public float ProjectileSpeedMultiplier => projectileSpeedMultiplier;
        public float BurnChance => burnChance;
        public float CritChance => critChance;
        public int AdditionalProjectiles => additionalProjectiles;
        public float InvincibilityTimeBonus => invincibilityTimeBonus;
        public float LifeSteal => lifeSteal;
        public float HealOnKill => healOnKill;
        public float ProjectileSizeMultiplier => projectileSizeMultiplier;
        public float RangeMultiplier => rangeMultiplier;
        public int CurrentDashCharges => currentDashCharges;
        public int MaxDashCharges => maxDashCharges;
        public bool IsDashing => isDashing;
        public float AntibioticBombChance => antibioticBombChance;
        public bool AutoCollectItems => autoCollectItems;

        public UnityEvent<float> OnDealDamage { get; } = new UnityEvent<float>();
        public UnityEvent OnDeath { get; } = new UnityEvent();

        public float SlowChance => slowChance;
        public int AdditionalPierce => additionalPierce;
        public CharacterBlueprint Blueprint => characterBlueprint;
        public Vector2 Velocity => rb != null ? rb.velocity : Vector2.zero;
        public bool HasAntibioticBomb => hasAntibioticBomb;

        public Vector2 Position => transform.position;
        public Vector2 Size => meleeHitboxCollider.bounds.size;
        public Dictionary<int, int> ListIndexByCellIndex { get; set; }
        public int QueryID { get; set; } = -1;

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            zPositioner = gameObject.AddComponent<ZPositioner>();
            spriteAnimator = GetComponentInChildren<SpriteAnimator>();

            if (spriteAnimator != null)
            {
                spriteRenderer = spriteAnimator.GetComponent<SpriteRenderer>();
            }

            characterBlueprint = CrossSceneData.CharacterBlueprint;
        }

        private void OnDisable()
        {
            RestoreDashCollisionGhost();
            RestoreDashSpriteVisual();

            isDashing = false;
            dashCoroutine = null;
            dashInvincibleEndTime = -1f;
        }

        public virtual void Init(EntityManager entityManager, AbilityManager abilityManager, StatsManager statsManager)
        {
            this.entityManager = entityManager;
            this.abilityManager = abilityManager;
            this.statsManager = statsManager;

            OnDealDamage.AddListener(statsManager.IncreaseDamageDealt);

            OnDealDamage.AddListener((damage) =>
            {
                if (lifeSteal > 0)
                {
                    GainHealth(damage * lifeSteal);
                }
            });

            coroutineQueue = new CoroutineQueue(this);
            coroutineQueue.StartLoop();

            currentHealth = characterBlueprint.hp;
            healthBar.Setup(currentHealth, 0, GetMaxHealth());
            expBar.Setup(currentExp, 0, nextLevelExp);

            currentLevel = 1;
            UpdateLevelDisplay();

            spriteAnimator.Init(characterBlueprint.walkSpriteSequence, characterBlueprint.walkFrameTime, false);

            movementSpeed = new UpgradeableMovementSpeed();
            movementSpeed.Value = characterBlueprint.movespeed;
            abilityManager.RegisterUpgradeableValue(movementSpeed, true);
            UpdateMoveSpeed();

            armor = new UpgradeableArmor();
            armor.Value = characterBlueprint.armor;
            abilityManager.RegisterUpgradeableValue(armor, true);

            zPositioner.Init(transform);

            InitDash();
            UpdateThermometerDisplay();
        }

        protected virtual void Update()
        {
            UpdateDashInput();
            HandleHealOnIdle();
            HandleThermometerMovementAndDecay();

            if (lookIndicator != null)
            {
                lookIndicator.transform.localPosition = lookDirection * lookIndicatorRadius;
            }

            if (spriteRenderer != null && !dashSpriteApplied)
            {
                spriteRenderer.flipX = lookDirection.x < 0;
            }
        }

        private void HandleHealOnIdle()
        {
            if (healOnIdlePerSecond > 0 && Velocity.magnitude < 0.1f)
            {
                float healAmount = GetMaxHealth() * healOnIdlePerSecond;
                GainHealth(healAmount * Time.deltaTime);
            }
        }

        protected virtual void FixedUpdate()
        {
            if (isDashing)
            {
                return;
            }

            if (moveDirection != Vector2.zero)
            {
                lookDirection = moveDirection;
            }
            else
            {
                StopWalkAnimation();
            }

            if (alive)
            {
                rb.velocity += moveDirection * characterBlueprint.acceleration * Time.deltaTime;
            }
        }

        private void InitDash()
        {
            maxDashCharges = Mathf.Max(1, maxDashCharges);
            currentDashCharges = maxDashCharges;
            dashInvincibleEndTime = -1f;
        }

        private void UpdateDashInput()
        {
            if (!CanReadDashInput())
            {
                return;
            }

            bool dashPressed =
                Keyboard.current.leftShiftKey.wasPressedThisFrame ||
                Keyboard.current.rightShiftKey.wasPressedThisFrame;

            if (dashPressed)
            {
                TryDash();
            }
        }

        private bool CanReadDashInput()
        {
            if (!enableDash)
            {
                return false;
            }

            if (!alive)
            {
                return false;
            }

            if (Keyboard.current == null)
            {
                return false;
            }

            if (abilitySelectionDialog != null && abilitySelectionDialog.MenuOpen)
            {
                return false;
            }

            return true;
        }

        public bool TryDash()
        {
            if (!enableDash)
            {
                return false;
            }

            if (!alive)
            {
                return false;
            }

            if (isDashing)
            {
                return false;
            }

            if (currentDashCharges <= 0)
            {
                if (debugDashLog)
                {
                    Debug.Log("[Dash] 사용 가능한 대쉬 횟수가 없습니다.");
                }

                return false;
            }

            Vector2 dashDirection = GetDashDirection();

            if (dashDirection == Vector2.zero)
            {
                return false;
            }

            currentDashCharges--;

            if (dashCoroutine != null)
            {
                StopCoroutine(dashCoroutine);
            }

            dashCoroutine = StartCoroutine(DashCoroutine(dashDirection));

            if (dashRechargeCoroutine == null)
            {
                dashRechargeCoroutine = StartCoroutine(DashRechargeCoroutine());
            }

            if (debugDashLog)
            {
                Debug.Log($"[Dash] 대쉬 사용 | 남은 대쉬 = {currentDashCharges}/{maxDashCharges}");
            }

            return true;
        }

        private Vector2 GetDashDirection()
        {
            if (moveDirection != Vector2.zero)
            {
                return moveDirection.normalized;
            }

            if (lookDirection != Vector2.zero)
            {
                return lookDirection.normalized;
            }

            return Vector2.right;
        }

        private IEnumerator DashCoroutine(Vector2 dashDirection)
        {
            isDashing = true;

            if (invincibleDuringDash)
            {
                dashInvincibleEndTime = Time.time + dashDuration + dashEndInvincibleGraceTime;
            }
            else
            {
                dashInvincibleEndTime = -1f;
            }

            ApplyDashCollisionGhost();

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            if (dashDirection != Vector2.zero)
            {
                lookDirection = dashDirection.normalized;
            }

            ApplyDashSpriteVisual(dashDirection);

            Vector2 startPosition = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 targetPosition = startPosition + dashDirection.normalized * dashDistance;

            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, dashDuration);

            while (elapsed < safeDuration)
            {
                float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                float moveT = normalizedTime;

                if (useDashMovementCurve && dashMovementCurve != null)
                {
                    moveT = Mathf.Clamp01(dashMovementCurve.Evaluate(normalizedTime));
                }

                Vector2 nextPosition = Vector2.LerpUnclamped(startPosition, targetPosition, moveT);

                if (rb != null)
                {
                    rb.MovePosition(nextPosition);
                }
                else
                {
                    transform.position = nextPosition;
                }

                elapsed += Time.fixedDeltaTime;
                yield return new WaitForFixedUpdate();
            }

            if (rb != null)
            {
                rb.MovePosition(targetPosition);

                if (stopVelocityAfterDash)
                {
                    rb.velocity = Vector2.zero;
                }
            }
            else
            {
                transform.position = targetPosition;
            }

            yield return new WaitForFixedUpdate();

            RestoreDashCollisionGhost();
            RestoreDashSpriteVisual();

            isDashing = false;
            dashCoroutine = null;

            if (invincibleDuringDash && dashEndInvincibleGraceTime > 0f)
            {
                dashInvincibleEndTime = Time.time + dashEndInvincibleGraceTime;

                if (debugDashLog)
                {
                    Debug.Log($"[Dash] 대쉬 종료 후 추가 무적 시작 | {dashEndInvincibleGraceTime:0.00}초");
                }
            }
            else
            {
                dashInvincibleEndTime = -1f;
            }

            if (debugDashLog)
            {
                Debug.Log("[Dash] 대쉬 종료");
            }
        }

        private bool IsDashInvincibilityActive()
        {
            if (!invincibleDuringDash)
            {
                return false;
            }

            if (isDashing)
            {
                return true;
            }

            return Time.time < dashInvincibleEndTime;
        }

        private void ApplyDashSpriteVisual(Vector2 dashDirection)
        {
            if (!useDashSprite)
            {
                return;
            }

            if (dashSprite == null)
            {
                return;
            }

            if (spriteRenderer == null)
            {
                return;
            }

            cachedSpriteBeforeDash = spriteRenderer.sprite;
            dashSpriteApplied = true;

            StopWalkAnimation();

            spriteRenderer.sprite = dashSprite;

            if (dashDirection.x != 0f)
            {
                if (dashSpriteFacesRight)
                {
                    spriteRenderer.flipX = dashDirection.x < 0f;
                }
                else
                {
                    spriteRenderer.flipX = dashDirection.x > 0f;
                }
            }
        }

        private void RestoreDashSpriteVisual()
        {
            if (!dashSpriteApplied)
            {
                return;
            }

            dashSpriteApplied = false;

            if (restartWalkAnimationAfterDash && moveDirection != Vector2.zero)
            {
                StartWalkAnimation();
            }
            else
            {
                StopWalkAnimation();

                if (spriteRenderer != null && cachedSpriteBeforeDash != null)
                {
                    spriteRenderer.sprite = cachedSpriteBeforeDash;
                }
            }

            cachedSpriteBeforeDash = null;
        }

        private void ApplyDashCollisionGhost()
        {
            if (!passThroughCollidersDuringDash)
            {
                return;
            }

            RestoreDashCollisionGhost();

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider2D targetCollider = colliders[i];

                if (targetCollider == null)
                {
                    continue;
                }

                if (!targetCollider.enabled)
                {
                    continue;
                }

                if (targetCollider.isTrigger && !includeTriggerCollidersInDashGhost)
                {
                    continue;
                }

                originalTriggerStateByCollider[targetCollider] = targetCollider.isTrigger;
                dashGhostedColliders.Add(targetCollider);
                targetCollider.isTrigger = true;
            }

            if (debugDashLog)
            {
                Debug.Log($"[Dash] 충돌 관통 활성화 | 대상 콜라이더 수 = {dashGhostedColliders.Count}");
            }
        }

        private void RestoreDashCollisionGhost()
        {
            if (dashGhostedColliders.Count <= 0)
            {
                originalTriggerStateByCollider.Clear();
                return;
            }

            for (int i = 0; i < dashGhostedColliders.Count; i++)
            {
                Collider2D targetCollider = dashGhostedColliders[i];

                if (targetCollider == null)
                {
                    continue;
                }

                if (originalTriggerStateByCollider.TryGetValue(targetCollider, out bool originalIsTrigger))
                {
                    targetCollider.isTrigger = originalIsTrigger;
                }
            }

            dashGhostedColliders.Clear();
            originalTriggerStateByCollider.Clear();

            if (debugDashLog)
            {
                Debug.Log("[Dash] 충돌 관통 해제");
            }
        }

        private IEnumerator DashRechargeCoroutine()
        {
            while (currentDashCharges < maxDashCharges)
            {
                yield return new WaitForSeconds(dashRechargeTime);

                if (!alive)
                {
                    dashRechargeCoroutine = null;
                    yield break;
                }

                currentDashCharges = Mathf.Min(currentDashCharges + 1, maxDashCharges);

                if (debugDashLog)
                {
                    Debug.Log($"[Dash] 대쉬 충전 | 현재 대쉬 = {currentDashCharges}/{maxDashCharges}");
                }
            }

            dashRechargeCoroutine = null;
        }

        public void GainExp(float exp)
        {
            if (alive)
            {
                coroutineQueue.EnqueueCoroutine(GainExpCoroutine(exp * expMultiplier));
            }
        }

        private IEnumerator GainExpCoroutine(float exp)
        {
            if (!alive)
            {
                yield break;
            }

            while (currentExp + exp >= nextLevelExp)
            {
                float expDiff = nextLevelExp - currentExp;
                currentExp += expDiff;
                exp -= expDiff;

                expBar.Setup(currentExp, 0, nextLevelExp);

                yield return LevelUpCoroutine();

                float prevLevelExp = nextLevelExp;
                expToNextLevel += characterBlueprint.LevelToExpIncrease(currentLevel);
                nextLevelExp += expToNextLevel;

                expBar.Setup(currentExp, prevLevelExp, nextLevelExp);
            }

            currentExp += exp;
            expBar.AddPoints(exp);
        }

        private IEnumerator LevelUpCoroutine()
        {
            if (!alive)
            {
                yield break;
            }

            currentLevel++;
            UpdateLevelDisplay();

            abilitySelectionDialog.Open();

            while (abilitySelectionDialog.MenuOpen)
            {
                yield return null;
            }
        }

        private void UpdateLevelDisplay()
        {
            if (levelText != null)
            {
                levelText.text = "LV " + currentLevel;
            }
        }

        public override void Knockback(Vector2 knockback)
        {
            if (IsDashInvincibilityActive())
            {
                return;
            }

            rb.velocity += knockback * Mathf.Sqrt(rb.drag);
        }

        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2), bool isCritical = false)
        {
            if (!alive)
            {
                return;
            }

            if (IsDashInvincibilityActive())
            {
                if (debugDashLog)
                {
                    Debug.Log("[Dash] 대쉬 무적으로 피해 무시");
                }

                return;
            }

            if (isInvincible)
            {
                return;
            }

            if (hasShield)
            {
                hasShield = false;
                return;
            }

            if (armor.Value >= damage)
            {
                damage = damage < 1 ? damage : 1;
            }
            else
            {
                damage -= armor.Value;
            }

            healthBar.SubtractPoints(damage);
            currentHealth -= damage;
            rb.velocity += knockback * Mathf.Sqrt(rb.drag);
            statsManager.IncreaseDamageTaken(damage);

            if (reflexHammerCount > 0 && damage > 0f)
            {
                float finalHammerChance = reflexHammerCount * 0.005f;

                if (UnityEngine.Random.value < finalHammerChance)
                {
                    TriggerReflexHammer();
                }
            }

            if (currentHealth <= 0)
            {
                if (reviveCount > 0)
                {
                    Revive();
                    return;
                }

                StartCoroutine(DeathAnimation());
            }
            else
            {
                if (hitAnimationCoroutine != null)
                {
                    StopCoroutine(hitAnimationCoroutine);
                }

                hitAnimationCoroutine = StartCoroutine(HitAnimation());
            }
        }

        private IEnumerator HitAnimation()
        {
            isInvincible = true;

            if (spriteRenderer != null)
            {
                spriteRenderer.sharedMaterial = hitMaterial;
            }

            yield return new WaitForSeconds(0.15f + invincibilityTimeBonus);

            if (spriteRenderer != null)
            {
                spriteRenderer.sharedMaterial = defaultMaterial;
            }

            isInvincible = false;
        }

        private IEnumerator DeathAnimation()
        {
            alive = false;

            RestoreDashCollisionGhost();
            RestoreDashSpriteVisual();

            isDashing = false;
            dashInvincibleEndTime = -1f;

            if (spriteRenderer != null)
            {
                spriteRenderer.sharedMaterial = deathMaterial;
            }

            abilityManager.DestroyActiveAbilities();
            StopWalkAnimation();

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            float height = spriteRenderer != null ? spriteRenderer.bounds.size.y : 1f;
            float t = 0;

            while (t < 1)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.sharedMaterial = deathMaterial;
                }

                if (deathParticles != null)
                {
                    deathParticles.transform.position = transform.position + Vector3.up * height * (1 - t);
                }

                if (deathMaterial != null)
                {
                    deathMaterial.SetFloat("_Wipe", t);
                }

                t += Time.deltaTime;
                yield return null;
            }

            if (deathMaterial != null)
            {
                deathMaterial.SetFloat("_Wipe", 1.0f);
            }

            yield return new WaitForSeconds(0.5f);

            OnDeath.Invoke();

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }
        }

        private void Revive()
        {
            reviveCount--;
            currentHealth = GetMaxHealth() * 0.5f;
            healthBar.Setup(currentHealth, 0, GetMaxHealth());

            if (hitAnimationCoroutine != null)
            {
                StopCoroutine(hitAnimationCoroutine);
            }

            hitAnimationCoroutine = StartCoroutine(HitAnimation());
        }

        public void GainHealth(float health)
        {
            float maxHealth = GetMaxHealth();

            healthBar.AddPoints(health);
            currentHealth += health;

            if (currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }
        }

        public void SetLookDirecton(InputAction.CallbackContext context)
        {
            LookDirection = context.ReadValue<Vector2>();
        }

        public void UpdateMoveSpeed()
        {
            rb.drag = characterBlueprint.acceleration / (movementSpeed.Value * movementSpeed.Value);
        }

        public void AddHealOnIdle(float amount)
        {
            healOnIdlePerSecond += amount;
        }

        public void AddBurnChance(float amount)
        {
            burnChance += amount;
        }

        public void Move(Vector2 moveDirection)
        {
            this.moveDirection = moveDirection;
        }

        public void StartWalkAnimation()
        {
            if (alive && spriteAnimator != null)
            {
                spriteAnimator.StartAnimating();
            }
        }

        public void StopWalkAnimation()
        {
            if (spriteAnimator != null)
            {
                spriteAnimator.StopAnimating(true);
            }
        }

        public void SetMoveDirection(InputAction.CallbackContext context)
        {
            moveDirection = context.action.ReadValue<Vector2>().normalized;
        }

        private float GetMaxHealth()
        {
            return characterBlueprint.hp + maxHealthBonus;
        }

        public void AddAttackSpeed(float amount)
        {
            attackSpeedMultiplier += amount;
        }

        public float DamageMultiplier
        {
            get
            {
                float finalMultiplier = damageMultiplier;

                if (hasGinsengStick && (currentHealth / GetMaxHealth()) <= 0.3f)
                {
                    finalMultiplier += 0.5f;
                }

                return finalMultiplier;
            }
        }

        public void AddProjectileSpeed(float amount)
        {
            projectileSpeedMultiplier += amount;
        }

        public void AddMaxHealthBonus(float amount)
        {
            maxHealthBonus += amount;

            float maxHealth = GetMaxHealth();

            if (currentHealth > maxHealth)
            {
                currentHealth = maxHealth;
            }

            healthBar.Setup(currentHealth, 0, maxHealth);
        }

        public void EnableThermometer()
        {
            hasThermometer = true;
        }

        public void AddMoveSpeedBoost(float amount)
        {
            if (movementSpeed == null)
            {
                return;
            }

            movementSpeed.Value += amount;
            UpdateMoveSpeed();
        }

        public void AddRangeBoost(float amount)
        {
            rangeMultiplier += amount;
        }

        public void AddMagnetRange(float amount)
        {
            magnetRangeBonus += amount;
        }

        public void AddExpMultiplier(float amount)
        {
            expMultiplier += amount;
        }

        public void AddAdditionalPierce(int amount)
        {
            additionalPierce += amount;
        }

        public void AddRangeMultiplier(float amount)
        {
            rangeMultiplier += amount;
        }

        public void AddCritChance(float amount)
        {
            critChance += amount;
        }

        public void AddThermometerStack()
        {
            if (!hasThermometer)
            {
                return;
            }

            if (thermometerStacks < 10)
            {
                thermometerStacks++;
                Debug.Log($"[체온계 스택] 적중 완료! 현재 스택: {thermometerStacks} (공속 +{thermometerStacks * 5}%)");
            }

            thermometerStackTimer = 3.0f;
        }

        private void HandleThermometerMovementAndDecay()
        {
            if (!hasThermometer)
            {
                return;
            }

            if (moveDirection != Vector2.zero)
            {
                thermometerStackTimer = 0f;
                thermometerMoveAccumulator += Time.deltaTime;

                if (thermometerMoveAccumulator >= 0.25f)
                {
                    thermometerMoveAccumulator = 0f;

                    if (thermometerStacks < 10)
                    {
                        thermometerStacks++;
                        UpdateThermometerDisplay();
                        Debug.Log($"[체온계 예열] 이동 중! 스택: {thermometerStacks} (공속 +{thermometerStacks * 3}%)");
                    }
                }
            }
            else
            {
                thermometerMoveAccumulator = 0f;

                if (thermometerStacks > 0)
                {
                    thermometerStackTimer -= Time.deltaTime;

                    if (thermometerStackTimer <= 0f)
                    {
                        thermometerStacks--;
                        UpdateThermometerDisplay();
                        thermometerStackTimer = 0.15f;

                        if (thermometerStacks == 0)
                        {
                            Debug.Log("[체온계 냉각] 완전히 냉각되었습니다. 공속 초기화.");
                        }
                        else
                        {
                            Debug.Log($"[체온계 냉각] 정지 상태! 체온 저하 중... 남은 스택: {thermometerStacks} (공속 +{thermometerStacks * 3}%)");
                        }
                    }
                }
            }
        }

        private void UpdateThermometerDisplay()
        {
            if (thermometerText == null)
            {
                return;
            }

            if (!hasThermometer || thermometerStacks == 0)
            {
                thermometerText.text = "";
                return;
            }

            float displayTemp = 36.5f + thermometerStacks;
            int bonusAttackSpeed = thermometerStacks * 3;
            string maxLabel = thermometerStacks >= 10 ? " [MAX]" : "";

            thermometerText.text = $"️ 온도 스택: {thermometerStacks} ({displayTemp:0.0}°C | 공속 +{bonusAttackSpeed}%){maxLabel}";
        }

        private void TriggerReflexHammer()
        {
            float pushRadius = 3.5f;
            float strongForce = 15.0f;

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, pushRadius);

            foreach (Collider2D hit in hits)
            {
                Monster monster = hit.GetComponentInParent<Monster>();

                if (monster == null)
                {
                    continue;
                }

                IDamageable damageable = monster as IDamageable;

                if (damageable != null)
                {
                    Vector2 pushDirection = (monster.transform.position - transform.position).normalized;

                    if (pushDirection == Vector2.zero)
                    {
                        pushDirection = Random.insideUnitCircle.normalized;
                    }

                    damageable.TakeDamage(0f, pushDirection * strongForce, false);
                }
            }

            Debug.Log("[반사신경 망치] 쾅! 반사 신경 작동, 주변 적들을 격퇴했습니다.");
        }

        public void AddLuck(float amount)
        {
            luckMultiplier += amount;
        }

        public void EnableGinsengStick()
        {
            hasGinsengStick = true;
        }

        public void AddMouthwash()
        {
            mouthwashCount++;
        }

        public void AddReflexHammer()
        {
            reflexHammerCount++;
        }

        public void EnableAntibioticBomb()
        {
            antibioticBombChance += 0.05f;
        }

        public void AddLifeSteal(float amount)
        {
            lifeSteal += amount;
        }

        public void AddHealOnKill(float amount)
        {
            healOnKill += amount;
        }

        public void AddProjectileSize(float amount)
        {
            projectileSizeMultiplier += amount;
        }

        public void AddDamageMultiplier(float amount)
        {
            damageMultiplier += amount;
        }

        public void AddProjectileCount(int count)
        {
            additionalProjectiles += count;
        }

        public void EnableAutoCollect()
        {
            autoCollectItems = true;
        }

        public void AddSlowChance(float amount)
        {
            slowChance += amount;
        }

        public void EnableShield()
        {
            hasShield = true;
        }

        public void AddReviveCount(int count)
        {
            reviveCount += count;
        }

        public void AddInvincibilityTime(float amount)
        {
            invincibilityTimeBonus += amount;
        }

        public void AddDashCharge(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            maxDashCharges += amount;
            currentDashCharges = Mathf.Min(currentDashCharges + amount, maxDashCharges);
        }

        public void AddDashDistance(float amount)
        {
            dashDistance += amount;
            dashDistance = Mathf.Max(0.1f, dashDistance);
        }

        public void AddDashRechargeSpeed(float amount)
        {
            dashRechargeTime -= amount;
            dashRechargeTime = Mathf.Max(0.1f, dashRechargeTime);
        }
    }
}
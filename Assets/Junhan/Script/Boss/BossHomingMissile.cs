using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 호밍 미사일의 현재 상태입니다.
    /// </summary>
    public enum BossHomingMissileState
    {
        Tracking = 0,
        Falling = 1,
        Embedded = 2,
        Destroyed = 3
    }

    /// <summary>
    /// 크리피커피 호밍 미사일입니다.
    ///
    /// 신규 동작:
    /// 1. Tracking: 일정 시간 플레이어를 추적합니다.
    /// 2. Falling: 추진력을 잃고 잠시 낙하 연출을 재생합니다.
    /// 3. Embedded: 바닥에 박힌 뒤 정지하며 플레이어 투사체로 기폭할 수 있습니다.
    ///
    /// 매립 미사일 폭발:
    /// - 플레이어에게 피해를 주지 않습니다.
    /// - 일반 Monster에게 고정 피해를 줍니다.
    /// - 소유 보스의 가장 가까운 파츠 하나에 BossSelf 피해를 줍니다.
    ///
    /// 기존 Init 시그니처는 그대로 유지하여 기존 호출부 호환성을 보존합니다.
    /// BossHomingMissilePattern에서 ConfigureEmbeddedBehavior를 추가 호출하면
    /// 신규 매립 모드가 활성화됩니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossHomingMissile : MonoBehaviour
    {
        [Header("Runtime State")]
        [Tooltip("현재 미사일 상태입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private BossHomingMissileState currentState =
            BossHomingMissileState.Tracking;

        [Tooltip("신규 추적 → 낙하 → 매립 동작이 활성화되어 있는지 표시합니다.")]
        [SerializeField]
        private bool embeddedBehaviorEnabled;

        [Header("Debug")]
        [Tooltip("상태 전환, 기폭, 보스/몬스터 피해 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = false;

        private Character targetCharacter;
        private BossController ownerBossController;

        private float speed;
        private float damage;
        private float lifeTime;

        private float turnSpeed;
        private float laneOffsetDistance;
        private float laneFadeDistance;
        private float initialSpreadDuration;

        // 기존 필드명/Init 인자를 유지합니다.
        // 신규 매립 모드에서는 Tracking 중에는 파괴되지 않고,
        // Embedded 상태에서만 플레이어 투사체 기폭 허용 여부로 사용합니다.
        private bool destroyByPlayerProjectile;
        private LayerMask playerProjectileLayerMask;
        private float projectileHitCheckRadius;

        private bool rotateToMoveDirection;
        private float visualForwardAngleOffset;
        private GameObject destroyEffectPrefab;

        private Vector2 moveDirection;
        private int laneIndex;
        private int laneCount;

        private float spawnTime;
        private float stateStartTime;
        private bool initialized;
        private bool isDestroying;

        private Rigidbody2D rb;
        private Collider2D[] missileColliders;

        // --------------------------------------------------
        // Embedded Behavior
        // --------------------------------------------------
        private float trackingDuration = 8f;
        private float fallingDuration = 0.6f;
        private float embeddedLifetime = 18f;
        private int maxEmbeddedMissiles = 4;

        private float embeddedScaleMultiplier = 0.8f;
        private float fallingSpinDegrees = 180f;

        private float embeddedExplosionRadius = 2.25f;
        private float embeddedBossMaxHpDamagePercent = 0.025f;
        private float embeddedMonsterDamage = 50f;

        private GameObject embeddedLandingEffectPrefab;
        private GameObject embeddedExplosionEffectPrefab;

        private Vector3 fallingStartScale;
        private Quaternion fallingStartRotation;

        private static readonly List<BossHomingMissile>
            activeEmbeddedMissiles =
                new List<BossHomingMissile>();

        public BossHomingMissileState CurrentState =>
            currentState;

        public bool IsTracking =>
            currentState == BossHomingMissileState.Tracking;

        public bool IsEmbedded =>
            currentState == BossHomingMissileState.Embedded;

        public BossController OwnerBossController =>
            ownerBossController;

        /// <summary>
        /// 기존 호출부 호환용 초기화 메서드입니다.
        /// 시그니처를 변경하지 않습니다.
        /// </summary>
        public void Init(
            Character target,
            Vector2 initialDirection,
            float missileSpeed,
            float missileDamage,
            float missileLifeTime,
            int missileLaneIndex,
            int totalLaneCount,
            float missileTurnSpeed,
            float missileLaneOffsetDistance,
            float missileLaneFadeDistance,
            float missileInitialSpreadDuration,
            bool canBeDestroyedByPlayerProjectile,
            LayerMask projectileLayerMask,
            float projectileCheckRadius,
            bool shouldRotateToMoveDirection,
            float missileVisualForwardAngleOffset,
            GameObject missileDestroyEffectPrefab
        )
        {
            targetCharacter = target;

            if (initialDirection == Vector2.zero)
            {
                initialDirection = Vector2.right;
            }

            moveDirection =
                initialDirection.normalized;

            speed = missileSpeed;
            damage = missileDamage;
            lifeTime = missileLifeTime;

            laneIndex = missileLaneIndex;
            laneCount =
                Mathf.Max(1, totalLaneCount);

            turnSpeed = missileTurnSpeed;
            laneOffsetDistance = missileLaneOffsetDistance;
            laneFadeDistance = missileLaneFadeDistance;
            initialSpreadDuration = missileInitialSpreadDuration;

            destroyByPlayerProjectile =
                canBeDestroyedByPlayerProjectile;

            playerProjectileLayerMask =
                projectileLayerMask;

            projectileHitCheckRadius =
                Mathf.Max(0f, projectileCheckRadius);

            rotateToMoveDirection =
                shouldRotateToMoveDirection;

            visualForwardAngleOffset =
                missileVisualForwardAngleOffset;

            destroyEffectPrefab =
                missileDestroyEffectPrefab;

            spawnTime = Time.time;
            stateStartTime = Time.time;

            initialized = true;
            isDestroying = false;

            currentState =
                BossHomingMissileState.Tracking;

            embeddedBehaviorEnabled = false;

            SetMissileCollidersEnabled(true);
            StopRigidbodyMotion();
            ApplyVisualRotation(moveDirection);
        }

        /// <summary>
        /// 신규 매립형 호밍 미사일 동작을 설정합니다.
        ///
        /// BossHomingMissilePattern에서 Init 직후 호출합니다.
        /// </summary>
        public void ConfigureEmbeddedBehavior(
            BossController ownerBoss,
            float newTrackingDuration,
            float newFallingDuration,
            float newEmbeddedLifetime,
            int newMaxEmbeddedMissiles,
            float newEmbeddedScaleMultiplier,
            float newFallingSpinDegrees,
            float newEmbeddedExplosionRadius,
            float newEmbeddedBossMaxHpDamagePercent,
            float newEmbeddedMonsterDamage,
            GameObject landingEffectPrefab,
            GameObject explosionEffectPrefab
        )
        {
            ownerBossController =
                ownerBoss;

            trackingDuration =
                Mathf.Max(0.05f, newTrackingDuration);

            fallingDuration =
                Mathf.Max(0f, newFallingDuration);

            embeddedLifetime =
                Mathf.Max(0.1f, newEmbeddedLifetime);

            maxEmbeddedMissiles =
                Mathf.Max(1, newMaxEmbeddedMissiles);

            embeddedScaleMultiplier =
                Mathf.Max(0.05f, newEmbeddedScaleMultiplier);

            fallingSpinDegrees =
                newFallingSpinDegrees;

            embeddedExplosionRadius =
                Mathf.Max(0.05f, newEmbeddedExplosionRadius);

            embeddedBossMaxHpDamagePercent =
                Mathf.Clamp01(
                    newEmbeddedBossMaxHpDamagePercent);

            embeddedMonsterDamage =
                Mathf.Max(0f, newEmbeddedMonsterDamage);

            embeddedLandingEffectPrefab =
                landingEffectPrefab;

            embeddedExplosionEffectPrefab =
                explosionEffectPrefab;

            embeddedBehaviorEnabled = true;

            // Init 직후 호출되는 것이 원칙이지만,
            // 안전하게 현재 상태/시간도 Tracking 기준으로 맞춥니다.
            currentState =
                BossHomingMissileState.Tracking;

            stateStartTime =
                Time.time;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossHomingMissile] 매립 모드 설정 | " +
                    $"Tracking={trackingDuration:0.##}s, " +
                    $"Falling={fallingDuration:0.##}s, " +
                    $"Embedded={embeddedLifetime:0.##}s, " +
                    $"ExplosionRadius={embeddedExplosionRadius:0.##}, " +
                    $"BossDamage={embeddedBossMaxHpDamagePercent * 100f:0.##}%",
                    this
                );
            }
        }

        private void Awake()
        {
            rb =
                GetComponent<Rigidbody2D>();

            missileColliders =
                GetComponentsInChildren
                <
                    Collider2D
                >(true);

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void OnDestroy()
        {
            activeEmbeddedMissiles.Remove(this);
        }

        private void Update()
        {
            if (!initialized ||
                isDestroying)
            {
                return;
            }

            if (!embeddedBehaviorEnabled)
            {
                UpdateLegacyBehavior();
                return;
            }

            switch (currentState)
            {
                case BossHomingMissileState.Tracking:
                    UpdateTrackingState();
                    break;

                case BossHomingMissileState.Falling:
                    UpdateFallingState();
                    break;

                case BossHomingMissileState.Embedded:
                    UpdateEmbeddedState();
                    break;
            }
        }

        /// <summary>
        /// ConfigureEmbeddedBehavior가 호출되지 않은 경우
        /// 기존 호밍 미사일 동작을 유지합니다.
        /// </summary>
        private void UpdateLegacyBehavior()
        {
            if (lifeTime > 0f &&
                Time.time >=
                spawnTime + lifeTime)
            {
                DestroyMissile(true);
                return;
            }

            MoveMissile();

            if (destroyByPlayerProjectile)
            {
                CheckPlayerProjectileOverlapLegacy();
            }
        }

        private void UpdateTrackingState()
        {
            float elapsed =
                Time.time - stateStartTime;

            if (elapsed >= trackingDuration)
            {
                BeginFalling();
                return;
            }

            // 신규 규칙:
            // Tracking 중에는 플레이어 투사체로 파괴할 수 없습니다.
            MoveMissile();
        }

        private void BeginFalling()
        {
            if (currentState !=
                BossHomingMissileState.Tracking)
            {
                return;
            }

            currentState =
                BossHomingMissileState.Falling;

            stateStartTime =
                Time.time;

            fallingStartScale =
                transform.localScale;

            fallingStartRotation =
                transform.rotation;

            StopRigidbodyMotion();

            // 낙하 중에는 플레이어/투사체와 충돌하지 않습니다.
            SetMissileCollidersEnabled(false);

            if (debugLog)
            {
                Debug.Log(
                    "[BossHomingMissile] 추진력 상실 → Falling",
                    this
                );
            }

            if (fallingDuration <= 0f)
            {
                EnterEmbeddedState();
            }
        }

        private void UpdateFallingState()
        {
            StopRigidbodyMotion();

            float duration =
                Mathf.Max(0.0001f, fallingDuration);

            float t =
                Mathf.Clamp01(
                    (Time.time - stateStartTime) /
                    duration);

            transform.localScale =
                Vector3.Lerp(
                    fallingStartScale,
                    fallingStartScale *
                    embeddedScaleMultiplier,
                    t);

            float startZ =
                fallingStartRotation.eulerAngles.z;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    startZ +
                    fallingSpinDegrees * t);

            if (t >= 1f)
            {
                EnterEmbeddedState();
            }
        }

        private void EnterEmbeddedState()
        {
            currentState =
                BossHomingMissileState.Embedded;

            stateStartTime =
                Time.time;

            StopRigidbodyMotion();

            transform.localScale =
                fallingStartScale *
                embeddedScaleMultiplier;

            SetMissileCollidersEnabled(true);

            if (embeddedLandingEffectPrefab != null)
            {
                Instantiate(
                    embeddedLandingEffectPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }

            RegisterEmbeddedMissile();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossHomingMissile] 바닥 매립 완료 | " +
                    $"Lifetime={embeddedLifetime:0.##}s",
                    this
                );
            }
        }

        private void UpdateEmbeddedState()
        {
            StopRigidbodyMotion();

            if (Time.time >=
                stateStartTime +
                embeddedLifetime)
            {
                ExpireEmbeddedMissile();
                return;
            }

            if (destroyByPlayerProjectile &&
                CheckPlayerProjectileOverlapEmbedded())
            {
                DetonateEmbedded(
                    1f,
                    "PlayerProjectile"
                );
            }
        }

        private void MoveMissile()
        {
            Transform targetTransform =
                GetTargetTransform();

            if (targetTransform == null)
            {
                MoveForwardOnly();
                return;
            }

            Vector2 currentPosition =
                transform.position;

            Vector2 targetPosition =
                targetTransform.position;

            Vector2 desiredDirection =
                moveDirection;

            float elapsed =
                Time.time - spawnTime;

            if (elapsed <
                initialSpreadDuration)
            {
                desiredDirection =
                    moveDirection;
            }
            else
            {
                Vector2 toPlayer =
                    targetPosition -
                    currentPosition;

                if (toPlayer.sqrMagnitude >
                    0.0001f)
                {
                    Vector2 toPlayerDirection =
                        toPlayer.normalized;

                    Vector2 sideDirection =
                        new Vector2(
                            -toPlayerDirection.y,
                            toPlayerDirection.x
                        );

                    float centerIndex =
                        (laneCount - 1) *
                        0.5f;

                    float laneValue =
                        laneIndex -
                        centerIndex;

                    float distanceToPlayer =
                        toPlayer.magnitude;

                    float laneFade =
                        Mathf.Clamp01(
                            distanceToPlayer /
                            Mathf.Max(
                                0.01f,
                                laneFadeDistance)
                        );

                    Vector2 laneTargetPosition =
                        targetPosition +
                        sideDirection *
                        laneValue *
                        laneOffsetDistance *
                        laneFade;

                    Vector2 toLaneTarget =
                        laneTargetPosition -
                        currentPosition;

                    if (toLaneTarget.sqrMagnitude >
                        0.0001f)
                    {
                        desiredDirection =
                            toLaneTarget.normalized;
                    }
                    else
                    {
                        desiredDirection =
                            toPlayerDirection;
                    }
                }
            }

            moveDirection =
                Vector2.Lerp(
                    moveDirection,
                    desiredDirection,
                    Mathf.Clamp01(
                        turnSpeed *
                        Time.deltaTime)
                ).normalized;

            Vector2 nextPosition =
                currentPosition +
                moveDirection *
                speed *
                Time.deltaTime;

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

            ApplyVisualRotation(
                moveDirection);
        }

        private void MoveForwardOnly()
        {
            Vector2 currentPosition =
                transform.position;

            Vector2 nextPosition =
                currentPosition +
                moveDirection *
                speed *
                Time.deltaTime;

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

            ApplyVisualRotation(
                moveDirection);
        }

        private Transform GetTargetTransform()
        {
            if (targetCharacter == null)
            {
                return null;
            }

            if (targetCharacter.CenterTransform != null)
            {
                return
                    targetCharacter.CenterTransform;
            }

            return targetCharacter.transform;
        }

        private void ApplyVisualRotation(
            Vector2 direction)
        {
            if (!rotateToMoveDirection ||
                direction == Vector2.zero)
            {
                return;
            }

            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x) *
                Mathf.Rad2Deg;

            transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle +
                    visualForwardAngleOffset
                );
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            if (!initialized ||
                isDestroying ||
                other == null)
            {
                return;
            }

            if (!embeddedBehaviorEnabled)
            {
                if (TryHitPlayer(other))
                {
                    return;
                }

                if (destroyByPlayerProjectile &&
                    TryDetectPlayerProjectile(other))
                {
                    DestroyMissile(true);
                }

                return;
            }

            // Tracking 중에는 플레이어 충돌만 처리합니다.
            // 플레이어 투사체는 미사일을 파괴하지 못합니다.
            if (currentState ==
                BossHomingMissileState.Tracking)
            {
                TryHitPlayer(other);
                return;
            }

            // Falling 동안 Collider 자체가 꺼져 있지만
            // 혹시 콜백이 들어오더라도 무시합니다.
            if (currentState ==
                BossHomingMissileState.Falling)
            {
                return;
            }

            if (currentState ==
                BossHomingMissileState.Embedded)
            {
                if (destroyByPlayerProjectile &&
                    TryDetectPlayerProjectile(other))
                {
                    DetonateEmbedded(
                        1f,
                        "PlayerProjectileTrigger"
                    );
                }
            }
        }

        private bool TryHitPlayer(
            Collider2D other)
        {
            if (targetCharacter == null ||
                other == null)
            {
                return false;
            }

            Character character =
                other.GetComponentInParent
                <
                    Character
                >();

            if (character == null ||
                character != targetCharacter)
            {
                return false;
            }

            targetCharacter.TakeDamage(
                damage);

            DestroyMissile(true);

            return true;
        }

        private bool TryDetectPlayerProjectile(
            Collider2D other)
        {
            if (other == null)
            {
                return false;
            }

            if (
                (
                    playerProjectileLayerMask.value &
                    (1 << other.gameObject.layer)
                ) == 0
            )
            {
                return false;
            }

            Projectile projectile =
                other.GetComponentInParent
                <
                    Projectile
                >();

            return projectile != null;
        }

        /// <summary>
        /// 구버전 호밍 미사일 보조 충돌 검사입니다.
        /// </summary>
        private void CheckPlayerProjectileOverlapLegacy()
        {
            if (projectileHitCheckRadius <= 0f)
            {
                return;
            }

            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    transform.position,
                    projectileHitCheckRadius,
                    playerProjectileLayerMask
                );

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider2D hit =
                    hits[i];

                if (hit == null ||
                    IsOwnCollider(hit))
                {
                    continue;
                }

                Projectile projectile =
                    hit.GetComponentInParent
                    <
                        Projectile
                    >();

                if (projectile != null)
                {
                    DestroyMissile(true);
                    return;
                }
            }
        }

        /// <summary>
        /// 매립 상태에서 플레이어 투사체가 미사일을 맞혔는지 검사합니다.
        /// </summary>
        private bool CheckPlayerProjectileOverlapEmbedded()
        {
            if (projectileHitCheckRadius <= 0f)
            {
                return false;
            }

            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    transform.position,
                    projectileHitCheckRadius,
                    playerProjectileLayerMask
                );

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider2D hit =
                    hits[i];

                if (hit == null ||
                    IsOwnCollider(hit))
                {
                    continue;
                }

                Projectile projectile =
                    hit.GetComponentInParent
                    <
                        Projectile
                    >();

                if (projectile != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Step 6B에서 돌진 충돌 시 사용할 확장 지점입니다.
        ///
        /// bossDamageMultiplier = 2이면
        /// 기본 2.5% 설정 기준 약 5% Max HP 자해가 됩니다.
        /// 현재 Step 6A에서는 직접 호출하지 않습니다.
        /// </summary>
        public bool TryDetonateByBossCharge(
            float bossDamageMultiplier = 2f)
        {
            if (!IsEmbedded ||
                isDestroying)
            {
                return false;
            }

            DetonateEmbedded(
                Mathf.Max(
                    0f,
                    bossDamageMultiplier),
                "BossCharge"
            );

            return true;
        }

        private void DetonateEmbedded(
            float bossDamageMultiplier,
            string cause)
        {
            if (currentState !=
                    BossHomingMissileState.Embedded ||
                isDestroying)
            {
                return;
            }

            isDestroying = true;
            currentState =
                BossHomingMissileState.Destroyed;

            activeEmbeddedMissiles.Remove(
                this);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossHomingMissile] 매립 미사일 기폭 | " +
                    $"Cause={cause}, " +
                    $"Radius={embeddedExplosionRadius:0.##}, " +
                    $"BossMultiplier={bossDamageMultiplier:0.##}",
                    this
                );
            }

            ApplyEmbeddedExplosionDamage(
                bossDamageMultiplier);

            GameObject explosionEffect =
                embeddedExplosionEffectPrefab != null
                    ? embeddedExplosionEffectPrefab
                    : destroyEffectPrefab;

            if (explosionEffect != null)
            {
                Instantiate(
                    explosionEffect,
                    transform.position,
                    Quaternion.identity
                );
            }

            Destroy(gameObject);
        }

        /// <summary>
        /// 매립 미사일 폭발 피해.
        ///
        /// 플레이어 피해는 의도적으로 없습니다.
        /// </summary>
        private void ApplyEmbeddedExplosionDamage(
            float bossDamageMultiplier)
        {
            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    transform.position,
                    embeddedExplosionRadius
                );

            DamageNearestBossPart(
                hits,
                bossDamageMultiplier);

            DamageOrdinaryMonsters(
                hits);
        }

        private void DamageNearestBossPart(
            Collider2D[] hits,
            float bossDamageMultiplier)
        {
            if (hits == null ||
                hits.Length == 0 ||
                embeddedBossMaxHpDamagePercent <= 0f)
            {
                return;
            }

            Transform ownerRoot =
                ownerBossController != null
                    ? ownerBossController.transform.root
                    : null;

            BossPartDamageTestPart nearestPart =
                null;

            float nearestDistanceSqr =
                float.MaxValue;

            HashSet<int> visitedPartIds =
                new HashSet<int>();

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider2D hit =
                    hits[i];

                if (hit == null)
                {
                    continue;
                }

                BossPartDamageTestPart part =
                    hit.GetComponentInParent
                    <
                        BossPartDamageTestPart
                    >(true);

                if (part == null ||
                    part.IsBroken)
                {
                    continue;
                }

                // 이 미사일을 발사한 보스만 자해 대상으로 봅니다.
                if (ownerRoot != null &&
                    part.transform.root != ownerRoot)
                {
                    continue;
                }

                int partId =
                    part.gameObject.GetInstanceID();

                if (!visitedPartIds.Add(partId))
                {
                    continue;
                }

                float distanceSqr =
                    (
                        (Vector2)part.transform.position -
                        (Vector2)transform.position
                    ).sqrMagnitude;

                if (distanceSqr <
                    nearestDistanceSqr)
                {
                    nearestDistanceSqr =
                        distanceSqr;

                    nearestPart =
                        part;
                }
            }

            if (nearestPart == null)
            {
                return;
            }

            BossPartDamageTestRootController
                partRoot =
                    nearestPart.transform.root
                        .GetComponentInChildren
                        <
                            BossPartDamageTestRootController
                        >(true);

            if (partRoot == null ||
                partRoot.TotalMaxHealth <= 0f)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        "[BossHomingMissile] 보스 파츠는 찾았지만 " +
                        "BossPartDamageTestRootController 또는 TotalMaxHealth를 찾지 못했습니다.",
                        this
                    );
                }

                return;
            }

            float selfDamage =
                partRoot.TotalMaxHealth *
                embeddedBossMaxHpDamagePercent *
                Mathf.Max(
                    0f,
                    bossDamageMultiplier);

            float actualDamage =
                nearestPart.TakeDamageFromSource(
                    selfDamage,
                    Vector2.zero,
                    false,
                    BossDamageSourceType.BossSelf
                );

            if (debugLog)
            {
                Debug.Log(
                    $"[BossHomingMissile] 보스 자해 | " +
                    $"Part={nearestPart.PartType}, " +
                    $"Requested={selfDamage:0.##}, " +
                    $"Actual={actualDamage:0.##}, " +
                    $"BossMaxHP={partRoot.TotalMaxHealth:0.##}",
                    this
                );
            }
        }

        private void DamageOrdinaryMonsters(
            Collider2D[] hits)
        {
            if (hits == null ||
                hits.Length == 0 ||
                embeddedMonsterDamage <= 0f)
            {
                return;
            }

            HashSet<int> damagedMonsterIds =
                new HashSet<int>();

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider2D hit =
                    hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster monster =
                    hit.GetComponentInParent
                    <
                        Monster
                    >();

                if (monster == null)
                {
                    continue;
                }

                // BossController/보스 파츠가 포함된 대상은
                // 위의 BossSelf 경로에서만 처리합니다.
                if (BossTargetUtility.IsBossTarget(
                        monster))
                {
                    continue;
                }

                int monsterId =
                    monster.gameObject
                        .GetInstanceID();

                if (!damagedMonsterIds.Add(
                        monsterId))
                {
                    continue;
                }

                monster.TakeDamage(
                    embeddedMonsterDamage,
                    Vector2.zero,
                    false
                );

                if (debugLog)
                {
                    Debug.Log(
                        $"[BossHomingMissile] 일반 몬스터 폭발 피해 | " +
                        $"Target={monster.gameObject.name}, " +
                        $"Damage={embeddedMonsterDamage:0.##}",
                        this
                    );
                }
            }
        }

        private void RegisterEmbeddedMissile()
        {
            CleanupEmbeddedList();

            if (!activeEmbeddedMissiles.Contains(
                    this))
            {
                activeEmbeddedMissiles.Add(
                    this);
            }

            int sameOwnerCount =
                CountEmbeddedForSameOwner();

            while (sameOwnerCount >
                   maxEmbeddedMissiles)
            {
                BossHomingMissile oldest =
                    FindOldestEmbeddedForSameOwner(
                        this);

                if (oldest == null)
                {
                    break;
                }

                oldest.ExpireEmbeddedMissile();

                CleanupEmbeddedList();

                sameOwnerCount =
                    CountEmbeddedForSameOwner();
            }
        }

        private int CountEmbeddedForSameOwner()
        {
            int count = 0;

            for (int i = 0;
                 i < activeEmbeddedMissiles.Count;
                 i++)
            {
                BossHomingMissile missile =
                    activeEmbeddedMissiles[i];

                if (missile == null ||
                    !missile.IsEmbedded)
                {
                    continue;
                }

                if (IsSameOwner(missile))
                {
                    count++;
                }
            }

            return count;
        }

        private BossHomingMissile
            FindOldestEmbeddedForSameOwner(
                BossHomingMissile exclude)
        {
            for (int i = 0;
                 i < activeEmbeddedMissiles.Count;
                 i++)
            {
                BossHomingMissile missile =
                    activeEmbeddedMissiles[i];

                if (missile == null ||
                    missile == exclude ||
                    !missile.IsEmbedded)
                {
                    continue;
                }

                if (IsSameOwner(missile))
                {
                    return missile;
                }
            }

            return null;
        }

        private bool IsSameOwner(
            BossHomingMissile other)
        {
            if (other == null)
            {
                return false;
            }

            Transform myRoot =
                ownerBossController != null
                    ? ownerBossController.transform.root
                    : null;

            Transform otherRoot =
                other.ownerBossController != null
                    ? other.ownerBossController.transform.root
                    : null;

            return myRoot == otherRoot;
        }

        private static void CleanupEmbeddedList()
        {
            for (int i =
                     activeEmbeddedMissiles.Count - 1;
                 i >= 0;
                 i--)
            {
                BossHomingMissile missile =
                    activeEmbeddedMissiles[i];

                if (missile == null ||
                    !missile.IsEmbedded)
                {
                    activeEmbeddedMissiles.RemoveAt(
                        i);
                }
            }
        }

        private void ExpireEmbeddedMissile()
        {
            if (isDestroying)
            {
                return;
            }

            activeEmbeddedMissiles.Remove(
                this);

            if (debugLog)
            {
                Debug.Log(
                    "[BossHomingMissile] 매립 미사일 시간 만료/개수 제한 제거",
                    this
                );
            }

            DestroyMissile(false);
        }

        private bool IsOwnCollider(
            Collider2D collider)
        {
            if (collider == null ||
                missileColliders == null)
            {
                return false;
            }

            for (int i = 0;
                 i < missileColliders.Length;
                 i++)
            {
                if (missileColliders[i] ==
                    collider)
                {
                    return true;
                }
            }

            return false;
        }

        private void SetMissileCollidersEnabled(
            bool enabled)
        {
            if (missileColliders == null)
            {
                missileColliders =
                    GetComponentsInChildren
                    <
                        Collider2D
                    >(true);
            }

            for (int i = 0;
                 i < missileColliders.Length;
                 i++)
            {
                Collider2D collider =
                    missileColliders[i];

                if (collider != null)
                {
                    collider.enabled =
                        enabled;
                }
            }
        }

        private void StopRigidbodyMotion()
        {
            if (rb == null)
            {
                return;
            }

            rb.velocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;
        }

        private void DestroyMissile(
            bool spawnEffect)
        {
            if (isDestroying)
            {
                return;
            }

            isDestroying = true;

            currentState =
                BossHomingMissileState.Destroyed;

            activeEmbeddedMissiles.Remove(
                this);

            if (spawnEffect &&
                destroyEffectPrefab != null)
            {
                Instantiate(
                    destroyEffectPrefab,
                    transform.position,
                    Quaternion.identity
                );
            }

            Destroy(gameObject);
        }
    }
}

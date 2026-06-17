using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 소화효소 몬스터.
    /// 
    /// 기존 Monster 풀에 넣지 않는 중립 개체입니다.
    /// - 플레이어 투사체/공격에는 IDamageable로 피해를 받습니다.
    /// - 일반 적 몬스터에게는 피해를 받지 않도록, 적 공격 구조와 분리된 독립 개체로 둡니다.
    /// - 일반 몬스터를 근접 공격합니다.
    /// - 플레이어가 소화효소를 처치했을 때만 난이도 상승 매니저에 알립니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class DigestiveEnzymeMonster : IDamageable
    {
        [Header("References")]
        [Tooltip("소화효소 몬스터의 스프라이트 렌더러입니다. 방향 전환과 사망 시 숨김 처리에 사용합니다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("플레이어 공격을 맞는 몸통 콜라이더입니다. Trigger로 사용하는 것을 권장합니다.")]
        [SerializeField] private Collider2D bodyCollider;

        [Tooltip("사망 시 재생할 파티클입니다. 없어도 동작합니다.")]
        [SerializeField] private ParticleSystem deathParticles;

        [Header("Health")]
        [Tooltip("소화효소 몬스터의 최대 체력입니다. 플레이어 공격으로만 깎이는 것을 전제로 사용합니다.")]
        [SerializeField] private float maxHealth = 30f;

        [Tooltip("사망 후 오브젝트를 제거하기까지 기다리는 시간입니다. 사망 파티클이 있으면 0.3~0.8초 정도를 추천합니다.")]
        [SerializeField] private float destroyDelayAfterDeath = 0.4f;

        [Header("Targeting")]
        [Tooltip("소화효소가 공격 대상으로 찾을 일반 적 몬스터 레이어입니다. 보통 Monster 레이어를 지정합니다.")]
        [SerializeField] private LayerMask enemyMonsterLayer;

        [Tooltip("공격할 적 몬스터를 탐색하는 반경입니다.")]
        [SerializeField] private float searchRadius = 7f;

        [Tooltip("새 공격 대상을 다시 찾는 주기입니다. 너무 낮으면 성능 부담이 늘 수 있습니다.")]
        [SerializeField] private float retargetInterval = 0.25f;

        [Tooltip("BossMonster를 공격 대상에서 제외할지 여부입니다. 보스 패턴 안정성을 위해 true를 추천합니다.")]
        [SerializeField] private bool ignoreBossMonsters = true;

        [Header("Movement")]
        [Tooltip("소화효소 몬스터의 이동 속도입니다.")]
        [SerializeField] private float moveSpeed = 1.2f;

        [Tooltip("대상에게 이 거리 안으로 들어오면 더 이상 접근하지 않습니다.")]
        [SerializeField] private float stopDistance = 0.55f;

        [Tooltip("주변에 공격 대상이 없을 때 제자리에서 살짝 움직이는 범위입니다.")]
        [SerializeField] private float idleWanderRadius = 1.2f;

        [Tooltip("대상이 없을 때 배회 이동 속도 배율입니다. 0이면 완전히 멈춥니다.")]
        [SerializeField] private float idleWanderSpeedMultiplier = 0.35f;

        [Header("Melee Attack")]
        [Tooltip("소화효소의 근접 공격력입니다. 기본값은 요청 기준인 3입니다.")]
        [SerializeField] private float attackDamage = 3f;

        [Tooltip("근접 공격 쿨다운입니다. 값이 클수록 공격 속도가 느려집니다.")]
        [SerializeField] private float attackCooldown = 1.35f;

        [Tooltip("근접 공격이 닿는 거리입니다. 너무 크면 원거리 공격처럼 보일 수 있으니 0.6~1.0 정도를 추천합니다.")]
        [SerializeField] private float attackRange = 0.8f;

        [Tooltip("소화효소가 적 몬스터를 때릴 때 적용할 넉백 세기입니다.")]
        [SerializeField] private float attackKnockback = 0.4f;

        [Header("Debug")]
        [Tooltip("소화효소 스폰, 피격, 공격, 사망 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private Rigidbody2D rb;
        private float currentHealth;
        private bool isAlive;
        private Monster currentTarget;
        private float nextRetargetTime;
        private float lastAttackTime = -999f;
        private Vector2 idleAnchorPosition;
        private DigestiveEnzymeFieldSpawner ownerSpawner;

        private static FieldInfo monsterCurrentHealthField;

        public bool IsAlive => isAlive;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        private void Reset()
        {
            enemyMonsterLayer = LayerMask.GetMask("Monster");
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponentInChildren<Collider2D>();
            }

            if (bodyCollider != null)
            {
                // 적과 몸싸움으로 밀고 밀리는 상황을 줄이고,
                // 플레이어 투사체의 Trigger 충돌은 받을 수 있게 Trigger 사용을 권장합니다.
                bodyCollider.isTrigger = true;
            }

            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            CacheMonsterHealthField();
            ResetRuntimeState();
        }

        private void OnEnable()
        {
            ResetRuntimeState();
        }

        private void Update()
        {
            if (!isAlive)
            {
                return;
            }

            if (Time.time >= nextRetargetTime)
            {
                currentTarget = FindNearestEnemyMonster();
                nextRetargetTime = Time.time + Mathf.Max(0.05f, retargetInterval);
            }

            TryMeleeAttack();
            UpdateSpriteDirection();
        }

        private void FixedUpdate()
        {
            if (!isAlive || rb == null)
            {
                return;
            }

            Vector2 velocity = Vector2.zero;

            if (IsValidTarget(currentTarget))
            {
                Vector2 toTarget = (Vector2)currentTarget.transform.position - rb.position;
                float distance = toTarget.magnitude;

                if (distance > stopDistance)
                {
                    velocity = toTarget.normalized * moveSpeed;
                }
            }
            else
            {
                velocity = GetIdleWanderVelocity();
            }

            rb.velocity = velocity;
        }

        /// <summary>
        /// 스포너가 생성 직후 자신을 등록할 때 호출합니다.
        /// </summary>
        public void SetupSpawner(DigestiveEnzymeFieldSpawner spawner)
        {
            ownerSpawner = spawner;
        }

        public override void TakeDamage(float damage, Vector2 knockback = default, bool isCritical = false)
        {
            if (!isAlive)
            {
                return;
            }

            if (damage <= 0f)
            {
                return;
            }

            currentHealth = Mathf.Max(0f, currentHealth - damage);
            Knockback(knockback);

            if (debugLog)
            {
                Debug.Log(
                    $"[DigestiveEnzymeMonster] 플레이어 공격 피격. damage={damage}, hp={currentHealth}/{maxHealth}"
                );
            }

            if (currentHealth <= 0f)
            {
                Die(killedByPlayer: true);
            }
        }

        public override void Knockback(Vector2 knockback)
        {
            if (rb == null)
            {
                return;
            }

            rb.velocity += knockback;
        }

        /// <summary>
        /// 자연 소멸, 테스트 정리, 씬 전환 등으로 제거할 때 사용합니다.
        /// 이 경우 난이도 상승은 발생하지 않습니다.
        /// </summary>
        public void KillWithoutDifficultyIncrease()
        {
            if (!isAlive)
            {
                return;
            }

            Die(killedByPlayer: false);
        }

        private void ResetRuntimeState()
        {
            currentHealth = maxHealth;
            isAlive = true;
            currentTarget = null;
            nextRetargetTime = 0f;
            lastAttackTime = -999f;
            idleAnchorPosition = transform.position;

            if (bodyCollider != null)
            {
                bodyCollider.enabled = true;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
            }
        }

        private Monster FindNearestEnemyMonster()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(
                transform.position,
                searchRadius,
                enemyMonsterLayer
            );

            Monster nearestMonster = null;
            float nearestSqrDistance = float.PositiveInfinity;
            Vector2 myPosition = transform.position;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider2D hit = hits[i];

                if (hit == null)
                {
                    continue;
                }

                Monster monster = hit.GetComponentInParent<Monster>();

                if (!IsValidTarget(monster))
                {
                    continue;
                }

                if (ignoreBossMonsters && monster is BossMonster)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)monster.transform.position - myPosition).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestMonster = monster;
                }
            }

            return nearestMonster;
        }

        private bool IsValidTarget(Monster monster)
        {
            if (monster == null)
            {
                return false;
            }

            if (!monster.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (monster.HP <= 0f)
            {
                return false;
            }

            return true;
        }

        private void TryMeleeAttack()
        {
            if (!IsValidTarget(currentTarget))
            {
                return;
            }

            if (Time.time - lastAttackTime < attackCooldown)
            {
                return;
            }

            float distance = Vector2.Distance(transform.position, currentTarget.transform.position);

            if (distance > attackRange)
            {
                return;
            }

            Vector2 direction = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
            Vector2 knockback = direction * attackKnockback;

            ApplyNeutralDamageToMonster(currentTarget, attackDamage, knockback);

            lastAttackTime = Time.time;

            if (debugLog)
            {
                Debug.Log(
                    $"[DigestiveEnzymeMonster] 일반 몬스터 근접 공격. target={currentTarget.name}, damage={attackDamage}"
                );
            }
        }

        /// <summary>
        /// 소화효소가 일반 몬스터를 때리는 처리입니다.
        /// 
        /// 기존 Monster.TakeDamage()는 피해 출처를 받지 않기 때문에,
        /// 그대로 막타를 치면 플레이어 처치처럼 처리될 수 있습니다.
        /// 그래서 막타가 아닌 경우에는 기존 TakeDamage()를 사용하고,
        /// 막타일 때는 currentHealth를 0으로 만든 뒤 Killed(false)를 호출하여
        /// 플레이어 처치 보상/카운트로 처리되지 않게 합니다.
        /// </summary>
        private void ApplyNeutralDamageToMonster(Monster monster, float damage, Vector2 knockback)
        {
            if (!IsValidTarget(monster))
            {
                return;
            }

            float currentMonsterHp = monster.HP;

            if (damage < currentMonsterHp)
            {
                monster.TakeDamage(damage, knockback, false);
                return;
            }

            // 막타가 될 상황이면, 0.01 HP를 남기는 만큼만 기존 피격 연출을 사용합니다.
            float nonLethalDamage = Mathf.Max(0f, currentMonsterHp - 0.01f);

            if (nonLethalDamage > 0f)
            {
                monster.TakeDamage(nonLethalDamage, knockback, false);
            }
            else
            {
                monster.Knockback(knockback);
            }

            if (monsterCurrentHealthField != null)
            {
                monsterCurrentHealthField.SetValue(monster, 0f);
                monster.StartCoroutine(monster.Killed(false));
            }
            else
            {
                Debug.LogWarning(
                    "[DigestiveEnzymeMonster] Monster.currentHealth 필드를 찾지 못했습니다. " +
                    "임시로 Monster.TakeDamage() 막타 처리를 사용합니다."
                );

                monster.TakeDamage(damage, knockback, false);
            }
        }

        private Vector2 GetIdleWanderVelocity()
        {
            if (idleWanderSpeedMultiplier <= 0f)
            {
                return Vector2.zero;
            }

            Vector2 offset = new Vector2(
                Mathf.Sin(Time.time * 0.8f),
                Mathf.Cos(Time.time * 0.6f)
            ) * idleWanderRadius;

            Vector2 targetPosition = idleAnchorPosition + offset;
            Vector2 toIdleTarget = targetPosition - (Vector2)transform.position;

            if (toIdleTarget.sqrMagnitude < 0.02f)
            {
                return Vector2.zero;
            }

            return toIdleTarget.normalized * moveSpeed * idleWanderSpeedMultiplier;
        }

        private void UpdateSpriteDirection()
        {
            if (spriteRenderer == null || rb == null)
            {
                return;
            }

            if (Mathf.Abs(rb.velocity.x) > 0.02f)
            {
                spriteRenderer.flipX = rb.velocity.x < 0f;
            }
        }

        private void Die(bool killedByPlayer)
        {
            if (!isAlive)
            {
                return;
            }

            isAlive = false;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            if (bodyCollider != null)
            {
                bodyCollider.enabled = false;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            ownerSpawner?.NotifyEnzymeRemoved(this);

            if (killedByPlayer)
            {
                DigestiveEnzymeDifficultyManager.NotifyDigestiveEnzymeKilledByPlayer(this);
            }

            if (debugLog)
            {
                Debug.Log(
                    killedByPlayer
                        ? "[DigestiveEnzymeMonster] 플레이어가 소화효소 몬스터를 처치했습니다. 난이도 상승 판정 발생."
                        : "[DigestiveEnzymeMonster] 소화효소 몬스터가 난이도 상승 없이 제거되었습니다."
                );
            }

            Destroy(gameObject, Mathf.Max(0f, destroyDelayAfterDeath));
        }

        private void OnDisable()
        {
            ownerSpawner?.NotifyEnzymeRemoved(this);
        }

        private void OnDestroy()
        {
            ownerSpawner?.NotifyEnzymeRemoved(this);
        }

        private static void CacheMonsterHealthField()
        {
            if (monsterCurrentHealthField != null)
            {
                return;
            }

            monsterCurrentHealthField = typeof(Monster).GetField(
                "currentHealth",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, searchRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
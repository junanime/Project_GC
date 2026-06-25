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
    ///
    /// 이번 수정 핵심:
    /// 기존 몬스터는 루트의 Monster Legs 콜라이더와 별도로,
    /// 스프라이트 자식 오브젝트에 Monster 레이어 피격 히트박스를 가지고 있습니다.
    /// 소화효소도 같은 방식으로 플레이어 투사체가 맞출 수 있는 자식 히트박스를 자동 생성합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public class DigestiveEnzymeMonster : IDamageable
    {
        private const string AutoProjectileHitboxName = "Projectile Hitbox";

        [Header("References")]
        [Tooltip("소화효소 몬스터의 스프라이트 렌더러입니다. 방향 전환과 사망 시 숨김 처리에 사용합니다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("소화효소의 몸통 콜라이더입니다. 루트 오브젝트의 CircleCollider2D 또는 CapsuleCollider2D를 넣는 것을 권장합니다.")]
        [SerializeField] private Collider2D bodyCollider;

        [Tooltip("플레이어 투사체가 실제로 맞추는 피격 전용 히트박스입니다. 비워두면 자동으로 자식 오브젝트를 생성합니다.")]
        [SerializeField] private Collider2D projectileHitbox;

        [Tooltip("데미지 숫자 표시를 위해 사용할 EntityManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private EntityManager entityManager;

        [Tooltip("사망 시 재생할 파티클입니다. 없어도 동작합니다.")]
        [SerializeField] private ParticleSystem deathParticles;

        [Header("Projectile Hitbox Compatibility")]
        [Tooltip("기존 몬스터처럼 플레이어 투사체가 맞출 수 있는 자식 히트박스를 자동 생성할지 여부입니다.")]
        [SerializeField] private bool autoCreateProjectileHitbox = true;

        [Tooltip("자동 생성되는 투사체 피격 히트박스에 지정할 레이어 이름입니다. 일반적으로 플레이어 투사체의 Monster Layer와 맞추기 위해 Monster를 사용합니다.")]
        [SerializeField] private string projectileHitboxLayerName = "Monster";

        [Tooltip("자동 생성되는 투사체 피격 히트박스의 반지름입니다. 소화효소 스프라이트 크기에 맞게 조절하세요.")]
        [SerializeField] private float projectileHitboxRadius = 0.45f;

        [Tooltip("자동 생성되는 투사체 피격 히트박스의 로컬 위치 보정값입니다. 위쪽 몸통에 맞추고 싶으면 Y값을 올리세요.")]
        [SerializeField] private Vector2 projectileHitboxLocalOffset = new Vector2(0f, 0.35f);

        [Header("Health")]
        [Tooltip("소화효소 몬스터의 최대 체력입니다. 플레이어 공격으로만 깎이는 것을 전제로 사용합니다.")]
        [SerializeField] private float maxHealth = 30f;

        [Tooltip("사망 후 오브젝트를 제거하기까지 기다리는 시간입니다. 사망 파티클이 있으면 0.3~0.8초 정도를 추천합니다.")]
        [SerializeField] private float destroyDelayAfterDeath = 0.4f;

        [Header("Hit Feedback")]
        [Tooltip("플레이어 공격에 맞았을 때 기존 몬스터처럼 데미지 숫자를 띄울지 여부입니다.")]
        [SerializeField] private bool showDamageText = true;

        [Tooltip("데미지 숫자가 뜨는 위치 보정값입니다. 소화효소 머리 위에 띄우고 싶으면 Y값을 올리세요.")]
        [SerializeField] private Vector2 damageTextOffset = new Vector2(0f, 0.8f);

        [Header("Targeting")]
        [Tooltip("소화효소가 공격 대상으로 찾을 일반 적 몬스터 레이어입니다. 보통 Monster 또는 Monster Legs를 포함합니다.")]
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
            int monsterMask = LayerMask.GetMask("Monster", "Monster Legs");

            if (monsterMask == 0)
            {
                monsterMask = LayerMask.GetMask("Monster");
            }

            enemyMonsterLayer = monsterMask;
            projectileHitboxLayerName = "Monster";
        }

        private void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            ResolveReferences();
            ConfigurePhysics();
            EnsureProjectileHitbox();
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

            SpawnDamageText(damage, isCritical);

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

        private void ResolveReferences()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponent<Collider2D>();
            }

            if (bodyCollider == null)
            {
                bodyCollider = GetComponentInChildren<Collider2D>(true);
            }

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }
        }

        private void ConfigurePhysics()
        {
            if (rb == null)
            {
                return;
            }

            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            if (bodyCollider != null)
            {
                bodyCollider.isTrigger = true;
            }
        }

        /// <summary>
        /// 기존 일반 몬스터처럼 투사체가 맞출 수 있는 자식 히트박스를 보장합니다.
        ///
        /// 핵심:
        /// - 루트는 Monster Legs여도 됨.
        /// - 자식 Projectile Hitbox는 Monster 레이어로 둠.
        /// - Projectile.cs는 충돌 콜라이더의 부모 1단계에서 IDamageable을 찾으므로,
        ///   히트박스는 반드시 DigestiveEnzymeMonster의 바로 아래 자식이어야 안정적입니다.
        /// </summary>
        private void EnsureProjectileHitbox()
        {
            if (!autoCreateProjectileHitbox)
            {
                if (projectileHitbox != null)
                {
                    projectileHitbox.isTrigger = true;
                    ApplyProjectileHitboxLayer(projectileHitbox.gameObject);
                }

                return;
            }

            if (projectileHitbox == null)
            {
                Transform existingHitbox = transform.Find(AutoProjectileHitboxName);

                if (existingHitbox != null)
                {
                    projectileHitbox = existingHitbox.GetComponent<Collider2D>();
                }
            }

            if (projectileHitbox == null)
            {
                GameObject hitboxObject = new GameObject(AutoProjectileHitboxName);
                hitboxObject.transform.SetParent(transform);
                hitboxObject.transform.localPosition = projectileHitboxLocalOffset;
                hitboxObject.transform.localRotation = Quaternion.identity;
                hitboxObject.transform.localScale = Vector3.one;

                CircleCollider2D circleCollider = hitboxObject.AddComponent<CircleCollider2D>();
                circleCollider.isTrigger = true;
                circleCollider.radius = Mathf.Max(0.05f, projectileHitboxRadius);

                projectileHitbox = circleCollider;
            }
            else
            {
                projectileHitbox.transform.SetParent(transform);
                projectileHitbox.transform.localPosition = projectileHitboxLocalOffset;
                projectileHitbox.transform.localRotation = Quaternion.identity;
                projectileHitbox.transform.localScale = Vector3.one;
                projectileHitbox.isTrigger = true;

                CircleCollider2D circleCollider = projectileHitbox as CircleCollider2D;

                if (circleCollider != null)
                {
                    circleCollider.radius = Mathf.Max(0.05f, projectileHitboxRadius);
                }
            }

            ApplyProjectileHitboxLayer(projectileHitbox.gameObject);
        }

        private void ApplyProjectileHitboxLayer(GameObject hitboxObject)
        {
            if (hitboxObject == null)
            {
                return;
            }

            int layerIndex = LayerMask.NameToLayer(projectileHitboxLayerName);

            if (layerIndex < 0)
            {
                Debug.LogWarning(
                    $"[DigestiveEnzymeMonster] '{projectileHitboxLayerName}' 레이어를 찾지 못했습니다. " +
                    "Project Settings > Tags and Layers에서 레이어 이름을 확인하세요."
                );

                return;
            }

            hitboxObject.layer = layerIndex;
        }

        private void SpawnDamageText(float damage, bool isCritical)
        {
            if (!showDamageText)
            {
                return;
            }

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager == null)
            {
                return;
            }

            Vector2 textPosition = (Vector2)transform.position + damageTextOffset;

            if (projectileHitbox != null)
            {
                textPosition = (Vector2)projectileHitbox.bounds.center + damageTextOffset;
            }
            else if (bodyCollider != null)
            {
                textPosition = (Vector2)bodyCollider.bounds.center + damageTextOffset;
            }

            entityManager.SpawnDamageText(textPosition, damage, isCritical);
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

            if (projectileHitbox != null)
            {
                projectileHitbox.enabled = true;
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

            string targetName = currentTarget.name;

            ApplyNeutralDamageToMonster(currentTarget, attackDamage, knockback);

            lastAttackTime = Time.time;

            if (debugLog)
            {
                Debug.Log(
                    $"[DigestiveEnzymeMonster] 일반 몬스터 근접 공격. target={targetName}, damage={attackDamage}"
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

            if (projectileHitbox != null)
            {
                projectileHitbox.enabled = false;
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

            Gizmos.color = Color.cyan;
            Vector3 hitboxPosition = transform.position + (Vector3)projectileHitboxLocalOffset;
            Gizmos.DrawWireSphere(hitboxPosition, projectileHitboxRadius);
        }
    }
}
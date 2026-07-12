using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 필드에 독립적으로 생성되는 특수 몬스터 공통 베이스입니다.
    ///
    /// 기존 Monster.cs의 시각 규격을 맞춥니다.
    /// - defaultMaterial
    /// - whiteMaterial
    /// - dissolveMaterial
    /// - 피격 시 whiteMaterial 플래시
    /// - 이후 defaultMaterial 복구
    ///
    /// 영양 도둑균, 추격형 보물 몬스터가 이 베이스를 사용합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody2D))]
    public abstract class FieldSpecialMonsterBase : IDamageable
    {
        private const string AutoProjectileHitboxName = "Projectile Hitbox";

        [Header("Common References")]
        [Tooltip("특수 몬스터의 스프라이트 렌더러입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] protected SpriteRenderer spriteRenderer;

        [Tooltip("루트 몸통 콜라이더입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] protected Collider2D bodyCollider;

        [Tooltip("플레이어 투사체가 실제로 맞추는 자식 히트박스입니다. 비워두면 자동으로 생성합니다.")]
        [SerializeField] protected Collider2D projectileHitbox;

        [Tooltip("데미지 숫자와 보상 드랍에 사용할 EntityManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] protected EntityManager entityManager;

        [Tooltip("플레이어 캐릭터입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] protected Character playerCharacter;

        [Tooltip("사망 시 재생할 파티클입니다. 없어도 동작합니다.")]
        [SerializeField] protected ParticleSystem deathParticles;

        [Header("Materials")]
        [Tooltip("기본 상태에서 사용할 몬스터 메터리얼입니다. 기존 몬스터 프리팹의 Default Material을 넣으세요.")]
        [SerializeField] protected Material defaultMaterial;

        [Tooltip("피격 순간 흰색으로 번쩍일 때 사용할 메터리얼입니다. 기존 몬스터 프리팹의 White Material을 넣으세요.")]
        [SerializeField] protected Material whiteMaterial;

        [Tooltip("사망/소멸 연출에 사용할 메터리얼입니다. 현재는 선택 사항이며, 기존 규격 유지를 위해 노출합니다.")]
        [SerializeField] protected Material dissolveMaterial;

        [Tooltip("피격 시 whiteMaterial로 잠깐 바뀌는 플래시를 사용할지 여부입니다.")]
        [SerializeField] protected bool useHitWhiteFlash = true;

        [Tooltip("피격 플래시가 유지되는 시간입니다. 기존 Monster.cs와 같은 기본값 0.15초를 사용합니다.")]
        [SerializeField] protected float hitFlashDuration = 0.15f;

        [Tooltip("사망 직전에 whiteMaterial 피격 플래시를 한 번 재생할지 여부입니다.")]
        [SerializeField] protected bool playHitFlashOnDeath = true;

        [Header("Projectile Hitbox")]
        [Tooltip("투사체 피격 히트박스를 자동 생성할지 여부입니다.")]
        [SerializeField] protected bool autoCreateProjectileHitbox = true;

        [Tooltip("자동 생성되는 투사체 피격 히트박스에 지정할 레이어 이름입니다. 기존 몬스터와 맞추려면 Monster Legs를 사용합니다.")]
        [SerializeField] protected string projectileHitboxLayerName = "Monster Legs";

        [Tooltip("자동 생성되는 투사체 피격 히트박스 반지름입니다.")]
        [SerializeField] protected float projectileHitboxRadius = 0.45f;

        [Tooltip("자동 생성되는 투사체 피격 히트박스 로컬 위치 보정값입니다.")]
        [SerializeField] protected Vector2 projectileHitboxLocalOffset = new Vector2(0f, 0.35f);

        [Header("Health")]
        [Tooltip("특수 몬스터 최대 체력입니다.")]
        [SerializeField] protected float maxHealth = 35f;

        [Tooltip("사망 후 오브젝트를 제거하기까지 기다리는 시간입니다.")]
        [SerializeField] protected float destroyDelayAfterDeath = 0.25f;

        [Header("Hit Feedback")]
        [Tooltip("피격 시 데미지 숫자를 띄울지 여부입니다.")]
        [SerializeField] protected bool showDamageText = true;

        [Tooltip("데미지 숫자가 뜨는 위치 보정값입니다.")]
        [SerializeField] protected Vector2 damageTextOffset = new Vector2(0f, 0.8f);

        [Header("Debug")]
        [Tooltip("특수 몬스터 공통 로그를 출력합니다.")]
        [SerializeField] protected bool debugLog = false;

        protected Rigidbody2D rb;
        protected FieldSpecialMonsterSpawner ownerSpawner;
        protected float currentHealth;
        protected bool isAlive;

        private Coroutine hitAnimationCoroutine;
        private Coroutine deathCoroutine;
        private bool removalNotified;

        public bool IsAlive => isAlive;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();

            ResolveCommonReferences();
            CacheDefaultMaterialIfMissing();
            ConfigurePhysics();
            EnsureProjectileHitbox();
            ResetRuntimeState();
        }

        protected virtual void OnEnable()
        {
            ResetRuntimeState();
        }

        protected virtual void Update()
        {
            if (!isAlive)
            {
                return;
            }

            OnAliveUpdate();
            UpdateSpriteDirection();
        }

        protected virtual void FixedUpdate()
        {
            if (!isAlive)
            {
                return;
            }

            OnAliveFixedUpdate();
        }

        public void SetupRuntime(
            FieldSpecialMonsterSpawner spawner,
            EntityManager manager,
            Character player)
        {
            ownerSpawner = spawner;

            if (manager != null)
            {
                entityManager = manager;
            }

            if (player != null)
            {
                playerCharacter = player;
            }
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

            if (hitAnimationCoroutine != null)
            {
                StopCoroutine(hitAnimationCoroutine);
                hitAnimationCoroutine = null;
            }

            if (currentHealth > 0f)
            {
                hitAnimationCoroutine = StartCoroutine(HitAnimation());
            }
            else
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

            if (knockback == default)
            {
                return;
            }

            rb.velocity += knockback;
        }

        public void DespawnWithoutReward()
        {
            Die(killedByPlayer: false);
        }

        protected virtual void OnAliveUpdate()
        {
        }

        protected virtual void OnAliveFixedUpdate()
        {
        }

        protected virtual void OnKilledByPlayer()
        {
        }

        protected virtual void OnExpiredOrRemoved()
        {
        }

        protected void Die(bool killedByPlayer)
        {
            if (deathCoroutine != null)
            {
                return;
            }

            deathCoroutine = StartCoroutine(DeathRoutine(killedByPlayer));
        }

        private IEnumerator DeathRoutine(bool killedByPlayer)
        {
            if (!isAlive)
            {
                yield break;
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

            if (hitAnimationCoroutine != null)
            {
                StopCoroutine(hitAnimationCoroutine);
                hitAnimationCoroutine = null;
            }

            if (killedByPlayer)
            {
                OnKilledByPlayer();
            }
            else
            {
                OnExpiredOrRemoved();
            }

            NotifyRemovedOnce();

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            if (playHitFlashOnDeath)
            {
                yield return HitAnimation();
            }
            else
            {
                ApplyDefaultMaterial();
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = false;
            }

            float waitTime = Mathf.Max(0f, destroyDelayAfterDeath);

            if (deathParticles != null)
            {
                waitTime = Mathf.Max(waitTime, Mathf.Max(0f, deathParticles.main.duration - hitFlashDuration));
            }

            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            Destroy(gameObject);
        }

        protected IEnumerator HitAnimation()
        {
            if (spriteRenderer != null && useHitWhiteFlash && whiteMaterial != null)
            {
                spriteRenderer.sharedMaterial = whiteMaterial;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, hitFlashDuration));

            ApplyDefaultMaterial();
        }

        protected void ApplyDefaultMaterial()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (defaultMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultMaterial;
            }
        }

        protected void ApplyWhiteMaterial()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (whiteMaterial != null)
            {
                spriteRenderer.sharedMaterial = whiteMaterial;
            }
        }

        protected void ApplyDissolveMaterial()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (dissolveMaterial != null)
            {
                spriteRenderer.sharedMaterial = dissolveMaterial;
            }
        }

        protected void DropExpValueAroundSelf(int totalExp)
        {
            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager == null)
            {
                return;
            }

            int remaining = Mathf.Max(0, totalExp);

            remaining = SpawnExpByUnit(remaining, GemType.Red50, 50);
            remaining = SpawnExpByUnit(remaining, GemType.Green10, 10);
            remaining = SpawnExpByUnit(remaining, GemType.Blue2, 2);
            remaining = SpawnExpByUnit(remaining, GemType.White1, 1);
        }

        protected void DropCoinValueAroundSelf(int totalCoin)
        {
            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager == null)
            {
                return;
            }

            int remaining = Mathf.Max(0, totalCoin);

            remaining = SpawnCoinByUnit(remaining, CoinType.Bag50, 50);
            remaining = SpawnCoinByUnit(remaining, CoinType.Pouch30, 30);
            remaining = SpawnCoinByUnit(remaining, CoinType.Gold5, 5);
            remaining = SpawnCoinByUnit(remaining, CoinType.Silver2, 2);
            remaining = SpawnCoinByUnit(remaining, CoinType.Bronze1, 1);
        }

        private int SpawnExpByUnit(int remaining, GemType gemType, int unitValue)
        {
            while (remaining >= unitValue)
            {
                entityManager.SpawnExpGem(GetRandomDropPosition(), gemType, true);
                remaining -= unitValue;
            }

            return remaining;
        }

        private int SpawnCoinByUnit(int remaining, CoinType coinType, int unitValue)
        {
            while (remaining >= unitValue)
            {
                entityManager.SpawnCoin(GetRandomDropPosition(), coinType, true);
                remaining -= unitValue;
            }

            return remaining;
        }

        protected Vector2 GetRandomDropPosition()
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(0.15f, 0.85f);
            return (Vector2)transform.position + offset;
        }

        private void ResolveCommonReferences()
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

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }
        }

        private void CacheDefaultMaterialIfMissing()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            if (defaultMaterial == null)
            {
                defaultMaterial = spriteRenderer.sharedMaterial;
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
                layerIndex = LayerMask.NameToLayer("Monster Full");
            }

            if (layerIndex < 0)
            {
                layerIndex = LayerMask.NameToLayer("Monster Legs");
            }

            if (layerIndex < 0)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] 투사체 피격 히트박스 레이어를 찾지 못했습니다. " +
                    "Project Settings > Tags and Layers에서 Monster Legs 레이어를 확인하세요.",
                    this);
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

            entityManager.SpawnDamageText(textPosition, damage, isCritical);
        }

        protected virtual void ResetRuntimeState()
        {
            currentHealth = maxHealth;
            isAlive = true;
            removalNotified = false;
            deathCoroutine = null;

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

            ApplyDefaultMaterial();
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

        private void NotifyRemovedOnce()
        {
            if (removalNotified)
            {
                return;
            }

            removalNotified = true;
            ownerSpawner?.NotifySpecialMonsterRemoved(this);
        }

        protected virtual void OnDisable()
        {
            ApplyDefaultMaterial();
            NotifyRemovedOnce();
        }

        protected virtual void OnDestroy()
        {
            ApplyDefaultMaterial();
            NotifyRemovedOnce();
        }
    }
}
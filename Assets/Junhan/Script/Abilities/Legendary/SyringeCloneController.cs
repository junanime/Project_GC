using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SyringeCloneController : MonoBehaviour
    {
        private SyringeAugmentVfx augmentVisual;
        private float visualReadyAt;
        private Character sourceCharacter;
        private EntityManager entityManager;
        private SyringeDartAbility sourceSyringeAbility;
        private PlayerGeneralStatRuntime statRuntime;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private CircleCollider2D hitCollider;

        private int projectilePoolIndex;
        private float fireTimer = 0f;

        private LayerMask monsterLayer;
        private GameObject projectilePrefab;

        [Header("Clone Follow")]
        [SerializeField] private float followOffsetDistance = 1.2f;
        [SerializeField] private float followLerpSpeed = 12f;

        [Header("Clone Safety")]
        [SerializeField] private float spawnInvincibleDuration = 2f;

        private float spawnInvincibleTimer = 0f;

        public static SyringeCloneController Create(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceSyringeAbility)
        {
            if (sourceCharacter == null || entityManager == null || sourceSyringeAbility == null)
            {
                Debug.LogWarning("[분신] 생성 실패: sourceCharacter/entityManager/sourceSyringeAbility 중 하나가 없습니다.");
                return null;
            }

            GameObject cloneObject = new GameObject("Syringe Clone");

            SpriteRenderer sourceRenderer = sourceCharacter.GetComponentInChildren<SpriteRenderer>();
            SpriteRenderer cloneRenderer = cloneObject.AddComponent<SpriteRenderer>();

            if (sourceRenderer != null)
            {
                cloneRenderer.sprite = sourceRenderer.sprite;
                cloneRenderer.sortingLayerID = sourceRenderer.sortingLayerID;
                cloneRenderer.sortingOrder = sourceRenderer.sortingOrder - 1;
                cloneRenderer.color = new Color(1f, 1f, 1f, 0.75f);
            }

            Rigidbody2D cloneRb = cloneObject.AddComponent<Rigidbody2D>();
            cloneRb.gravityScale = 0f;
            cloneRb.drag = 0f;
            cloneRb.angularDrag = 0f;
            cloneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
            cloneRb.bodyType = RigidbodyType2D.Kinematic;

            CircleCollider2D cloneCollider = cloneObject.AddComponent<CircleCollider2D>();
            cloneCollider.isTrigger = true;
            cloneCollider.radius = 0.3f;

            SyringeCloneController controller = cloneObject.AddComponent<SyringeCloneController>();

            controller.Init(
                sourceCharacter,
                entityManager,
                sourceSyringeAbility,
                cloneRenderer,
                cloneRb,
                cloneCollider
            );

            return controller;
        }

        private void Init(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceSyringeAbility,
            SpriteRenderer spriteRenderer,
            Rigidbody2D rb,
            CircleCollider2D hitCollider)
        {
            this.sourceCharacter = sourceCharacter;
            this.entityManager = entityManager;
            this.sourceSyringeAbility = sourceSyringeAbility;
            this.spriteRenderer = spriteRenderer;
            this.rb = rb;
            this.hitCollider = hitCollider;

            statRuntime = PlayerGeneralStatRuntime.GetOrCreate(sourceCharacter);

            projectilePrefab = sourceSyringeAbility.ProjectilePrefab;
            monsterLayer = sourceSyringeAbility.MonsterLayer;
            projectilePoolIndex = entityManager.AddPoolForProjectile(projectilePrefab);

            transform.position = GetSourceCenterPosition() + Vector3.right * followOffsetDistance;

            spawnInvincibleTimer = spawnInvincibleDuration;
            visualReadyAt = Time.time + 0.24f;
            var spawn = SyringeAugmentVfx.Play("CloneSpawn", transform.position, spriteRenderer);
            if (spawn != null) spawn.BindTo(transform);

            if (sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.AddListener(DestroySelf);
            }
        }

        private void Update()
        {
            if (sourceCharacter == null || sourceSyringeAbility == null || entityManager == null)
            {
                Destroy(gameObject);
                return;
            }

            if (sourceCharacter.CurrentHealth <= 0f || !sourceCharacter.gameObject.activeInHierarchy)
            {
                DestroySelf();
                return;
            }
            if (augmentVisual == null && Time.time >= visualReadyAt)
                augmentVisual = SyringeAugmentVfx.Play("CloneCulture", transform.position, spriteRenderer);

            if (statRuntime == null)
            {
                statRuntime = PlayerGeneralStatRuntime.GetOrCreate(sourceCharacter);
            }

            if (spawnInvincibleTimer > 0f)
            {
                spawnInvincibleTimer -= Time.deltaTime;
            }

            UpdateFollowPosition();
            UpdateVisual();
            UpdateAttack();
        }

        private Vector3 GetSourceCenterPosition()
        {
            if (sourceCharacter == null)
            {
                return transform.position;
            }

            if (sourceCharacter.CenterTransform != null)
            {
                return sourceCharacter.CenterTransform.position;
            }

            return sourceCharacter.transform.position;
        }

        private void UpdateFollowPosition()
        {
            Vector2 lookDirection = sourceCharacter.LookDirection;

            if (lookDirection == Vector2.zero)
            {
                lookDirection = Vector2.right;
            }

            Vector2 sideDirection = new Vector2(-lookDirection.y, lookDirection.x);

            if (sideDirection == Vector2.zero)
            {
                sideDirection = Vector2.right;
            }

            Vector3 targetPosition =
                GetSourceCenterPosition() +
                (Vector3)(sideDirection.normalized * followOffsetDistance);

            transform.position = Vector3.Lerp(
                transform.position,
                targetPosition,
                followLerpSpeed * Time.deltaTime
            );

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void UpdateVisual()
        {
            SpriteRenderer sourceRenderer = sourceCharacter.GetComponentInChildren<SpriteRenderer>();

            if (sourceRenderer != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = sourceRenderer.sprite;
                spriteRenderer.flipX = sourceRenderer.flipX;

                if (spawnInvincibleTimer > 0f)
                {
                    spriteRenderer.color = new Color(1f, 1f, 1f, 0.45f);
                }
                else
                {
                    spriteRenderer.color = new Color(1f, 1f, 1f, 0.75f);
                }
            }
        }

        private void UpdateAttack()
        {
            float cloneAttackSpeedMultiplier =
                statRuntime != null ? statRuntime.CloneAttackSpeedMultiplier : 1f;

            float effectiveCooldown =
                sourceSyringeAbility.GetCloneCooldown() /
                Mathf.Max(0.1f, cloneAttackSpeedMultiplier);

            effectiveCooldown = Mathf.Max(0.05f, effectiveCooldown);

            fireTimer += Time.deltaTime;

            if (fireTimer >= effectiveCooldown)
            {
                fireTimer = Mathf.Repeat(fireTimer, effectiveCooldown);
                StartCoroutine(FireRoutine());
            }
        }

        private IEnumerator FireRoutine()
        {
            Vector2 baseDirection = sourceCharacter.LookDirection;

            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            int projectileCount = sourceSyringeAbility.GetCloneProjectileCount();

            float cloneDamageMultiplier =
                statRuntime != null ? statRuntime.CloneDamageMultiplier : 1f;

            float damage = sourceSyringeAbility.GetCloneDamage() * cloneDamageMultiplier;
            float knockback = sourceSyringeAbility.GetCloneKnockback();
            float projectileSpeed = sourceSyringeAbility.GetCloneSpeed();

            float projectileSizeMultiplier = sourceSyringeAbility.GetEffectiveProjectileSizeMultiplier();
            float rangeMultiplier = sourceSyringeAbility.GetEffectiveRangeMultiplier();

            SyringeSpecialRuntime currentRuntime = sourceSyringeAbility.GetCurrentSpecialRuntime();

            for (int i = 0; i < projectileCount; i++)
            {
                Vector2 spreadDirection = sourceSyringeAbility.GetSpreadDirection(
                    baseDirection,
                    i,
                    projectileCount
                );

                Projectile projectile = entityManager.SpawnProjectile(
                    projectilePoolIndex,
                    transform.position,
                    damage,
                    knockback,
                    projectileSpeed,
                    monsterLayer
                );

                if (projectile == null)
                {
                    continue;
                }

                // 핵심 수정:
                // 풀링된 투사체가 이전 크기를 들고 나오지 않도록 분신도 매번 명시적으로 크기를 세팅한다.
                projectile.transform.localScale = Vector3.one * projectileSizeMultiplier;
                projectile.maxDistance *= rangeMultiplier;

                if (projectile is SyringeProjectile syringeProjectile)
                {
                    syringeProjectile.ConfigureSpecials(currentRuntime);
                }
                else
                {
                    Debug.LogWarning(
                        $"[분신] Spawned projectile is '{projectile.GetType().Name}', not 'SyringeProjectile'. " +
                        "Projectile Prefab 연결을 다시 확인하세요."
                    );
                }

                projectile.OnHitDamageable.AddListener(sourceCharacter.OnDealDamage.Invoke);
                projectile.Launch(spreadDirection);

                yield return null;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 분신은 몬스터 접촉으로 사라지지 않는다.
            // 플레이어 사망 시에만 제거된다.
        }

        private void DestroySelf()
        {
            if (isActiveAndEnabled && augmentVisual != null)
                SyringeAugmentVfx.Play("CloneDisappear", transform.position, spriteRenderer);
            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }

        private void OnDisable() { SyringeAugmentVfx.ReleaseOwned(ref augmentVisual); }

        private void OnDestroy()
        {
            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            if (sourceCharacter != null && sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.RemoveListener(DestroySelf);
            }
        }
    }
}
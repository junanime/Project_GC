using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SyringeCloneController : MonoBehaviour
    {
        private Character sourceCharacter;
        private EntityManager entityManager;
        private SyringeDartAbility sourceSyringeAbility;

        private SpriteRenderer spriteRenderer;
        private Rigidbody2D rb;
        private CircleCollider2D hitCollider;

        private int projectilePoolIndex;
        private float fireTimer = 0f;
        private float fireCooldown = 1f;
        private float damage;
        private float knockback;
        private float projectileSpeed;
        private int projectileCount;
        private LayerMask monsterLayer;
        private GameObject projectilePrefab;
        private SyringeSpecialRuntime specialSnapshot;

        [SerializeField] private float followOffsetDistance = 1.2f;
        [SerializeField] private float followLerpSpeed = 12f;
        [SerializeField] private float spawnInvincibleDuration = 2f;

        private float spawnInvincibleTimer = 0f;

        public static SyringeCloneController Create(Character sourceCharacter, EntityManager entityManager, SyringeDartAbility sourceSyringeAbility)
        {
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
            controller.Init(sourceCharacter, entityManager, sourceSyringeAbility, cloneRenderer, cloneRb, cloneCollider);

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

            projectilePrefab = sourceSyringeAbility.ProjectilePrefab;
            monsterLayer = sourceSyringeAbility.MonsterLayer;
            damage = sourceSyringeAbility.GetCloneDamage();
            knockback = sourceSyringeAbility.GetCloneKnockback();
            projectileSpeed = sourceSyringeAbility.GetCloneSpeed();
            fireCooldown = sourceSyringeAbility.GetCloneCooldown();
            projectileCount = sourceSyringeAbility.GetCloneProjectileCount();
            specialSnapshot = sourceSyringeAbility.GetCurrentSpecialRuntime();

            projectilePoolIndex = entityManager.AddPoolForProjectile(projectilePrefab);

            transform.position = sourceCharacter.transform.position + Vector3.right * followOffsetDistance;
            spawnInvincibleTimer = spawnInvincibleDuration;

            sourceCharacter.OnDeath.AddListener(DestroySelf);
        }

        private void Update()
        {
            if (sourceCharacter == null)
            {
                Destroy(gameObject);
                return;
            }

            if (spawnInvincibleTimer > 0f)
            {
                spawnInvincibleTimer -= Time.deltaTime;
            }

            UpdateFollowPosition();
            UpdateVisual();
            UpdateAttack();
        }

        private void UpdateFollowPosition()
        {
            Vector2 lookDirection = sourceCharacter.LookDirection;
            Vector2 sideDirection = new Vector2(-lookDirection.y, lookDirection.x);

            if (sideDirection == Vector2.zero)
            {
                sideDirection = Vector2.right;
            }

            Vector3 targetPosition = sourceCharacter.transform.position + (Vector3)(sideDirection.normalized * followOffsetDistance);
            transform.position = Vector3.Lerp(transform.position, targetPosition, followLerpSpeed * Time.deltaTime);
        }

        private void UpdateVisual()
        {
            SpriteRenderer sourceRenderer = sourceCharacter.GetComponentInChildren<SpriteRenderer>();
            if (sourceRenderer != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = sourceRenderer.sprite;
                spriteRenderer.flipX = sourceRenderer.flipX;

                // 생성 직후 무적 상태일 때 약간 더 투명하게 표시
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
            fireTimer += Time.deltaTime;

            if (fireTimer >= fireCooldown)
            {
                fireTimer = Mathf.Repeat(fireTimer, fireCooldown);
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

            for (int i = 0; i < projectileCount; i++)
            {
                Vector2 spreadDirection = sourceSyringeAbility.GetSpreadDirection(baseDirection, i, projectileCount);

                Projectile projectile = entityManager.SpawnProjectile(
                    projectilePoolIndex,
                    transform.position,
                    damage,
                    knockback,
                    projectileSpeed,
                    monsterLayer
                );

                if (projectile is SyringeProjectile syringeProjectile)
                {
                    syringeProjectile.ConfigureSpecials(specialSnapshot);
                }

                projectile.OnHitDamageable.AddListener(sourceCharacter.OnDealDamage.Invoke);
                projectile.Launch(spreadDirection);

                yield return null;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (spawnInvincibleTimer > 0f)
            {
                return;
            }

            if ((monsterLayer & (1 << other.gameObject.layer)) != 0)
            {
                DestroySelf();
            }
        }

        private void DestroySelf()
        {
            if (gameObject != null)
            {
                Destroy(gameObject);
            }
        }
    }
}
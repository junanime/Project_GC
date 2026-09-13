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


        // =========================================================
        // Clone Follow
        // =========================================================

        [Header("Clone Follow")]
        [SerializeField] private float followOffsetDistance = 1.2f;
        [SerializeField] private float followLerpSpeed = 12f;


        // =========================================================
        // Clone Safety
        // =========================================================

        [Header("Clone Safety")]
        [SerializeField] private float spawnInvincibleDuration = 2f;

        private float spawnInvincibleTimer = 0f;


        // =========================================================
        // Create
        // =========================================================

        public static SyringeCloneController Create(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceSyringeAbility
        )
        {
            if (sourceCharacter == null ||
                entityManager == null ||
                sourceSyringeAbility == null)
            {
                Debug.LogWarning(
                    "[�н�] ���� ����: " +
                    "sourceCharacter/entityManager/sourceSyringeAbility �� �ϳ��� �����ϴ�."
                );

                return null;
            }


            GameObject cloneObject =
                new GameObject(
                    "Syringe Clone"
                );


            // =====================================================
            // Sprite
            // =====================================================

            SpriteRenderer sourceRenderer =
                sourceCharacter.GetComponentInChildren<SpriteRenderer>();


            SpriteRenderer cloneRenderer =
                cloneObject.AddComponent<SpriteRenderer>();


            if (sourceRenderer != null)
            {
                cloneRenderer.sprite =
                    sourceRenderer.sprite;


                cloneRenderer.sortingLayerID =
                    sourceRenderer.sortingLayerID;


                cloneRenderer.sortingOrder =
                    sourceRenderer.sortingOrder - 1;


                cloneRenderer.color =
                    new Color(
                        1f,
                        1f,
                        1f,
                        0.75f
                    );
            }


            // =====================================================
            // Rigidbody
            // =====================================================

            Rigidbody2D cloneRb =
                cloneObject.AddComponent<Rigidbody2D>();


            cloneRb.gravityScale = 0f;
            cloneRb.drag = 0f;
            cloneRb.angularDrag = 0f;

            cloneRb.constraints =
                RigidbodyConstraints2D.FreezeRotation;

            cloneRb.bodyType =
                RigidbodyType2D.Kinematic;


            // =====================================================
            // Collider
            // =====================================================

            CircleCollider2D cloneCollider =
                cloneObject.AddComponent<CircleCollider2D>();


            cloneCollider.isTrigger = true;
            cloneCollider.radius = 0.3f;


            // =====================================================
            // Controller
            // =====================================================

            SyringeCloneController controller =
                cloneObject.AddComponent<SyringeCloneController>();


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


        // =========================================================
        // Init
        // =========================================================

        private void Init(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceSyringeAbility,
            SpriteRenderer spriteRenderer,
            Rigidbody2D rb,
            CircleCollider2D hitCollider
        )
        {
            this.sourceCharacter =
                sourceCharacter;

            this.entityManager =
                entityManager;

            this.sourceSyringeAbility =
                sourceSyringeAbility;

            this.spriteRenderer =
                spriteRenderer;

            this.rb =
                rb;

            this.hitCollider =
                hitCollider;


            statRuntime =
                PlayerGeneralStatRuntime.GetOrCreate(
                    sourceCharacter
                );


            projectilePrefab =
                sourceSyringeAbility.ProjectilePrefab;


            monsterLayer =
                sourceSyringeAbility.MonsterLayer;


            projectilePoolIndex =
                entityManager.AddPoolForProjectile(
                    projectilePrefab
                );


            transform.position =
                GetSourceCenterPosition()
                +
                Vector3.right *
                followOffsetDistance;


            spawnInvincibleTimer =
                spawnInvincibleDuration;

            visualReadyAt = Time.time + 0.24f;
            var spawn = SyringeAugmentVfx.Play("CloneSpawn", transform.position, spriteRenderer);
            if (spawn != null) spawn.BindTo(transform);

            if (sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.AddListener(
                    DestroySelf
                );
            }
        }


        // =========================================================
        // Update
        // =========================================================

        private void Update()
        {
            if (sourceCharacter == null ||
                sourceSyringeAbility == null ||
                entityManager == null)
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
                statRuntime =
                    PlayerGeneralStatRuntime.GetOrCreate(
                        sourceCharacter
                    );
            }


            if (spawnInvincibleTimer > 0f)
            {
                spawnInvincibleTimer -=
                    Time.deltaTime;
            }


            UpdateFollowPosition();

            UpdateVisual();

            UpdateAttack();
        }


        // =========================================================
        // Source Position
        // =========================================================

        private Vector3 GetSourceCenterPosition()
        {
            if (sourceCharacter == null)
            {
                return transform.position;
            }


            if (sourceCharacter.CenterTransform != null)
            {
                return sourceCharacter
                    .CenterTransform
                    .position;
            }


            return sourceCharacter
                .transform
                .position;
        }


        // =========================================================
        // Follow
        // =========================================================

        private void UpdateFollowPosition()
        {
            Vector2 lookDirection =
                sourceCharacter.LookDirection;


            if (lookDirection == Vector2.zero)
            {
                lookDirection =
                    Vector2.right;
            }


            Vector2 sideDirection =
                new Vector2(
                    -lookDirection.y,
                    lookDirection.x
                );


            if (sideDirection == Vector2.zero)
            {
                sideDirection =
                    Vector2.right;
            }


            Vector3 targetPosition =
                GetSourceCenterPosition()
                +
                (Vector3)(
                    sideDirection.normalized *
                    followOffsetDistance
                );


            transform.position =
                Vector3.Lerp(
                    transform.position,
                    targetPosition,
                    followLerpSpeed *
                    Time.deltaTime
                );


            if (rb != null)
            {
                rb.velocity =
                    Vector2.zero;

                rb.angularVelocity =
                    0f;
            }
        }


        // =========================================================
        // Visual
        // =========================================================

        private void UpdateVisual()
        {
            SpriteRenderer sourceRenderer =
                sourceCharacter
                    .GetComponentInChildren<SpriteRenderer>();


            if (sourceRenderer != null &&
                spriteRenderer != null)
            {
                spriteRenderer.sprite =
                    sourceRenderer.sprite;


                spriteRenderer.flipX =
                    sourceRenderer.flipX;


                if (spawnInvincibleTimer > 0f)
                {
                    spriteRenderer.color =
                        new Color(
                            1f,
                            1f,
                            1f,
                            0.45f
                        );
                }
                else
                {
                    spriteRenderer.color =
                        new Color(
                            1f,
                            1f,
                            1f,
                            0.75f
                        );
                }
            }
        }


        // =========================================================
        // Attack
        // =========================================================

        private void UpdateAttack()
        {
            float cloneAttackSpeedMultiplier =
                statRuntime != null
                    ? statRuntime.CloneAttackSpeedMultiplier
                    : 1f;


            float effectiveCooldown =
                sourceSyringeAbility.GetCloneCooldown()
                /
                Mathf.Max(
                    0.1f,
                    cloneAttackSpeedMultiplier
                );


            effectiveCooldown =
                Mathf.Max(
                    0.05f,
                    effectiveCooldown
                );


            fireTimer +=
                Time.deltaTime;


            if (fireTimer >= effectiveCooldown)
            {
                fireTimer =
                    Mathf.Repeat(
                        fireTimer,
                        effectiveCooldown
                    );


                StartCoroutine(
                    FireRoutine()
                );
            }
        }


        // =========================================================
        // Fire Routine
        // =========================================================

        private IEnumerator FireRoutine()
        {
            Vector2 baseDirection =
                sourceCharacter.LookDirection;


            if (baseDirection == Vector2.zero)
            {
                baseDirection =
                    Vector2.right;
            }


            int projectileCount =
                sourceSyringeAbility
                    .GetCloneProjectileCount();


            float cloneDamageMultiplier =
                statRuntime != null
                    ? statRuntime.CloneDamageMultiplier
                    : 1f;


            float damage =
                sourceSyringeAbility
                    .GetCloneDamage()
                *
                cloneDamageMultiplier;


            float knockback =
                sourceSyringeAbility
                    .GetCloneKnockback();


            float projectileSpeed =
                sourceSyringeAbility
                    .GetCloneSpeed();


            float projectileSizeMultiplier =
                sourceSyringeAbility
                    .GetEffectiveProjectileSizeMultiplier();


            float rangeMultiplier =
                sourceSyringeAbility
                    .GetEffectiveRangeMultiplier();


            SyringeSpecialRuntime currentRuntime =
                sourceSyringeAbility
                    .GetCurrentSpecialRuntime();


            for (
                int i = 0;
                i < projectileCount;
                i++
            )
            {
                Vector2 spreadDirection =
                    sourceSyringeAbility
                        .GetSpreadDirection(
                            baseDirection,
                            i,
                            projectileCount
                        );


                Projectile projectile =
                    entityManager.SpawnProjectile(
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


                // =================================================
                // Projectile Size
                // =================================================
                //
                // Ǯ���� ����ü�� ���� ũ�⸦
                // ��� ������ �ʵ���
                // �нŵ� �Ź� ũ�⸦ ���������� ����
                // =================================================

                projectile.transform.localScale =
                    Vector3.one *
                    projectileSizeMultiplier;


                // =================================================
                // Range
                // =================================================

                projectile.maxDistance *=
                    rangeMultiplier;


                // =================================================
                // Syringe Specials
                // =================================================

                if (projectile is SyringeProjectile syringeProjectile)
                {
                    syringeProjectile.ConfigureSpecials(
                        currentRuntime
                    );
                }
                else
                {
                    Debug.LogWarning(
                        $"[�н�] Spawned projectile is " +
                        $"'{projectile.GetType().Name}', " +
                        $"not 'SyringeProjectile'. " +
                        "Projectile Prefab ������ �ٽ� Ȯ���ϼ���."
                    );
                }


                // =================================================
                // Damage Report
                // =================================================
                //
                // �н��� ������ ������ �������� ��
                // ���� ���ط��� ReportCloneDamage()�� ����
                // =================================================

                projectile.OnHitDamageable.AddListener(
                    ReportCloneDamage
                );


                // =================================================
                // Launch
                // =================================================

                projectile.Launch(
                    spreadDirection
                );


                yield return null;
            }
        }


        // =========================================================
        // Clone Damage Report
        // =========================================================

        private void ReportCloneDamage(
            float dealtDamage
        )
        {
            if (dealtDamage <= 0f)
            {
                return;
            }


            // =====================================================
            // ���� ��ü ���ط� �ý��� ����
            // =====================================================

            if (sourceCharacter != null &&
                sourceCharacter.OnDealDamage != null)
            {
                sourceCharacter
                    .OnDealDamage
                    .Invoke(
                        dealtDamage
                    );
            }


            // =====================================================
            // ������ ���ط�
            // =====================================================

            if (AugmentDamageTracker.Instance != null)
            {
                AugmentDamageTracker.Instance
                    .RecordDamage(
                        "�нŹ��",
                        dealtDamage
                    );
            }
        }


        // =========================================================
        // Trigger
        // =========================================================

        private void OnTriggerEnter2D(
            Collider2D other
        )
        {
            // �н��� ���� �������� ������� �ʴ´�.
            // �÷��̾� ��� �ÿ��� ���ŵȴ�.
        }


        // =========================================================
        // Destroy
        // =========================================================

        private void DestroySelf()
        {
            if (isActiveAndEnabled && augmentVisual != null)
                SyringeAugmentVfx.Play("CloneDisappear", transform.position, spriteRenderer);
            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            if (gameObject != null)
            {
                Destroy(
                    gameObject
                );
            }
        }

        private void OnDisable() { SyringeAugmentVfx.ReleaseOwned(ref augmentVisual); }

        private void OnDestroy()
        {
            SyringeAugmentVfx.ReleaseOwned(ref augmentVisual);
            if (sourceCharacter != null && sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.RemoveListener(
                    DestroySelf
                );
            }
        }
    }
}
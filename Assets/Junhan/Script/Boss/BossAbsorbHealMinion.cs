using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    public class BossAbsorbHealMinion : IDamageable
    {
        [Header("HP")]
        [Tooltip("흡수 몬스터의 최대 체력입니다. 패턴 스크립트에서 설정한 HP 값으로 런타임에 덮어씌워집니다.")]
        [SerializeField] private float maxHp = 20f;

        [Tooltip("흡수 몬스터의 현재 체력입니다. 런타임 확인용입니다.")]
        [SerializeField] private float currentHp = 20f;

        [Header("Movement")]
        [Tooltip("흡수 몬스터가 보스를 향해 이동하는 속도입니다. 패턴 스크립트에서 설정한 이동 속도 값으로 런타임에 덮어씌워집니다.")]
        [SerializeField] private float moveSpeed = 2.5f;

        [Tooltip("보스 중심과 이 거리 이하로 가까워지면 흡수된 것으로 처리합니다.")]
        [SerializeField] private float absorbDistance = 0.9f;

        [Tooltip("체크하면 이동 방향에 따라 스프라이트 좌우를 뒤집습니다.")]
        [SerializeField] private bool flipSpriteByMoveDirection = true;

        [Header("Heal")]
        [Tooltip("이 몬스터가 보스에게 흡수될 때 회복시키는 체력입니다. 패턴 스크립트에서 설정한 값으로 런타임에 덮어씌워집니다.")]
        [SerializeField] private float healAmountOnAbsorb = 10f;

        [Header("References")]
        [Tooltip("흡수 몬스터의 스프라이트 렌더러입니다. 비워두면 자식 오브젝트에서 자동으로 찾습니다.")]
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("흡수 몬스터의 피격 판정 콜라이더입니다. 가능하면 자식 Hitbox 오브젝트의 Collider2D를 넣으세요.")]
        [SerializeField] private Collider2D hitbox;

        [Tooltip("흡수 몬스터의 Rigidbody2D입니다. 비워두면 현재 오브젝트에서 자동으로 찾거나 추가합니다.")]
        [SerializeField] private Rigidbody2D rb;

        [Header("Projectile Hit Detection")]
        [Tooltip("체크하면 투사체가 이 몬스터를 targetLayer로 인식하지 못해도, 몬스터 쪽에서 직접 투사체 충돌을 감지해 데미지를 받습니다.")]
        [SerializeField] private bool acceptProjectileTriggerDamage = true;

        [Tooltip("체크하면 이 몬스터를 맞춘 플레이어 투사체를 즉시 사라지게 합니다.")]
        [SerializeField] private bool consumeProjectileOnHit = true;

        [Tooltip("투사체에서 데미지 값을 읽지 못했을 때 사용할 기본 데미지입니다.")]
        [SerializeField] private float fallbackProjectileDamage = 1f;

        [Tooltip("흡수 몬스터가 투사체로 받을 데미지 배율입니다. 1이면 투사체 데미지를 그대로 받습니다.")]
        [SerializeField] private float projectileDamageMultiplier = 1f;

        [Header("Hitbox Auto Setup")]
        [Tooltip("체크하면 Awake에서 자식 Collider2D를 우선으로 찾아 Hitbox에 자동 연결합니다.")]
        [SerializeField] private bool autoFindChildHitbox = true;

        [Tooltip("체크하면 Hitbox 자식 오브젝트에 BossAbsorbHealMinionHitboxForwarder를 자동으로 붙입니다.")]
        [SerializeField] private bool autoAddHitboxForwarder = true;

        [Header("Hit Flash")]
        [Tooltip("피격 시 잠깐 바꿀 흰색 머티리얼입니다. 없어도 동작합니다.")]
        [SerializeField] private Material whiteMaterial;

        [Tooltip("기본 머티리얼입니다. 비워두면 시작 시 SpriteRenderer의 현재 머티리얼을 자동 저장합니다.")]
        [SerializeField] private Material defaultMaterial;

        [Tooltip("피격 시 흰색으로 깜빡이는 시간입니다.")]
        [SerializeField] private float hitFlashSeconds = 0.08f;

        [Header("VFX")]
        [Tooltip("플레이어에게 처치되었을 때 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject deathVfxPrefab;

        [Tooltip("보스에게 흡수될 때 몬스터 위치에 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject absorbVfxPrefab;

        [Tooltip("보스가 회복될 때 보스 위치에 생성할 이펙트 프리팹입니다.")]
        [SerializeField] private GameObject bossHealVfxPrefab;

        [Header("Debug")]
        [Tooltip("체크하면 흡수 몬스터 이동/피격/흡수 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private BossAbsorbMinionHealPattern ownerPattern;
        private BossController targetBoss;
        private Coroutine hitFlashCoroutine;
        private bool isFinished = false;

        private readonly HashSet<int> processedProjectileIds = new HashSet<int>();

        public bool IsFinished => isFinished;

        private void Awake()
        {
            ResolveReferences();
            SetupPhysics();
            SetupHitboxForwarder();
        }

        private void Update()
        {
            MoveToBossAndCheckAbsorb();
        }

        public void Setup(
            BossAbsorbMinionHealPattern owner,
            BossController boss,
            float hp,
            float speed,
            float healAmount,
            float finalAbsorbDistance,
            GameObject deathVfxOverride,
            GameObject absorbVfxOverride,
            GameObject bossHealVfxOverride,
            bool enableDebugLog)
        {
            ownerPattern = owner;
            targetBoss = boss;

            maxHp = Mathf.Max(1f, hp);
            currentHp = maxHp;
            moveSpeed = Mathf.Max(0f, speed);
            healAmountOnAbsorb = Mathf.Max(0f, healAmount);
            absorbDistance = Mathf.Max(0.05f, finalAbsorbDistance);

            deathVfxPrefab = deathVfxOverride != null ? deathVfxOverride : deathVfxPrefab;
            absorbVfxPrefab = absorbVfxOverride != null ? absorbVfxOverride : absorbVfxPrefab;
            bossHealVfxPrefab = bossHealVfxOverride != null ? bossHealVfxOverride : bossHealVfxPrefab;

            debugLog = enableDebugLog;
            isFinished = false;
            processedProjectileIds.Clear();

            ResolveReferences();
            SetupPhysics();
            SetupHitboxForwarder();
            ShowVisualAndCollision();

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbHealMinion] 생성 완료 / HP={currentHp}, speed={moveSpeed}, heal={healAmountOnAbsorb}");
            }
        }

        private void MoveToBossAndCheckAbsorb()
        {
            if (isFinished)
            {
                return;
            }

            if (targetBoss == null || targetBoss.IsDead)
            {
                FinishWithoutHeal(false);
                return;
            }

            Vector2 currentPosition = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 targetPosition = targetBoss.BossCenterPosition;
            Vector2 toBoss = targetPosition - currentPosition;
            float distance = toBoss.magnitude;

            if (distance <= absorbDistance)
            {
                AbsorbIntoBoss();
                return;
            }

            if (distance <= 0.001f)
            {
                return;
            }

            Vector2 direction = toBoss.normalized;
            Vector2 nextPosition = Vector2.MoveTowards(
                currentPosition,
                targetPosition,
                moveSpeed * Time.deltaTime
            );

            if (rb != null)
            {
                rb.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            if (flipSpriteByMoveDirection && spriteRenderer != null && Mathf.Abs(direction.x) > 0.01f)
            {
                spriteRenderer.flipX = direction.x < 0f;
            }
        }

        private void AbsorbIntoBoss()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            HideVisualAndCollision();

            if (absorbVfxPrefab != null)
            {
                Instantiate(absorbVfxPrefab, transform.position, Quaternion.identity);
            }

            bool healed = BossHealingRuntimeBridge.TryHealBoss(
                targetBoss,
                healAmountOnAbsorb,
                out float beforeHp,
                out float afterHp,
                out float maxHpValue
            );

            if (healed && bossHealVfxPrefab != null && targetBoss != null)
            {
                Instantiate(bossHealVfxPrefab, targetBoss.BossCenterPosition, Quaternion.identity);
            }

            if (debugLog)
            {
                if (healed)
                {
                    Debug.Log($"[BossAbsorbHealMinion] 보스 흡수 회복 / {beforeHp:0.##} → {afterHp:0.##} / max={maxHpValue:0.##}");
                }
                else
                {
                    Debug.Log("[BossAbsorbHealMinion] 보스 흡수 시도 / 회복 실패");
                }
            }

            ownerPattern?.NotifyMinionAbsorbed(this);
            Destroy(gameObject);
        }

        public override void TakeDamage(
    float damage,
    Vector2 knockback = default(Vector2),
    bool isCritical = false)
        {
            if (isFinished)
            {
                return;
            }

            if (damage <= 0f)
            {
                return;
            }

            currentHp -= damage;

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbHealMinion] 피격 / damage={damage}, hp={currentHp}/{maxHp}");
            }

            if (currentHp <= 0f)
            {
                DieByPlayer();
                return;
            }

            PlayHitFlash();
        }

        public override void Knockback(Vector2 knockback)
        {
            // 흡수 몬스터는 보스에게 끌려가는 연출을 유지하기 위해 넉백을 받지 않는다.
        }

        private void DieByPlayer()
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            HideVisualAndCollision();

            if (deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            if (debugLog)
            {
                Debug.Log("[BossAbsorbHealMinion] 플레이어에게 처치됨");
            }

            ownerPattern?.NotifyMinionKilled(this);
            Destroy(gameObject);
        }

        private void FinishWithoutHeal(bool playDeathVfx)
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            HideVisualAndCollision();

            if (playDeathVfx && deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            if (debugLog)
            {
                Debug.Log("[BossAbsorbHealMinion] 회복 없이 제거됨");
            }

            ownerPattern?.NotifyMinionKilled(this);
            Destroy(gameObject);
        }

        public void ForceDespawn(bool playDeathVfx)
        {
            if (isFinished)
            {
                return;
            }

            isFinished = true;
            HideVisualAndCollision();

            if (playDeathVfx && deathVfxPrefab != null)
            {
                Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
            }

            if (debugLog)
            {
                Debug.Log("[BossAbsorbHealMinion] 강제 제거");
            }

            Destroy(gameObject);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            ProcessProjectileHit(other);
        }

        public void ProcessProjectileHit(Collider2D other)
        {
            if (!acceptProjectileTriggerDamage)
            {
                return;
            }

            if (isFinished || other == null)
            {
                return;
            }

            // 핵심 수정:
            // 흡수 몬스터는 플레이어의 침 투사체만 맞아야 한다.
            // SniperBulletProjectile 같은 적 탄환도 Projectile을 상속할 수 있으므로,
            // Projectile 전체를 허용하면 적 탄환이 흡수 몬스터를 대신 잡아버리는 문제가 생긴다.
            SyringeProjectile syringeProjectile = other.GetComponentInParent<SyringeProjectile>();

            if (syringeProjectile == null)
            {
                if (debugLog)
                {
                    Projectile ignoredProjectile = other.GetComponentInParent<Projectile>();

                    if (ignoredProjectile != null)
                    {
                        Debug.Log($"[BossAbsorbHealMinion] 플레이어 침이 아닌 투사체 무시 / projectile={ignoredProjectile.name}");
                    }
                }

                return;
            }

            Projectile projectile = syringeProjectile;

            if (IsProjectileDespawning(projectile))
            {
                return;
            }

            int projectileId = projectile.GetInstanceID();

            if (processedProjectileIds.Contains(projectileId))
            {
                return;
            }

            processedProjectileIds.Add(projectileId);

            float projectileDamage = GetFloatField(projectile, "damage", fallbackProjectileDamage);
            float projectileKnockback = GetFloatField(projectile, "knockback", 0f);
            Vector2 projectileDirection = GetVector2Field(projectile, "direction", Vector2.zero);

            float finalDamage = Mathf.Max(0f, projectileDamage) * Mathf.Max(0f, projectileDamageMultiplier);
            Vector2 finalKnockback = projectileKnockback * projectileDirection;

            if (debugLog)
            {
                Debug.Log($"[BossAbsorbHealMinion] 플레이어 침 직접 감지 / projectile={projectile.name}, damage={finalDamage}");
            }

            TakeDamage(finalDamage, finalKnockback, false);

            if (consumeProjectileOnHit)
            {
                TryConsumeProjectile(projectile);
            }
        }

        private void ResolveReferences()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (hitbox == null)
            {
                if (autoFindChildHitbox)
                {
                    Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

                    foreach (Collider2D candidate in colliders)
                    {
                        if (candidate != null && candidate.gameObject != gameObject)
                        {
                            hitbox = candidate;
                            break;
                        }
                    }
                }

                if (hitbox == null)
                {
                    hitbox = GetComponent<Collider2D>();
                }
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody2D>();
            }

            if (spriteRenderer != null && defaultMaterial == null)
            {
                defaultMaterial = spriteRenderer.sharedMaterial;
            }
        }

        private void SetupPhysics()
        {
            if (hitbox != null)
            {
                hitbox.isTrigger = true;
            }

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void SetupHitboxForwarder()
        {
            if (!autoAddHitboxForwarder || hitbox == null)
            {
                return;
            }

            BossAbsorbHealMinionHitboxForwarder forwarder =
                hitbox.GetComponent<BossAbsorbHealMinionHitboxForwarder>();

            if (forwarder == null)
            {
                forwarder = hitbox.gameObject.AddComponent<BossAbsorbHealMinionHitboxForwarder>();
            }

            forwarder.Init(this);
        }

        private void ShowVisualAndCollision()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

            foreach (Collider2D collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = true;
                    collider.isTrigger = true;
                }
            }

            if (spriteRenderer != null && defaultMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultMaterial;
            }
        }

        private void HideVisualAndCollision()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = null;
            }

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                if (renderer != null)
                {
                    renderer.enabled = false;
                }
            }

            Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);

            foreach (Collider2D collider in colliders)
            {
                if (collider != null)
                {
                    collider.enabled = false;
                }
            }

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private float nextVisibleHitTime;
        private void PlayHitFlash()
        {
            if (SuppressHitFlash || Time.time < nextVisibleHitTime) return;
            nextVisibleHitTime = Time.time + .22f;
            if (spriteRenderer == null || whiteMaterial == null || defaultMaterial == null)
            {
                return;
            }

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(hitFlashCoroutine);
            }

            hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.sharedMaterial = whiteMaterial;

            yield return new WaitForSeconds(hitFlashSeconds);

            if (!isFinished && spriteRenderer != null && defaultMaterial != null)
            {
                spriteRenderer.sharedMaterial = defaultMaterial;
            }

            hitFlashCoroutine = null;
        }

        private static bool IsProjectileDespawning(Projectile projectile)
        {
            return GetBoolField(projectile, "isDespawning", false);
        }

        private static void TryConsumeProjectile(Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            MethodInfo destroyMethod = FindMethod(projectile.GetType(), "DestroyProjectile");

            if (destroyMethod != null)
            {
                destroyMethod.Invoke(projectile, null);
                return;
            }

            projectile.gameObject.SetActive(false);
        }

        private static float GetFloatField(object target, string fieldName, float fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            FieldInfo field = FindField(target.GetType(), fieldName);

            if (field == null || field.FieldType != typeof(float))
            {
                return fallback;
            }

            return (float)field.GetValue(target);
        }

        private static bool GetBoolField(object target, string fieldName, bool fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            FieldInfo field = FindField(target.GetType(), fieldName);

            if (field == null || field.FieldType != typeof(bool))
            {
                return fallback;
            }

            return (bool)field.GetValue(target);
        }

        private static Vector2 GetVector2Field(object target, string fieldName, Vector2 fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            FieldInfo field = FindField(target.GetType(), fieldName);

            if (field == null || field.FieldType != typeof(Vector2))
            {
                return fallback;
            }

            return (Vector2)field.GetValue(target);
        }

        private static FieldInfo FindField(System.Type type, string fieldName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            System.Type currentType = type;

            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(fieldName, flags);

                if (field != null)
                {
                    return field;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethod(System.Type type, string methodName)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            System.Type currentType = type;

            while (currentType != null)
            {
                MethodInfo method = currentType.GetMethod(methodName, flags);

                if (method != null)
                {
                    return method;
                }

                currentType = currentType.BaseType;
            }

            return null;
        }
    }

    public class BossAbsorbHealMinionHitboxForwarder : MonoBehaviour
    {
        private BossAbsorbHealMinion owner;

        public void Init(BossAbsorbHealMinion minion)
        {
            owner = minion;
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner = GetComponentInParent<BossAbsorbHealMinion>();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (owner == null)
            {
                owner = GetComponentInParent<BossAbsorbHealMinion>();
            }

            if (owner != null)
            {
                owner.ProcessProjectileHit(other);
            }
        }
    }
}
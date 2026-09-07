using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피의 카페인 주입기 파괴 오브젝트입니다.
    ///
    /// BossHealColaBottle과 동일한 방식으로:
    /// - IDamageable 상속
    /// - 플레이어 Projectile 직접 감지
    /// - 자식 Hitbox Forwarder 지원
    /// - 구조물형 오브젝트라 Knockback 무시
    ///
    /// BossCaffeineInjectorPattern이 생성/수명/성공 판정을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class BossCaffeineInjector : IDamageable
    {
        [Header("HP")]

        [Tooltip(
            "카페인 주입기 최대 체력입니다. " +
            "Pattern의 Injector HP 값으로 런타임에 덮어씌워집니다."
        )]
        [SerializeField, Min(1f)]
        private float maxHp = 60f;

        [Tooltip("현재 카페인 주입기 체력입니다. 런타임 확인용입니다.")]
        [SerializeField]
        private float currentHp = 60f;

        [Header("References")]

        [Tooltip(
            "주입기 SpriteRenderer입니다. " +
            "비워 두면 자식에서 자동 탐색합니다."
        )]
        [SerializeField]
        private SpriteRenderer spriteRenderer;

        [Tooltip(
            "주입기 피격 Collider2D입니다. " +
            "가능하면 자식 Hitbox 오브젝트의 Collider2D를 넣으세요."
        )]
        [SerializeField]
        private Collider2D hitbox;

        [Tooltip(
            "주입기 Rigidbody2D입니다. " +
            "비워 두면 현재 오브젝트에서 찾거나 자동 추가합니다."
        )]
        [SerializeField]
        private Rigidbody2D rb;

        [Header("Projectile Hit Detection")]

        [Tooltip(
            "체크하면 SyringeProjectile의 targetLayer 직접 판정과 별개로 " +
            "주입기 쪽에서 플레이어 Projectile 충돌을 감지해 피해를 받습니다."
        )]
        [SerializeField]
        private bool acceptProjectileTriggerDamage = true;

        [Tooltip(
            "체크하면 주입기를 맞춘 플레이어 투사체를 즉시 소모합니다. " +
            "테스트 단계에서는 ON을 권장합니다."
        )]
        [SerializeField]
        private bool consumeProjectileOnHit = true;

        [Tooltip(
            "Projectile 내부 damage 값을 읽지 못했을 때 사용할 기본 피해입니다."
        )]
        [SerializeField, Min(0f)]
        private float fallbackProjectileDamage = 1f;

        [Tooltip(
            "주입기가 Projectile로 받을 피해 배율입니다. " +
            "1이면 원래 투사체 피해 그대로 받습니다."
        )]
        [SerializeField, Min(0f)]
        private float projectileDamageMultiplier = 1f;

        [Header("Hitbox Auto Setup")]

        [Tooltip(
            "체크하면 Awake에서 자식 Collider2D를 우선 탐색해 Hitbox에 자동 연결합니다."
        )]
        [SerializeField]
        private bool autoFindChildHitbox = true;

        [Tooltip(
            "체크하면 Hitbox 자식에 BossCaffeineInjectorHitboxForwarder를 자동 추가합니다."
        )]
        [SerializeField]
        private bool autoAddHitboxForwarder = true;

        [Header("Hit Flash")]

        [Tooltip("피격 순간 사용할 흰색 머티리얼입니다. 없어도 기능은 동작합니다.")]
        [SerializeField]
        private Material whiteMaterial;

        [Tooltip(
            "주입기 기본 머티리얼입니다. " +
            "비워 두면 시작 시 SpriteRenderer의 현재 머티리얼을 저장합니다."
        )]
        [SerializeField]
        private Material defaultMaterial;

        [Tooltip("피격 Flash 지속 시간입니다.")]
        [SerializeField, Min(0f)]
        private float hitFlashSeconds = 0.08f;

        [Header("VFX")]

        [Tooltip(
            "주입기 파괴 시 생성할 기본 이펙트입니다. " +
            "Pattern에서 Destroy VFX를 지정하면 Pattern 값이 우선합니다."
        )]
        [SerializeField]
        private GameObject destroyVfxPrefab;

        [Header("Debug")]

        [Tooltip("주입기 생성/피격/파괴 로그를 출력합니다.")]
        [SerializeField]
        private bool debugLog = false;

        private BossCaffeineInjectorPattern ownerPattern;
        private Coroutine hitFlashCoroutine;
        private bool isDestroyed;
        private GameObject runtimeDestroyVfxPrefab;

        private readonly HashSet<int> processedProjectileIds =
            new HashSet<int>();

        public bool IsDestroyed => isDestroyed;
        public float CurrentHp => currentHp;
        public float MaxHp => maxHp;

        private void Awake()
        {
            ResolveReferences();
            SetupPhysics();
            SetupHitboxForwarder();
        }

        /// <summary>
        /// Pattern 생성 직후 호출합니다.
        /// </summary>
        public void Setup(
            BossCaffeineInjectorPattern owner,
            float hp,
            GameObject destroyVfxOverride,
            bool enableDebugLog)
        {
            ownerPattern = owner;

            maxHp =
                Mathf.Max(
                    1f,
                    hp);

            currentHp =
                maxHp;

            isDestroyed =
                false;

            debugLog =
                enableDebugLog;

            processedProjectileIds.Clear();

            runtimeDestroyVfxPrefab =
                destroyVfxOverride != null
                    ? destroyVfxOverride
                    : destroyVfxPrefab;

            ResolveReferences();
            SetupPhysics();
            SetupHitboxForwarder();
            ShowVisualAndCollision();

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineInjector] 생성 완료 | HP={currentHp:0.##}/{maxHp:0.##}",
                    this
                );
            }
        }

        private void ResolveReferences()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer =
                    GetComponentInChildren
                    <
                        SpriteRenderer
                    >(true);
            }

            if (hitbox == null)
            {
                if (autoFindChildHitbox)
                {
                    Collider2D[] colliders =
                        GetComponentsInChildren
                        <
                            Collider2D
                        >(true);

                    for (int i = 0;
                         i < colliders.Length;
                         i++)
                    {
                        Collider2D candidate =
                            colliders[i];

                        if (candidate != null &&
                            candidate.gameObject != gameObject)
                        {
                            hitbox =
                                candidate;

                            break;
                        }
                    }
                }

                if (hitbox == null)
                {
                    hitbox =
                        GetComponent<Collider2D>();
                }
            }

            if (rb == null)
            {
                rb =
                    GetComponent<Rigidbody2D>();
            }

            if (rb == null)
            {
                rb =
                    gameObject.AddComponent
                    <
                        Rigidbody2D
                    >();
            }

            if (spriteRenderer != null &&
                defaultMaterial == null)
            {
                defaultMaterial =
                    spriteRenderer.sharedMaterial;
            }
        }

        private void SetupPhysics()
        {
            if (hitbox != null)
            {
                hitbox.isTrigger =
                    true;
            }

            if (rb != null)
            {
                rb.gravityScale =
                    0f;

                rb.bodyType =
                    RigidbodyType2D.Kinematic;

                rb.velocity =
                    Vector2.zero;

                rb.angularVelocity =
                    0f;
            }
        }

        private void SetupHitboxForwarder()
        {
            if (!autoAddHitboxForwarder ||
                hitbox == null)
            {
                return;
            }

            BossCaffeineInjectorHitboxForwarder forwarder =
                hitbox.GetComponent
                <
                    BossCaffeineInjectorHitboxForwarder
                >();

            if (forwarder == null)
            {
                forwarder =
                    hitbox.gameObject.AddComponent
                    <
                        BossCaffeineInjectorHitboxForwarder
                    >();
            }

            forwarder.Init(
                this);
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            ProcessProjectileHit(
                other);
        }

        /// <summary>
        /// Hitbox Forwarder에서도 호출합니다.
        /// </summary>
        public void ProcessProjectileHit(
            Collider2D other)
        {
            if (!acceptProjectileTriggerDamage ||
                isDestroyed ||
                other == null)
            {
                return;
            }

            Projectile projectile =
                other.GetComponentInParent
                <
                    Projectile
                >();

            if (projectile == null)
            {
                return;
            }

            if (IsProjectileDespawning(
                    projectile))
            {
                return;
            }

            int projectileId =
                projectile.GetInstanceID();

            if (!processedProjectileIds.Add(
                    projectileId))
            {
                return;
            }

            float projectileDamage =
                GetFloatField(
                    projectile,
                    "damage",
                    fallbackProjectileDamage);

            float projectileKnockback =
                GetFloatField(
                    projectile,
                    "knockback",
                    0f);

            Vector2 projectileDirection =
                GetVector2Field(
                    projectile,
                    "direction",
                    Vector2.zero);

            float finalDamage =
                Mathf.Max(
                    0f,
                    projectileDamage) *
                Mathf.Max(
                    0f,
                    projectileDamageMultiplier);

            Vector2 finalKnockback =
                projectileKnockback *
                projectileDirection;

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineInjector] Projectile 피격 | " +
                    $"Projectile={projectile.name}, Damage={finalDamage:0.##}",
                    this
                );
            }

            TakeDamage(
                finalDamage,
                finalKnockback,
                false);

            if (consumeProjectileOnHit)
            {
                TryConsumeProjectile(
                    projectile);
            }
        }

        public override void TakeDamage(
            float damage,
            Vector2 knockback = default(Vector2),
            bool isCritical = false)
        {
            if (isDestroyed ||
                damage <= 0f)
            {
                return;
            }

            currentHp =
                Mathf.Max(
                    0f,
                    currentHp -
                    damage);

            if (debugLog)
            {
                Debug.Log(
                    $"[BossCaffeineInjector] 피격 | " +
                    $"Damage={damage:0.##}, HP={currentHp:0.##}/{maxHp:0.##}",
                    this
                );
            }

            if (currentHp <= 0f)
            {
                DestroyInjector();
                return;
            }

            PlayHitFlash();
        }

        public override void Knockback(
            Vector2 knockback)
        {
            // 보스 몸에 꽂힌 구조물형 오브젝트라 넉백을 받지 않습니다.
        }

        /// <summary>
        /// Pattern 실패/종료 시 보상 판정 없이 강제 제거할 때 사용합니다.
        /// </summary>
        public void ForceDestroy()
        {
            if (isDestroyed)
            {
                return;
            }

            isDestroyed =
                true;

            HideVisualAndCollision();

            Destroy(
                gameObject);
        }

        private void DestroyInjector()
        {
            if (isDestroyed)
            {
                return;
            }

            isDestroyed =
                true;

            HideVisualAndCollision();

            if (runtimeDestroyVfxPrefab != null)
            {
                Instantiate(
                    runtimeDestroyVfxPrefab,
                    transform.position,
                    Quaternion.identity);
            }

            if (debugLog)
            {
                Debug.Log(
                    "[BossCaffeineInjector] 파괴 완료",
                    this
                );
            }

            ownerPattern?.NotifyInjectorDestroyed(
                this);

            Destroy(
                gameObject);
        }

        private void ShowVisualAndCollision()
        {
            SpriteRenderer[] renderers =
                GetComponentsInChildren
                <
                    SpriteRenderer
                >(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled =
                        true;
                }
            }

            Collider2D[] colliders =
                GetComponentsInChildren
                <
                    Collider2D
                >(true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                Collider2D collider =
                    colliders[i];

                if (collider != null)
                {
                    collider.enabled =
                        true;

                    collider.isTrigger =
                        true;
                }
            }

            if (spriteRenderer != null &&
                defaultMaterial != null)
            {
                spriteRenderer.sharedMaterial =
                    defaultMaterial;
            }
        }

        private void HideVisualAndCollision()
        {
            if (hitFlashCoroutine != null)
            {
                StopCoroutine(
                    hitFlashCoroutine);

                hitFlashCoroutine =
                    null;
            }

            SpriteRenderer[] renderers =
                GetComponentsInChildren
                <
                    SpriteRenderer
                >(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                if (renderers[i] != null)
                {
                    renderers[i].enabled =
                        false;
                }
            }

            Collider2D[] colliders =
                GetComponentsInChildren
                <
                    Collider2D
                >(true);

            for (int i = 0;
                 i < colliders.Length;
                 i++)
            {
                if (colliders[i] != null)
                {
                    colliders[i].enabled =
                        false;
                }
            }

            if (rb != null)
            {
                rb.velocity =
                    Vector2.zero;

                rb.angularVelocity =
                    0f;
            }
        }

        private void PlayHitFlash()
        {
            if (spriteRenderer == null ||
                whiteMaterial == null ||
                defaultMaterial == null)
            {
                return;
            }

            if (hitFlashCoroutine != null)
            {
                StopCoroutine(
                    hitFlashCoroutine);
            }

            hitFlashCoroutine =
                StartCoroutine(
                    HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            spriteRenderer.sharedMaterial =
                whiteMaterial;

            yield return
                new WaitForSeconds(
                    hitFlashSeconds);

            if (!isDestroyed &&
                spriteRenderer != null &&
                defaultMaterial != null)
            {
                spriteRenderer.sharedMaterial =
                    defaultMaterial;
            }

            hitFlashCoroutine =
                null;
        }

        private static bool IsProjectileDespawning(
            Projectile projectile)
        {
            return
                GetBoolField(
                    projectile,
                    "isDespawning",
                    false);
        }

        private static void TryConsumeProjectile(
            Projectile projectile)
        {
            if (projectile == null)
            {
                return;
            }

            MethodInfo destroyMethod =
                FindMethod(
                    projectile.GetType(),
                    "DestroyProjectile");

            if (destroyMethod != null)
            {
                destroyMethod.Invoke(
                    projectile,
                    null);

                return;
            }

            projectile.gameObject.SetActive(
                false);
        }

        private static float GetFloatField(
            object target,
            string fieldName,
            float fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            FieldInfo field =
                FindField(
                    target.GetType(),
                    fieldName);

            if (field == null ||
                field.FieldType !=
                typeof(float))
            {
                return fallback;
            }

            return
                (float)field.GetValue(
                    target);
        }

        private static bool GetBoolField(
            object target,
            string fieldName,
            bool fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            FieldInfo field =
                FindField(
                    target.GetType(),
                    fieldName);

            if (field == null ||
                field.FieldType !=
                typeof(bool))
            {
                return fallback;
            }

            return
                (bool)field.GetValue(
                    target);
        }

        private static Vector2 GetVector2Field(
            object target,
            string fieldName,
            Vector2 fallback)
        {
            if (target == null)
            {
                return fallback;
            }

            FieldInfo field =
                FindField(
                    target.GetType(),
                    fieldName);

            if (field == null ||
                field.FieldType !=
                typeof(Vector2))
            {
                return fallback;
            }

            return
                (Vector2)field.GetValue(
                    target);
        }

        private static FieldInfo FindField(
            System.Type type,
            string fieldName)
        {
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            System.Type currentType =
                type;

            while (currentType != null)
            {
                FieldInfo field =
                    currentType.GetField(
                        fieldName,
                        flags);

                if (field != null)
                {
                    return field;
                }

                currentType =
                    currentType.BaseType;
            }

            return null;
        }

        private static MethodInfo FindMethod(
            System.Type type,
            string methodName)
        {
            const BindingFlags flags =
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic;

            System.Type currentType =
                type;

            while (currentType != null)
            {
                MethodInfo method =
                    currentType.GetMethod(
                        methodName,
                        flags);

                if (method != null)
                {
                    return method;
                }

                currentType =
                    currentType.BaseType;
            }

            return null;
        }
    }

    /// <summary>
    /// 자식 Hitbox의 Trigger를 루트 BossCaffeineInjector로 전달합니다.
    /// </summary>
    public sealed class BossCaffeineInjectorHitboxForwarder :
        MonoBehaviour
    {
        private BossCaffeineInjector owner;

        public void Init(
            BossCaffeineInjector injector)
        {
            owner =
                injector;
        }

        private void Awake()
        {
            if (owner == null)
            {
                owner =
                    GetComponentInParent
                    <
                        BossCaffeineInjector
                    >();
            }
        }

        private void OnTriggerEnter2D(
            Collider2D other)
        {
            if (owner == null)
            {
                owner =
                    GetComponentInParent
                    <
                        BossCaffeineInjector
                    >();
            }

            owner?.ProcessProjectileHit(
                other);
        }
    }
}

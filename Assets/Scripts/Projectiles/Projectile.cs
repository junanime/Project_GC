using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    public class Projectile : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("투사체의 실제 스프라이트를 표시하는 SpriteRenderer입니다.")]
        [SerializeField] protected SpriteRenderer projectileSpriteRenderer;

        [Header("Movement Settings")]
        [Tooltip("투사체가 이동할 수 있는 최대 거리입니다. 이 거리를 넘으면 자동으로 사라집니다.")]
        [SerializeField] public float maxDistance;

        [Tooltip("투사체가 날아가는 동안 회전하는 속도입니다. 오이처럼 방향을 고정해서 날아가야 하는 탄환은 0으로 두세요.")]
        [SerializeField] protected float rotationSpeed = 0;

        [Tooltip("투사체가 날아가는 동안 속도가 줄어드는 정도입니다. 오이 저격 탄환처럼 직선으로 빠르게 날아가야 하면 0으로 두세요.")]
        [SerializeField] protected float airResistance = 0;

        [Header("Launch Direction Visual Settings")]
        [Tooltip("체크하면 투사체 이미지가 발사 방향을 바라보도록 자동 회전합니다. 오이 저격 탄환은 체크하세요.")]
        [SerializeField] private bool alignVisualToLaunchDirection = false;

        [Tooltip("스프라이트의 기본 앞 방향 보정 각도입니다. 이미지가 오른쪽을 바라보면 0, 왼쪽을 바라보면 180, 위쪽을 바라보면 -90, 아래쪽을 바라보면 90을 넣으세요.")]
        [SerializeField] private float visualAngleOffset = 0f;

        [Tooltip("오브젝트 풀에서 재사용될 때 이전 회전값을 초기화합니다. 대부분 켜두는 것이 안전합니다.")]
        [SerializeField] private bool resetRotationOnSetup = true;

        [Tooltip("이동 중에도 계속 방향 회전을 다시 맞춥니다. 일반 직선 탄환은 꺼도 됩니다.")]
        [SerializeField] private bool keepVisualAlignedWhileMoving = false;

        [Header("Effects")]
        [Tooltip("투사체가 사라질 때 재생할 파티클입니다. 없으면 비워둬도 됩니다.")]
        [SerializeField] protected ParticleSystem destructionParticleSystem;

        [Header("Critical Settings")]
        [Tooltip("치명타가 발생했을 때 데미지 배율입니다.")]
        [SerializeField] protected float criticalDamageMultiplier = 2f;

        protected float despawnTime = 1;
        protected LayerMask targetLayer;
        protected float speed;
        protected float damage;
        protected float knockback;

        protected EntityManager entityManager;
        protected Character playerCharacter;
        protected Collider2D col;
        protected ZPositioner zPositioner;
        protected Coroutine moveCoroutine;
        protected int projectileIndex;
        protected Vector2 direction;
        protected TrailRenderer trailRenderer = null;
        protected bool isDespawning = false;

        private Quaternion initialRotation;

        public UnityEvent<float> OnHitDamageable { get; private set; }

        protected virtual void Awake()
        {
            initialRotation = transform.rotation;

            col = GetComponent<Collider2D>();
            zPositioner = gameObject.AddComponent<ZPositioner>();
            TryGetComponent<TrailRenderer>(out trailRenderer);
        }

        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            if (zPositioner != null && playerCharacter != null)
            {
                zPositioner.Init(playerCharacter.transform);
            }
        }

        public virtual void Setup(
            int projectileIndex,
            Vector2 position,
            float damage,
            float knockback,
            float speed,
            LayerMask targetLayer)
        {
            transform.position = position;

            if (resetRotationOnSetup)
            {
                transform.rotation = initialRotation;
            }

            trailRenderer?.Clear();

            this.projectileIndex = projectileIndex;
            this.damage = damage;
            this.knockback = knockback;
            this.speed = speed;
            this.targetLayer = targetLayer;

            isDespawning = false;

            if (projectileSpriteRenderer != null)
            {
                projectileSpriteRenderer.gameObject.SetActive(true);
            }

            if (col != null)
            {
                col.enabled = true;
            }

            OnHitDamageable = new UnityEvent<float>();
        }

        public virtual void Launch(Vector2 direction)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                this.direction = Vector2.right;
            }
            else
            {
                this.direction = direction.normalized;
            }

            ApplyVisualRotationToDirection();

            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
            }

            moveCoroutine = StartCoroutine(Move());
        }

        public virtual IEnumerator Move()
        {
            float distanceTravelled = 0;
            float timeOffScreen = 0;

            while (distanceTravelled < maxDistance && timeOffScreen < despawnTime && speed > 0)
            {
                float step = speed * Time.deltaTime;

                if (keepVisualAlignedWhileMoving)
                {
                    ApplyVisualRotationToDirection();
                }

                transform.position += step * (Vector3)direction;
                distanceTravelled += step;

                if (Mathf.Abs(rotationSpeed) > 0.001f)
                {
                    transform.RotateAround(
                        transform.position,
                        Vector3.back,
                        Time.deltaTime * 100 * rotationSpeed
                    );
                }

                speed -= airResistance * Time.deltaTime;

                yield return null;
            }

            HitNothing();
        }

        private void ApplyVisualRotationToDirection()
        {
            if (!alignVisualToLaunchDirection)
            {
                return;
            }

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + visualAngleOffset);
        }

        protected virtual void HitDamageable(IDamageable damageable)
        {
            if (isDespawning)
            {
                return;
            }

            float finalDamage = damage;
            bool isCritical = false;

            if (playerCharacter != null && Random.value < playerCharacter.CritChance)
            {
                isCritical = true;
                finalDamage *= criticalDamageMultiplier;
                Debug.Log($"<color=red>[치명타]</color> Critical Hit! Damage: {finalDamage}");
            }

            damageable.TakeDamage(finalDamage, knockback * direction, isCritical);
            OnHitDamageable?.Invoke(finalDamage);

            DestroyProjectile();
        }

        protected virtual void HitNothing()
        {
            if (isDespawning)
            {
                return;
            }

            DestroyProjectile();
        }

        protected virtual void DestroyProjectile()
        {
            if (isDespawning)
            {
                return;
            }

            isDespawning = true;

            if (col != null)
            {
                col.enabled = false;
            }

            if (moveCoroutine != null)
            {
                StopCoroutine(moveCoroutine);
                moveCoroutine = null;
            }

            if (!gameObject.activeInHierarchy)
            {
                entityManager.DespawnProjectile(projectileIndex, this);
                return;
            }

            StartCoroutine(DestroyProjectileAnimation());
        }

        protected IEnumerator DestroyProjectileAnimation()
        {
            if (projectileSpriteRenderer != null)
            {
                projectileSpriteRenderer.gameObject.SetActive(false);
            }

            if (destructionParticleSystem != null)
            {
                destructionParticleSystem.Play();
                yield return new WaitForSeconds(destructionParticleSystem.main.duration);
            }
            else
            {
                yield return null;
            }

            if (projectileSpriteRenderer != null)
            {
                projectileSpriteRenderer.gameObject.SetActive(true);
            }

            entityManager.DespawnProjectile(projectileIndex, this);
        }

        protected void CollisionCheck(Collider2D collider)
        {
            if (isDespawning)
            {
                return;
            }

            if ((targetLayer & (1 << collider.gameObject.layer)) != 0)
            {
                Transform parent = collider.transform.parent;

                if (parent != null && parent.TryGetComponent<IDamageable>(out IDamageable damageable))
                {
                    HitDamageable(damageable);
                }
                else if (collider.TryGetComponent<IDamageable>(out IDamageable directDamageable))
                {
                    HitDamageable(directDamageable);
                }
                else
                {
                    HitNothing();
                }
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D collider)
        {
            CollisionCheck(collider);
        }
    }
}
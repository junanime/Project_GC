using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] protected SpriteRenderer projectileSpriteRenderer;
        [SerializeField] protected float maxDistance;
        [SerializeField] protected float rotationSpeed = 0;
        [SerializeField] protected float airResistance = 0;
        [SerializeField] protected ParticleSystem destructionParticleSystem;

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

        public UnityEvent<float> OnHitDamageable { get; private set; }

        protected virtual void Awake()
        {
            col = GetComponent<Collider2D>();
            zPositioner = gameObject.AddComponent<ZPositioner>();
            TryGetComponent<TrailRenderer>(out trailRenderer);
        }

        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform);
        }

        public virtual void Setup(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            transform.position = position;
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
            this.direction = direction.normalized;
            moveCoroutine = StartCoroutine(Move());
        }

        public virtual IEnumerator Move()
        {
            float distanceTravelled = 0;
            float timeOffScreen = 0;

            while (distanceTravelled < maxDistance && timeOffScreen < despawnTime && speed > 0)
            {
                float step = speed * Time.deltaTime;
                transform.position += step * (Vector3)direction;
                distanceTravelled += step;

                transform.RotateAround(transform.position, Vector3.back, Time.deltaTime * 100 * rotationSpeed);

                speed -= airResistance * Time.deltaTime;
                yield return null;
            }

            HitNothing();
        }

        protected virtual void HitDamageable(IDamageable damageable)
        {
            if (isDespawning)
            {
                return;
            }

            damageable.TakeDamage(damage, knockback * direction);
            OnHitDamageable?.Invoke(damage);
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
                if (collider.transform.parent.TryGetComponent<IDamageable>(out IDamageable damageable))
                {
                    HitDamageable(collider.gameObject.GetComponentInParent<IDamageable>());
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
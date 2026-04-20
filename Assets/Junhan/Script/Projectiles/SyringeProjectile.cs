using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class SyringeProjectile : Projectile
    {
        private SyringeSpecialRuntime specials;
        private int remainingPierces;
        private readonly HashSet<int> hitTargetIds = new HashSet<int>();

        public override void Setup(int projectileIndex, Vector2 position, float damage, float knockback, float speed, LayerMask targetLayer)
        {
            base.Setup(projectileIndex, position, damage, knockback, speed, targetLayer);

            specials = default;
            remainingPierces = 0;
            hitTargetIds.Clear();
        }

        public void ConfigureSpecials(SyringeSpecialRuntime runtime)
        {
            specials = runtime;
            remainingPierces = runtime.pierceCount;
        }

        public override IEnumerator Move()
        {
            float distanceTravelled = 0;
            float timeOffScreen = 0;

            while (distanceTravelled < maxDistance && timeOffScreen < despawnTime && speed > 0)
            {
                if (specials.homingEnabled)
                {
                    UpdateHomingDirection();
                }

                float step = speed * Time.deltaTime;
                transform.position += step * (Vector3)direction;
                distanceTravelled += step;
                transform.RotateAround(transform.position, Vector3.back, Time.deltaTime * 100 * rotationSpeed);
                speed -= airResistance * Time.deltaTime;

                yield return null;
            }

            HitNothing();
        }

        private void UpdateHomingDirection()
        {
            Transform target = FindClosestTarget();

            if (target == null)
            {
                return;
            }

            Vector2 desiredDirection = ((Vector2)target.position - (Vector2)transform.position).normalized;
            direction = Vector2.Lerp(direction, desiredDirection, specials.homingLerpSpeed * Time.deltaTime).normalized;
        }

        private Transform FindClosestTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, specials.homingRange, targetLayer);

            float closestDistance = float.MaxValue;
            Transform closestTarget = null;

            foreach (Collider2D hit in hits)
            {
                IDamageable damageable = hit.GetComponentInParent<IDamageable>();
                Component damageableComponent = damageable as Component;

                if (damageableComponent == null)
                {
                    continue;
                }

                int targetId = damageableComponent.gameObject.GetInstanceID();
                if (hitTargetIds.Contains(targetId))
                {
                    continue;
                }

                float distance = Vector2.Distance(transform.position, damageableComponent.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = damageableComponent.transform;
                }
            }

            return closestTarget;
        }

        protected override void OnTriggerEnter2D(Collider2D collider)
        {
            if (isDespawning || !gameObject.activeInHierarchy)
            {
                return;
            }

            if ((targetLayer & (1 << collider.gameObject.layer)) == 0)
            {
                return;
            }

            IDamageable damageable = collider.GetComponentInParent<IDamageable>();
            Component damageableComponent = damageable as Component;

            if (damageable == null || damageableComponent == null)
            {
                HitNothing();
                return;
            }

            int targetId = damageableComponent.gameObject.GetInstanceID();
            if (hitTargetIds.Contains(targetId))
            {
                return;
            }

            hitTargetIds.Add(targetId);

            damageable.TakeDamage(damage, knockback * direction);
            OnHitDamageable?.Invoke(damage);

            if (specials.poisonEnabled)
            {
                ApplyPoison(damageableComponent);
            }

            if (specials.explosionEnabled)
            {
                ApplyExplosion(damageableComponent.gameObject);
            }

            bool canPierce = specials.pierceEnabled && remainingPierces > 0;

            if (canPierce)
            {
                remainingPierces--;

                if (col != null)
                {
                    StartCoroutine(ReenableColliderNextFrame());
                }

                return;
            }

            DestroyProjectile();
        }

        private IEnumerator ReenableColliderNextFrame()
        {
            if (col == null)
            {
                yield break;
            }

            col.enabled = false;
            yield return null;

            if (!isDespawning && gameObject.activeInHierarchy)
            {
                col.enabled = true;
            }
        }

        private void ApplyPoison(Component damageableComponent)
        {
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            PoisonStatus poisonStatus = monster.GetComponent<PoisonStatus>();
            if (poisonStatus == null)
            {
                poisonStatus = monster.gameObject.AddComponent<PoisonStatus>();
            }

            poisonStatus.Apply(
                specials.poisonDuration,
                specials.poisonTickInterval,
                specials.poisonTickDamage
            );
        }

        private void ApplyExplosion(GameObject originalTarget)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, specials.explosionRadius, targetLayer);
            HashSet<int> damagedIds = new HashSet<int>();

            foreach (Collider2D hit in hits)
            {
                IDamageable splashDamageable = hit.GetComponentInParent<IDamageable>();
                Component splashComponent = splashDamageable as Component;

                if (splashDamageable == null || splashComponent == null)
                {
                    continue;
                }

                int splashId = splashComponent.gameObject.GetInstanceID();

                if (originalTarget != null && splashId == originalTarget.GetInstanceID())
                {
                    continue;
                }

                if (damagedIds.Contains(splashId))
                {
                    continue;
                }

                damagedIds.Add(splashId);
                splashDamageable.TakeDamage(specials.explosionDamage, Vector2.zero);
            }
        }
    }
}
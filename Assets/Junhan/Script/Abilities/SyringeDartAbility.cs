using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SyringeDartAbility : ProjectileAbility
    {
        [Header("Syringe Dart Stats")]
        [SerializeField] protected UpgradeableProjectileCount projectileCount;
        [SerializeField] protected float syringeDelay = 0.08f;

        [Header("Active Special Augments")]
        [SerializeField] private bool poisonEnabled = false;
        [SerializeField] private bool explosionEnabled = false;
        [SerializeField] private bool homingEnabled = false;
        [SerializeField] private bool pierceEnabled = false;

        [Header("Poison Settings")]
        [SerializeField] private float poisonDuration = 3f;
        [SerializeField] private float poisonTickInterval = 0.5f;
        [SerializeField] private float poisonTickDamage = 2f;

        [Header("Explosion Settings")]
        [SerializeField] private float explosionRadius = 1.5f;
        [SerializeField] private float explosionDamage = 2f;

        [Header("Homing Settings")]
        [SerializeField] private float homingRange = 6f;
        [SerializeField] private float homingLerpSpeed = 8f;

        [Header("Pierce Settings")]
        [SerializeField] private int pierceCount = 1;

        protected override void Attack()
        {
            StartCoroutine(LaunchSyringes());
        }

        protected IEnumerator LaunchSyringes()
        {
            timeSinceLastAttack -= projectileCount.Value * syringeDelay;

            for (int i = 0; i < projectileCount.Value; i++)
            {
                LaunchSyringeProjectile(playerCharacter.LookDirection);
                yield return new WaitForSeconds(syringeDelay);
            }
        }

        private void LaunchSyringeProjectile(Vector2 direction)
        {
            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                playerCharacter.CenterTransform.position,
                damage.Value,
                knockback.Value,
                speed.Value,
                monsterLayer
            );

            if (projectile is SyringeProjectile syringeProjectile)
            {
                syringeProjectile.ConfigureSpecials(BuildSpecialRuntime());
            }
            else
            {
                Debug.LogWarning(
                    $"[SyringeDartAbility] Spawned projectile is '{projectile.GetType().Name}', not 'SyringeProjectile'. " +
                    "Projectile Prefab 연결을 다시 확인하세요."
                );
            }

            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
            projectile.Launch(direction);
        }

        private SyringeSpecialRuntime BuildSpecialRuntime()
        {
            SyringeSpecialRuntime runtime = new SyringeSpecialRuntime
            {
                poisonEnabled = poisonEnabled,
                poisonDuration = poisonDuration,
                poisonTickInterval = poisonTickInterval,
                poisonTickDamage = poisonTickDamage,

                explosionEnabled = explosionEnabled,
                explosionRadius = explosionRadius,
                explosionDamage = explosionDamage,

                homingEnabled = homingEnabled,
                homingRange = homingRange,
                homingLerpSpeed = homingLerpSpeed,

                pierceEnabled = pierceEnabled,
                pierceCount = pierceCount
            };

            return runtime;
        }

        public void EnablePoisonAugment()
        {
            poisonEnabled = true;
        }

        public void EnableExplosionAugment()
        {
            explosionEnabled = true;
        }

        public void EnableHomingAugment()
        {
            homingEnabled = true;
        }

        public void EnablePierceAugment()
        {
            pierceEnabled = true;
        }

        public bool HasPoisonAugment()
        {
            return poisonEnabled;
        }

        public bool HasExplosionAugment()
        {
            return explosionEnabled;
        }

        public bool HasHomingAugment()
        {
            return homingEnabled;
        }

        public bool HasPierceAugment()
        {
            return pierceEnabled;
        }
    }
}
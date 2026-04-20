using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SyringeDartAbility : ProjectileAbility
    {
        [Header("Syringe Dart Stats")]
        [SerializeField] protected UpgradeableProjectileCount projectileCount;
        [SerializeField] protected float syringeDelay = 0.08f;

        [Header("Spread Settings")]
        [SerializeField] private float angleBetweenProjectiles = 8f;
        [SerializeField] private float maxTotalSpreadAngle = 120f;

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

        [Header("Legendary - Life Burn")]
        [SerializeField] private bool lifeBurnEnabled = false;
        [SerializeField] private float lifeBurnDamageMultiplier = 3f;
        [SerializeField] private int lifeBurnBonusProjectiles = 10;
        [SerializeField] private float lifeBurnBonusRange = 3f;

        [Header("Legendary - Clone Culture")]
        [SerializeField] private bool cloneLegendaryTaken = false;

        public GameObject ProjectilePrefab => projectilePrefab;
        public LayerMask MonsterLayer => monsterLayer;

        protected override void Attack()
        {
            StartCoroutine(LaunchSyringes());
        }

        protected IEnumerator LaunchSyringes()
        {
            int totalProjectileCount = GetEffectiveProjectileCount();
            Vector2 baseDirection = playerCharacter.LookDirection;

            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            timeSinceLastAttack -= totalProjectileCount * syringeDelay;

            for (int i = 0; i < totalProjectileCount; i++)
            {
                Vector2 spreadDirection = GetSpreadDirection(baseDirection, i, totalProjectileCount);
                LaunchSyringeProjectile(spreadDirection);
                yield return new WaitForSeconds(syringeDelay);
            }
        }

        private void LaunchSyringeProjectile(Vector2 direction)
        {
            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                playerCharacter.CenterTransform.position,
                GetEffectiveDamage(),
                GetEffectiveKnockback(),
                GetEffectiveSpeed(),
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
                pierceCount = pierceCount,

                rangeBonus = lifeBurnEnabled ? lifeBurnBonusRange : 0f
            };

            return runtime;
        }

        public Vector2 GetSpreadDirection(Vector2 baseDirection, int projectileIndex, int totalCount)
        {
            if (baseDirection == Vector2.zero)
            {
                baseDirection = Vector2.right;
            }

            baseDirection.Normalize();

            if (totalCount <= 1)
            {
                return baseDirection;
            }

            float totalSpreadAngle = angleBetweenProjectiles * (totalCount - 1);
            totalSpreadAngle = Mathf.Min(totalSpreadAngle, maxTotalSpreadAngle);

            float actualAngleStep = totalSpreadAngle / (totalCount - 1);
            float startAngle = -totalSpreadAngle * 0.5f;
            float angleOffset = startAngle + (actualAngleStep * projectileIndex);

            return RotateVector(baseDirection, angleOffset);
        }

        private Vector2 RotateVector(Vector2 vector, float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos
            ).normalized;
        }

        public float GetEffectiveDamage()
        {
            float multiplier = lifeBurnEnabled ? lifeBurnDamageMultiplier : 1f;
            return damage.Value * multiplier;
        }

        public float GetEffectiveKnockback()
        {
            return knockback.Value;
        }

        public float GetEffectiveSpeed()
        {
            return speed.Value;
        }

        public float GetEffectiveCooldown()
        {
            return cooldown.Value;
        }

        public int GetEffectiveProjectileCount()
        {
            int totalCount = projectileCount.Value;

            if (lifeBurnEnabled)
            {
                totalCount += lifeBurnBonusProjectiles;
            }

            return Mathf.Max(1, totalCount);
        }

        public SyringeSpecialRuntime GetCurrentSpecialRuntime()
        {
            return BuildSpecialRuntime();
        }

        public float GetCloneDamage()
        {
            return GetEffectiveDamage() * 0.2f;
        }

        public float GetCloneKnockback()
        {
            return GetEffectiveKnockback() * 0.2f;
        }

        public float GetCloneSpeed()
        {
            return GetEffectiveSpeed();
        }

        public float GetCloneCooldown()
        {
            return GetEffectiveCooldown();
        }

        public int GetCloneProjectileCount()
        {
            return Mathf.Max(1, Mathf.FloorToInt(GetEffectiveProjectileCount() * 0.2f));
        }

        public void EnablePoisonAugment() => poisonEnabled = true;
        public void EnableExplosionAugment() => explosionEnabled = true;
        public void EnableHomingAugment() => homingEnabled = true;
        public void EnablePierceAugment() => pierceEnabled = true;

        public bool HasPoisonAugment() => poisonEnabled;
        public bool HasExplosionAugment() => explosionEnabled;
        public bool HasHomingAugment() => homingEnabled;
        public bool HasPierceAugment() => pierceEnabled;

        public void EnableLifeBurnLegendary() => lifeBurnEnabled = true;
        public bool HasLifeBurnLegendary() => lifeBurnEnabled;

        public void MarkCloneLegendaryTaken() => cloneLegendaryTaken = true;
        public bool HasCloneLegendary() => cloneLegendaryTaken;
    }
}
using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SyringeDartAbility : ProjectileAbility
    {
        [Header("Syringe Dart Stats")]
        [SerializeField] protected UpgradeableProjectileCount projectileCount;
        [SerializeField] protected float syringeDelay = 0.08f;

        protected override void Attack()
        {
            StartCoroutine(LaunchSyringes());
        }

        protected IEnumerator LaunchSyringes()
        {
            timeSinceLastAttack -= projectileCount.Value * syringeDelay;

            for (int i = 0; i < projectileCount.Value; i++)
            {
                LaunchProjectile(playerCharacter.LookDirection);
                yield return new WaitForSeconds(syringeDelay);
            }
        }

        protected void LaunchProjectile(Vector2 direction)
        {
            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                playerCharacter.CenterTransform.position,
                damage.Value,
                knockback.Value,
                speed.Value,
                monsterLayer
            );

            projectile.OnHitDamageable.AddListener(playerCharacter.OnDealDamage.Invoke);
            projectile.Launch(direction);
        }
    }
}
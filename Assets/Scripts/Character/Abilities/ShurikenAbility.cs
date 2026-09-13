using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class ShurikenAbility : ProjectileAbility
    {
        [Header("Shuriken Stats")]
        [SerializeField] protected UpgradeableProjectileCount projectileCount;
        [SerializeField] protected float shurikenDelay;


        // =========================================================
        // Attack
        // =========================================================

        protected override void Attack()
        {
            StartCoroutine(
                LuanchShurikens()
            );
        }


        // =========================================================
        // Launch Shurikens
        // =========================================================

        protected IEnumerator LuanchShurikens()
        {
            timeSinceLastAttack -=
                projectileCount.Value *
                shurikenDelay;


            for (int i = 0; i < projectileCount.Value; i++)
            {
                Vector2 direction =
                    playerCharacter != null
                        ? playerCharacter.LookDirection
                        : Vector2.right;


                if (direction.sqrMagnitude <= 0.0001f)
                {
                    direction = Vector2.right;
                }


                LaunchProjectile(
                    direction.normalized
                );


                yield return new WaitForSeconds(
                    shurikenDelay
                );
            }
        }


        // =========================================================
        // Launch Projectile
        // =========================================================

        protected void LaunchProjectile(
            Vector2 direction
        )
        {
            if (playerCharacter == null)
            {
                return;
            }


            Projectile projectile =
                entityManager.SpawnProjectile(
                    projectileIndex,
                    playerCharacter.CenterTransform.position,
                    damage.Value,
                    knockback.Value,
                    speed.Value,
                    monsterLayer
                );


            if (projectile == null)
            {
                Debug.LogWarning(
                    "[ShurikenAbility] Projectile 생성에 실패했습니다."
                );

                return;
            }


            // =====================================================
            // 피해량 기록
            // =====================================================
            //
            // 기존:
            //
            // projectile.OnHitDamageable.AddListener(
            //     playerCharacter.OnDealDamage.Invoke
            // );
            //
            //
            // 변경:
            //
            // ReportDamage()
            //
            // 1. 기존 StatsManager 총 피해량
            // 2. AugmentDamageTracker 표창 누적 피해량
            //
            // 을 동시에 처리
            // =====================================================

            projectile.OnHitDamageable.AddListener(
                ReportDamage
            );


            projectile.Launch(
                direction
            );
        }
    }
}
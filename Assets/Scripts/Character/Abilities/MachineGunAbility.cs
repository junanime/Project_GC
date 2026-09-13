using UnityEngine;

namespace Vampire
{
    public class MachineGunAbility : GunAbility
    {
        [Header("Machine Gun Stats")]
        [SerializeField] protected GameObject machineGun;
        [SerializeField] protected Transform launchTransform;
        [SerializeField] protected UpgradeableRotationSpeed rotationSpeed;
        [SerializeField] protected float gunRadius;

        protected Vector3 gunDirection = Vector2.right;


        // =========================================================
        // Update
        // =========================================================

        protected override void Update()
        {
            base.Update();


            if (playerCharacter == null ||
                machineGun == null)
            {
                return;
            }


            // 재장전 중 회전
            float reloadRotation = 0f;


            float safeCooldown =
                Mathf.Max(
                    0.01f,
                    cooldown.Value
                );


            float t =
                timeSinceLastAttack /
                safeCooldown;


            if (t > 0f && t < 1f)
            {
                reloadRotation =
                    t * 360f;
            }


            // 기관총 회전 방향
            float theta =
                Time.time *
                rotationSpeed.Value;


            gunDirection =
                new Vector3(
                    Mathf.Cos(theta),
                    Mathf.Sin(theta),
                    0f
                );


            // 기관총 위치
            machineGun.transform.position =
                playerCharacter.CenterTransform.position
                +
                gunDirection *
                gunRadius;


            // 기관총 회전
            machineGun.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    Mathf.Rad2Deg * theta -
                    reloadRotation
                );
        }


        // =========================================================
        // Launch Projectile
        // =========================================================

        protected override void LaunchProjectile()
        {
            if (launchTransform == null)
            {
                Debug.LogWarning(
                    "[MachineGunAbility] launchTransform이 연결되지 않았습니다."
                );

                return;
            }


            Projectile projectile =
                entityManager.SpawnProjectile(
                    projectileIndex,
                    launchTransform.position,
                    damage.Value,
                    knockback.Value,
                    speed.Value,
                    monsterLayer
                );


            if (projectile == null)
            {
                Debug.LogWarning(
                    "[MachineGunAbility] Projectile 생성에 실패했습니다."
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
            // 변경:
            //
            // ReportDamage()
            //
            // 1. 기존 StatsManager 총 피해량
            // 2. AugmentDamageTracker 기관총 누적 피해량
            //
            // 을 동시에 처리
            // =====================================================

            projectile.OnHitDamageable.AddListener(
                ReportDamage
            );


            // 발사
            projectile.Launch(
                gunDirection
            );
        }
    }
}
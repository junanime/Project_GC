using UnityEngine;

namespace Vampire
{
    public class GrenadeThrowableAbility : ThrowableAbility
    {
        [Header("Grenade Stats")]
        [SerializeField] protected UpgradeableProjectileCount fragmentCount;


        // =========================================================
        // Launch Throwable
        // =========================================================

        protected override void LaunchThrowable()
        {
            if (playerCharacter == null)
            {
                return;
            }


            // =====================================================
            // 수류탄 생성
            // =====================================================

            GrenadeThrowable throwable =
                entityManager.SpawnThrowable(
                    throwableIndex,
                    playerCharacter.CenterTransform.position,
                    damage.Value,
                    knockback.Value,
                    0,
                    monsterLayer
                ) as GrenadeThrowable;


            if (throwable == null)
            {
                Debug.LogWarning(
                    "[GrenadeThrowableAbility] GrenadeThrowable 생성에 실패했습니다."
                );

                return;
            }


            // =====================================================
            // 수류탄 파편 개수 설정
            // =====================================================

            throwable.SetupGrenade(
                fragmentCount.Value
            );


            // =====================================================
            // 투척 위치 설정
            // =====================================================

            Vector2 targetPosition =
                (Vector2)playerCharacter.transform.position
                +
                Random.insideUnitCircle *
                throwRadius;


            throwable.Throw(
                targetPosition
            );


            // =====================================================
            // 피해량 기록
            // =====================================================
            //
            // GrenadeThrowable 내부에서 생성되는
            // 각 파편 Projectile의 실제 피해량이
            //
            // GrenadeThrowable.OnHitDamageable
            //
            // 으로 전달됩니다.
            //
            // 그 피해를 ReportDamage()로 연결하여:
            //
            // 1. StatsManager 총 피해량
            // 2. AugmentDamageTracker 수류탄 누적 피해량
            //
            // 을 동시에 기록합니다.
            // =====================================================

            throwable.OnHitDamageable.AddListener(
                ReportDamage
            );
        }
    }
}
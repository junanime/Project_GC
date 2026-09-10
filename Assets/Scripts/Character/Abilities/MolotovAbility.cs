using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MolotovAbility : ThrowableAbility
    {
        [Header("Molotov Stats")]
        [SerializeField] protected UpgradeableDuration duration;
        [SerializeField] protected UpgradeableAOE fireRadius;
        [SerializeField] protected UpgradeableDamageRate fireDamageRate;


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
            // 화염병 생성
            // =====================================================

            MolotovThrowable throwable =
                entityManager.SpawnThrowable(
                    throwableIndex,
                    playerCharacter.CenterTransform.position,
                    damage.Value,
                    knockback.Value,
                    0,
                    monsterLayer
                ) as MolotovThrowable;


            if (throwable == null)
            {
                Debug.LogWarning(
                    "[MolotovAbility] MolotovThrowable 생성에 실패했습니다."
                );

                return;
            }


            // =====================================================
            // 화염 설정
            // =====================================================

            throwable.SetupFire(
                duration.Value,
                fireRadius.Value,
                fireDamageRate.Value
            );


            // =====================================================
            // 투척 위치 결정
            // =====================================================

            List<ISpatialHashGridClient> nearbyEnemies =
                entityManager.Grid.FindNearbyInRadius(
                    playerCharacter.transform.position,
                    throwRadius
                );


            Vector2 throwPosition;


            if (nearbyEnemies.Count > 0)
            {
                throwPosition =
                    nearbyEnemies[
                        Random.Range(
                            0,
                            nearbyEnemies.Count
                        )
                    ].Position;
            }
            else
            {
                throwPosition =
                    (Vector2)playerCharacter.transform.position
                    +
                    Random.insideUnitCircle *
                    throwRadius;
            }


            // =====================================================
            // 화염병 투척
            // =====================================================

            throwable.Throw(
                throwPosition
            );


            // =====================================================
            // 피해량 기록
            // =====================================================
            //
            // MolotovThrowable 내부의 지속 화염 피해가
            //
            // OnHitDamageable
            //
            // 이벤트로 전달됩니다.
            //
            // 여기서 ReportDamage()로 연결하면:
            //
            // 1. 기존 StatsManager 총 피해량
            // 2. AugmentDamageTracker 화염병 누적 피해량
            //
            // 을 동시에 기록합니다.
            // =====================================================

            throwable.OnHitDamageable.AddListener(
                ReportDamage
            );
        }
    }
}
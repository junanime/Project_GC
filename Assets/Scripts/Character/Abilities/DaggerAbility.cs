using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class DaggerAbility : StabAbility
    {
        [Header("Dagger Stats")]
        [SerializeField] protected UpgradeableBleedDamage bleedDamage;
        [SerializeField] protected UpgradeableBleedRate bleedRate;
        [SerializeField] protected UpgradeableBleedDuration bleedDuration;


        // =========================================================
        // Damage Monster
        // =========================================================

        protected override void DamageMonster(
            Monster monster,
            float damage,
            Vector2 knockback
        )
        {
            // 직접 타격 피해
            // StabAbility에서 처리
            base.DamageMonster(
                monster,
                damage,
                knockback
            );


            // 출혈 시작
            Coroutine monsterBleed =
                StartCoroutine(
                    BleedMonster(monster)
                );


            // 몬스터가 죽으면 출혈 Coroutine 중지
            monster.OnKilled.AddListener(
                delegate
                {
                    StopMonsterBleed(monsterBleed);
                }
            );
        }


        // =========================================================
        // Bleed
        // =========================================================

        protected IEnumerator BleedMonster(
            Monster monster
        )
        {
            // 0으로 나누는 문제 방지
            float safeBleedRate =
                Mathf.Max(0.01f, bleedRate.Value);


            float bleedDelay =
                1f / safeBleedRate;


            int bleedCount =
                Mathf.RoundToInt(
                    bleedDuration.Value *
                    safeBleedRate
                );


            for (int i = 0; i < bleedCount; i++)
            {
                yield return new WaitForSeconds(
                    bleedDelay
                );


                // 이미 죽었거나 사라진 경우 종료
                if (monster == null || monster.HP <= 0)
                {
                    break;
                }


                float finalBleedDamage =
                    bleedDamage.Value;


                // 실제 출혈 피해
                monster.TakeDamage(
                    finalBleedDamage
                );


                // =============================================
                // 피해 기록
                // =============================================
                //
                // 기존:
                //
                // playerCharacter.OnDealDamage.Invoke(
                //     bleedDamage.Value
                // );
                //
                // 변경:
                //
                // ReportDamage()를 통해
                //
                // 1. StatsManager 총 피해량
                // 2. AugmentDamageTracker의 단검 누적 피해량
                //
                // 을 동시에 기록
                // =============================================

                ReportDamage(
                    finalBleedDamage
                );


                if (monster.HP <= 0)
                {
                    break;
                }
            }
        }


        // =========================================================
        // Stop Bleed
        // =========================================================

        protected void StopMonsterBleed(
            Coroutine monsterBleed
        )
        {
            if (monsterBleed != null)
            {
                StopCoroutine(
                    monsterBleed
                );
            }
        }
    }
}
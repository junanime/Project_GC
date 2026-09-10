using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class ThrowableAbility : Ability
    {
        [Header("Throwable Stats")]
        [SerializeField] protected GameObject throwablePrefab;
        [SerializeField] protected LayerMask monsterLayer;
        [SerializeField] protected float throwRadius;

        [SerializeField] protected UpgradeableDamageRate throwRate;
        [SerializeField] protected UpgradeableDamage damage;
        [SerializeField] protected UpgradeableKnockback knockback;
        [SerializeField] protected UpgradeableWeaponCooldown cooldown;
        [SerializeField] protected UpgradeableProjectileCount throwableCount;

        protected float timeSinceLastAttack;
        protected int throwableIndex;


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            gameObject.SetActive(true);

            timeSinceLastAttack = cooldown.Value;

            throwableIndex =
                entityManager.AddPoolForThrowable(
                    throwablePrefab
                );
        }


        // =========================================================
        // Update
        // =========================================================

        private void Update()
        {
            timeSinceLastAttack += Time.deltaTime;

            if (timeSinceLastAttack >= cooldown.Value)
            {
                timeSinceLastAttack =
                    Mathf.Repeat(
                        timeSinceLastAttack,
                        cooldown.Value
                    );

                StartCoroutine(
                    Attack()
                );
            }
        }


        // =========================================================
        // Attack
        // =========================================================

        protected virtual IEnumerator Attack()
        {
            timeSinceLastAttack -=
                throwableCount.Value /
                throwRate.Value;


            for (int i = 0; i < throwableCount.Value; i++)
            {
                LaunchThrowable();

                yield return new WaitForSeconds(
                    1f / throwRate.Value
                );
            }
        }


        // =========================================================
        // Launch Throwable
        // =========================================================

        protected virtual void LaunchThrowable()
        {
            Throwable throwable =
                entityManager.SpawnThrowable(
                    throwableIndex,
                    playerCharacter.CenterTransform.position,
                    damage.Value,
                    knockback.Value,
                    0,
                    monsterLayer
                );


            if (throwable == null)
            {
                return;
            }


            Vector2 targetPosition =
                (Vector2)playerCharacter.transform.position +
                Random.insideUnitCircle * throwRadius;


            throwable.Throw(
                targetPosition
            );


            // =====================================================
            // 피해 기록
            // =====================================================
            //
            // 기존:
            //
            // throwable.OnHitDamageable.AddListener(
            //     playerCharacter.OnDealDamage.Invoke
            // );
            //
            // 변경:
            //
            // ReportDamage()가
            //
            // 1. Character.OnDealDamage
            //    → 기존 StatsManager 총 피해량 기록
            //
            // 2. AugmentDamageTracker
            //    → 해당 Ability의 누적 피해량 기록
            //
            // 을 동시에 처리합니다.
            // =====================================================

            throwable.OnHitDamageable.AddListener(
                ReportDamage
            );
        }
    }
}
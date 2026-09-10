using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BoomerangAbility : Ability
    {
        [Header("Boomerang Stats")]
        [SerializeField] protected GameObject boomerangPrefab;
        [SerializeField] protected LayerMask monsterLayer;
        [SerializeField] protected float throwRadius;
        [SerializeField] protected float throwTime = 1;

        [SerializeField] protected UpgradeableDamageRate throwRate;
        [SerializeField] protected UpgradeableDamage damage;
        [SerializeField] protected UpgradeableKnockback knockback;
        [SerializeField] protected UpgradeableWeaponCooldown cooldown;
        [SerializeField] protected UpgradeableProjectileCount boomerangCount;

        protected float timeSinceLastAttack;
        protected int boomerangIndex;


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            gameObject.SetActive(true);

            timeSinceLastAttack = cooldown.Value;

            boomerangIndex =
                entityManager.AddPoolForBoomerang(
                    boomerangPrefab
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
                boomerangCount.Value /
                throwRate.Value;


            for (int i = 0; i < boomerangCount.Value; i++)
            {
                ThrowBoomerang();

                yield return new WaitForSeconds(
                    1f / throwRate.Value
                );
            }
        }


        // =========================================================
        // Throw Boomerang
        // =========================================================

        protected virtual void ThrowBoomerang()
        {
            Boomerang boomerang =
                entityManager.SpawnBoomerang(
                    boomerangIndex,
                    playerCharacter.CenterTransform.position,
                    damage.Value,
                    knockback.Value,
                    throwRadius,
                    throwTime,
                    monsterLayer
                );


            if (boomerang == null)
            {
                return;
            }


            Vector2 throwPosition;


            // 주변 적 중 하나에게 랜덤 투척
            List<ISpatialHashGridClient> nearbyEnemies =
                entityManager.Grid.FindNearbyInRadius(
                    playerCharacter.transform.position,
                    throwRadius
                );


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
                    (Vector2)playerCharacter.transform.position +
                    Random.insideUnitCircle.normalized *
                    throwRadius;
            }


            boomerang.Throw(
                playerCharacter.transform,
                throwPosition
            );


            // =====================================================
            // 피해 기록
            // =====================================================
            //
            // 기존:
            //
            // boomerang.OnHitDamageable.AddListener(
            //     playerCharacter.OnDealDamage.Invoke
            // );
            //
            // 변경:
            //
            // ReportDamage()가
            //
            // 1. 기존 전체 피해량 기록
            // 2. AugmentDamageTracker 증강별 피해량 기록
            //
            // 을 동시에 처리합니다.
            // =====================================================

            boomerang.OnHitDamageable.AddListener(
                ReportDamage
            );
        }
    }
}
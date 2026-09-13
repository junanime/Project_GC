using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class StabAbility : MeleeAbility
    {
        [Header("Stab Stats")]
        [SerializeField] protected float stabOffset;
        [SerializeField] protected float stabDistance;
        [SerializeField] protected float stabTime;

        protected Vector2 weaponSize;
        protected FastList<GameObject> hitMonsters;


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            weaponSize = weaponSpriteRenderer.bounds.size;
            weaponSpriteRenderer.enabled = false;
        }


        // =========================================================
        // Attack
        // =========================================================

        protected override void Attack()
        {
            StartCoroutine(Stab());
        }


        // =========================================================
        // Stab
        // =========================================================

        protected virtual IEnumerator Stab()
        {
            hitMonsters = new FastList<GameObject>();

            timeSinceLastAttack -= stabTime;

            float t = 0f;

            weaponSpriteRenderer.enabled = true;

            Vector2 dir = playerCharacter.LookDirection;


            // =====================================================
            // 찌르기 진행
            // =====================================================

            while (t < stabTime)
            {
                Vector2 attackBoxPosition =
                    (Vector2)playerCharacter.CenterTransform.position +
                    dir *
                    (
                        weaponSize.x / 2f +
                        stabOffset +
                        stabDistance / stabTime * t
                    );


                float attackAngle =
                    Vector2.SignedAngle(
                        Vector2.right,
                        dir
                    );


                Collider2D[] hitColliders =
                    Physics2D.OverlapBoxAll(
                        attackBoxPosition,
                        weaponSize,
                        attackAngle,
                        targetLayer
                    );


                // 무기 위치
                weaponSpriteRenderer.transform.position =
                    attackBoxPosition;


                // 무기 방향
                weaponSpriteRenderer.transform.localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        attackAngle
                    );


                // =================================================
                // 몬스터 충돌 처리
                // =================================================

                foreach (Collider2D collider in hitColliders)
                {
                    if (hitMonsters.Contains(collider.gameObject))
                    {
                        continue;
                    }


                    Monster monster =
                        collider.gameObject
                            .GetComponentInParent<Monster>();


                    if (monster == null)
                    {
                        continue;
                    }


                    // 한 번 맞은 몬스터 등록
                    hitMonsters.Add(
                        collider.gameObject
                    );


                    float dealtDamage =
                        damage.Value;


                    // 실제 피해
                    DamageMonster(
                        monster,
                        dealtDamage,
                        dir * knockback.Value
                    );


                    // =============================================
                    // 피해량 기록
                    // =============================================
                    //
                    // 기존:
                    //
                    // playerCharacter.OnDealDamage.Invoke(
                    //     damage.Value
                    // );
                    //
                    // 변경:
                    //
                    // ReportDamage()
                    //
                    // 1. 기존 StatsManager 총 피해량
                    // 2. AugmentDamageTracker 증강별 피해량
                    //
                    // 동시 기록
                    // =============================================

                    ReportDamage(
                        dealtDamage
                    );
                }


                t += Time.deltaTime;

                yield return null;
            }


            // =====================================================
            // 찌르기 종료 애니메이션
            // =====================================================

            Vector2 initialScale =
                weaponSpriteRenderer.transform.localScale;


            t = 0f;


            while (t < 1f)
            {
                weaponSpriteRenderer.transform.localPosition =
                    (Vector2)playerCharacter.CenterTransform.position +
                    dir *
                    (
                        weaponSpriteRenderer.transform.localScale.x /
                        initialScale.x *
                        weaponSize.x /
                        2f +
                        stabOffset +
                        stabDistance
                    );


                weaponSpriteRenderer.transform.localScale =
                    Vector2.Lerp(
                        initialScale,
                        Vector2.zero,
                        EasingUtils.EaseInQuart(t)
                    );


                t += Time.deltaTime * 4f;

                yield return null;
            }


            weaponSpriteRenderer.transform.localScale =
                initialScale;

            weaponSpriteRenderer.enabled =
                false;
        }


        // =========================================================
        // Damage Monster
        // =========================================================

        protected virtual void DamageMonster(
            Monster monster,
            float damage,
            Vector2 knockback
        )
        {
            if (monster == null)
            {
                return;
            }


            monster.TakeDamage(
                damage,
                knockback
            );
        }
    }
}
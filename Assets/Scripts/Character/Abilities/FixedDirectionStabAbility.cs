using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class FixedDirectionStabAbility : StabAbility
    {
        protected override IEnumerator Stab()
        {
            hitMonsters = new FastList<GameObject>();

            timeSinceLastAttack -= stabTime;

            float t = 0f;

            weaponSpriteRenderer.enabled = true;


            Vector2 dir =
                playerCharacter.LookDirection.x > 0
                    ? Vector2.right
                    : Vector2.left;


            // =====================================================
            // 찌르기 진행
            // =====================================================

            while (t < stabTime)
            {
                Vector2 attackBoxPosition =
                    (Vector2)playerCharacter.CenterTransform.position
                    +
                    dir *
                    (
                        weaponSize.x / 2f
                        +
                        stabOffset
                        +
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
                // 몬스터 타격
                // =================================================

                foreach (Collider2D collider in hitColliders)
                {
                    if (
                        hitMonsters.Contains(
                            collider.gameObject
                        )
                    )
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


                    // 피해량 기록
                    //
                    // 1. 기존 전체 피해량
                    // 2. AugmentDamageTracker 증강별 피해량
                    //
                    // 을 동시에 처리
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
                    (Vector2)playerCharacter.CenterTransform.position
                    +
                    dir *
                    (
                        weaponSpriteRenderer.transform.localScale.x
                        /
                        initialScale.x
                        *
                        weaponSize.x
                        /
                        2f
                        +
                        stabOffset
                        +
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
    }
}
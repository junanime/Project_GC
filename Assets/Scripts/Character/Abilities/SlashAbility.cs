using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class SlashAbility : MeleeAbility
    {
        [Header("Slash Stats")]
        [SerializeField] protected float slashAngle;
        [SerializeField] protected float slashOffset;
        [SerializeField] protected float slashTime;
        [SerializeField] protected float scaleInTime = 0.1f;

        protected Vector2 weaponSize;
        protected Vector2 initialWeaponScale;

        private FastList<GameObject> hitMonsters;


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            weaponSize =
                weaponSpriteRenderer.bounds.size;

            initialWeaponScale =
                weaponSpriteRenderer.transform.localScale;

            weaponSpriteRenderer.enabled = false;
        }


        // =========================================================
        // Attack
        // =========================================================

        protected override void Attack()
        {
            StartCoroutine(
                Slash()
            );
        }


        // =========================================================
        // Slash
        // =========================================================

        protected IEnumerator Slash()
        {
            hitMonsters =
                new FastList<GameObject>();


            timeSinceLastAttack -=
                slashTime;


            float t = 0f;

            weaponSpriteRenderer.enabled =
                true;


            Vector2 initialDir =
                playerCharacter.LookDirection;


            // =====================================================
            // 베기 진행
            // =====================================================

            while (t < slashTime)
            {
                float scaleMultiplier =
                    GetScaleMultiplier(t);


                float theta =
                    slashAngle *
                    (slashTime / 2f - t) /
                    slashTime;


                Vector2 dir =
                    new Vector2(
                        initialDir.x *
                            Mathf.Cos(
                                Mathf.Deg2Rad * theta
                            )
                        -
                        initialDir.y *
                            Mathf.Sin(
                                Mathf.Deg2Rad * theta
                            ),

                        initialDir.x *
                            Mathf.Sin(
                                Mathf.Deg2Rad * theta
                            )
                        +
                        initialDir.y *
                            Mathf.Cos(
                                Mathf.Deg2Rad * theta
                            )
                    );


                Vector2 attackBoxPosition =
                    (Vector2)playerCharacter
                        .CenterTransform
                        .position
                    +
                    dir *
                    (
                        scaleMultiplier *
                        weaponSize.x /
                        2f
                        +
                        slashOffset
                    );


                float attackAngle =
                    Vector2.SignedAngle(
                        Vector2.right,
                        dir
                    );


                Collider2D[] hitColliders =
                    Physics2D.OverlapBoxAll(
                        attackBoxPosition,
                        scaleMultiplier *
                        weaponSize,
                        attackAngle,
                        targetLayer
                    );


                // =================================================
                // 무기 비주얼
                // =================================================

                weaponSpriteRenderer.transform.position =
                    attackBoxPosition;


                weaponSpriteRenderer
                    .transform
                    .localRotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        attackAngle
                    );


                weaponSpriteRenderer
                    .transform
                    .localScale =
                    initialWeaponScale *
                    scaleMultiplier;


                // =================================================
                // 몬스터 타격
                // =================================================

                foreach (
                    Collider2D collider in hitColliders
                )
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


                    // 이미 맞은 몬스터 등록
                    hitMonsters.Add(
                        collider.gameObject
                    );


                    float dealtDamage =
                        damage.Value;


                    // 실제 피해
                    monster.TakeDamage(
                        dealtDamage,
                        dir * knockback.Value
                    );


                    // =============================================
                    // 피해 기록
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
                    // 동시에 기록
                    // =============================================

                    ReportDamage(
                        dealtDamage
                    );
                }


                t += Time.deltaTime;

                yield return null;
            }


            weaponSpriteRenderer.enabled =
                false;
        }


        // =========================================================
        // Deregister Monster
        // =========================================================

        private void DeregisterMonster(
            Monster monster
        )
        {
            if (
                monster == null ||
                hitMonsters == null
            )
            {
                return;
            }


            hitMonsters.Remove(
                monster.gameObject
            );
        }


        // =========================================================
        // Scale Multiplier
        // =========================================================

        private float GetScaleMultiplier(
            float t
        )
        {
            // scaleInTime이 0이면
            // 0으로 나누는 문제 방지
            if (scaleInTime <= 0f)
            {
                return 1f;
            }


            if (t < scaleInTime)
            {
                return EasingUtils.EaseOutQuad(
                    t / scaleInTime
                );
            }
            else if (
                t > slashTime - scaleInTime
            )
            {
                return EasingUtils.EaseOutQuad(
                    (slashTime - t) /
                    scaleInTime
                );
            }
            else
            {
                return 1f;
            }
        }
    }
}
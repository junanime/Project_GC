using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class BazookaGunAbility : GunAbility
    {
        [Header("Bazooka Gun Stats")]
        [SerializeField] protected GameObject bazookaGun;
        [SerializeField] protected Transform launchTransform;
        [SerializeField] protected ParticleSystem launchParticles;
        [SerializeField] protected UpgradeableAOE explosionAOE;
        [SerializeField] protected Vector2 hoverOffset;
        [SerializeField] protected float targetRadius = 5f;

        protected Vector2 currHoverOffset;
        protected Vector3 gunDirection = Vector2.right;
        protected float theta = 0f;


        // =========================================================
        // Update
        // =========================================================

        protected override void Update()
        {
            base.Update();


            // 재장전 중 회전값
            float reloadRotation = 0f;

            float t =
                timeSinceLastAttack /
                cooldown.Value;


            if (t > 0f && t < 1f)
            {
                reloadRotation =
                    t * 360f;
            }


            currHoverOffset =
                hoverOffset +
                Vector2.up *
                Mathf.Sin(Time.time * 5f) *
                0.1f;


            if (bazookaGun != null &&
                playerCharacter != null)
            {
                bazookaGun.transform.position =
                    (Vector2)playerCharacter
                        .CenterTransform
                        .position
                    +
                    currHoverOffset;
            }
        }


        // =========================================================
        // Launch Projectile
        // =========================================================

        protected override void LaunchProjectile()
        {
            StartCoroutine(
                LaunchProjecileAnimation()
            );
        }


        // =========================================================
        // Launch Animation
        // =========================================================

        protected IEnumerator LaunchProjecileAnimation()
        {
            if (bazookaGun == null ||
                launchTransform == null)
            {
                yield break;
            }


            ISpatialHashGridClient targetEntity =
                entityManager.Grid.FindClosestInRadius(
                    bazookaGun.transform.position,
                    targetRadius
                );


            Vector2 launchDirection =
                targetEntity == null
                    ? Random.insideUnitCircle.normalized
                    : (
                        targetEntity.Position -
                        (Vector2)bazookaGun.transform.position
                    ).normalized;


            if (launchDirection.sqrMagnitude <= 0.0001f)
            {
                launchDirection =
                    Vector2.right;
            }


            float targetTheta =
                Vector2.SignedAngle(
                    Vector2.right,
                    launchDirection
                );


            float initialTheta =
                theta;


            float t =
                0f;


            float safeFireRate =
                Mathf.Max(
                    0.01f,
                    firerate.Value
                );


            float tMax =
                1f /
                safeFireRate *
                0.45f;


            // =====================================================
            // 첫 번째 조준
            // =====================================================

            while (t < tMax)
            {
                if (targetEntity != null)
                {
                    launchDirection =
                        (
                            targetEntity.Position -
                            (Vector2)bazookaGun.transform.position
                        ).normalized;


                    if (launchDirection.sqrMagnitude <= 0.0001f)
                    {
                        launchDirection =
                            Vector2.right;
                    }


                    targetTheta =
                        Vector2.SignedAngle(
                            Vector2.right,
                            launchDirection
                        );
                }


                float tScaled =
                    t / tMax;


                theta =
                    Mathf.Lerp(
                        initialTheta,
                        targetTheta,
                        EasingUtils.EaseOutBack(tScaled)
                    );


                bazookaGun.transform.rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        theta
                    );


                t +=
                    Time.deltaTime;

                yield return null;
            }


            // =====================================================
            // 발사 직전 추적
            // =====================================================

            if (targetEntity != null)
            {
                t = 0f;


                while (t < tMax)
                {
                    launchDirection =
                        (
                            targetEntity.Position -
                            (Vector2)bazookaGun.transform.position
                        ).normalized;


                    if (launchDirection.sqrMagnitude <= 0.0001f)
                    {
                        launchDirection =
                            Vector2.right;
                    }


                    targetTheta =
                        Vector2.SignedAngle(
                            Vector2.right,
                            launchDirection
                        );


                    bazookaGun.transform.rotation =
                        Quaternion.Euler(
                            0f,
                            0f,
                            targetTheta
                        );


                    t +=
                        Time.deltaTime;

                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSeconds(
                    tMax
                );
            }


            theta =
                targetTheta;


            bazookaGun.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    theta
                );


            // =====================================================
            // 바주카 투사체 생성
            // =====================================================

            ExplosiveProjectile projectile =
                entityManager.SpawnProjectile(
                    projectileIndex,
                    launchTransform.position,
                    damage.Value,
                    knockback.Value,
                    speed.Value,
                    monsterLayer
                ) as ExplosiveProjectile;


            if (projectile == null)
            {
                Debug.LogWarning(
                    "[BazookaGunAbility] ExplosiveProjectile 생성에 실패했습니다."
                );

                yield break;
            }


            // 폭발 설정
            projectile.SetupExplosion(
                damage.Value,
                explosionAOE.Value,
                knockback.Value
            );


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
            // → 기존 StatsManager 총 피해량
            // → AugmentDamageTracker 바주카 누적 피해량
            //
            // 둘 다 처리
            // =====================================================

            projectile.OnHitDamageable.AddListener(
                ReportDamage
            );


            projectile.Launch(
                launchDirection
            );


            if (launchParticles != null)
            {
                launchParticles.Play();
            }
        }
    }
}

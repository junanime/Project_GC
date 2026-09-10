using UnityEngine;

namespace Vampire
{
    public class GrenadeThrowable : Throwable
    {
        [Header("Fragment Settings")]
        [SerializeField] private GameObject fragmentPrefab;
        [SerializeField] protected float fragmentSpeed;
        [SerializeField] protected int fragmentCount;

        protected int projectileIndex = -1;


        // =========================================================
        // Init
        // =========================================================

        public override void Init(
            EntityManager entityManager,
            Character playerCharacter
        )
        {
            base.Init(
                entityManager,
                playerCharacter
            );

            projectileIndex =
                entityManager.AddPoolForProjectile(
                    fragmentPrefab
                );
        }


        // =========================================================
        // Setup Grenade
        // =========================================================

        public void SetupGrenade(int fragmentCount)
        {
            this.fragmentCount =
                Mathf.Max(0, fragmentCount);
        }


        // =========================================================
        // Explode
        // =========================================================

        protected override void Explode()
        {
            // 파편 개수가 없는 경우
            if (fragmentCount <= 0)
            {
                DestroyThrowable();
                return;
            }


            for (int i = 0; i < fragmentCount; i++)
            {
                float theta =
                    i *
                    Mathf.PI *
                    2.0f /
                    fragmentCount;


                Vector2 direction =
                    new Vector2(
                        Mathf.Sin(theta),
                        Mathf.Cos(theta)
                    );


                Projectile projectile =
                    entityManager.SpawnProjectile(
                        projectileIndex,
                        transform.position,
                        damage,
                        knockback,
                        fragmentSpeed,
                        targetLayer
                    );


                if (projectile == null)
                {
                    continue;
                }


                // =================================================
                // 파편 피해 전달
                // =================================================
                //
                // Fragment Projectile
                //      ↓
                // 실제 몬스터 피해량
                //      ↓
                // ForwardFragmentDamage()
                //      ↓
                // GrenadeThrowable.OnHitDamageable
                //      ↓
                // GrenadeThrowableAbility.ReportDamage()
                //
                // 이를 통해 수류탄 증강의 누적 피해량으로 기록됩니다.
                // =================================================

                projectile.OnHitDamageable.AddListener(
                    ForwardFragmentDamage
                );


                projectile.Launch(
                    direction
                );
            }


            DestroyThrowable();
        }


        // =========================================================
        // Forward Fragment Damage
        // =========================================================

        private void ForwardFragmentDamage(float dealtDamage)
        {
            if (dealtDamage <= 0f)
                return;


            OnHitDamageable?.Invoke(
                dealtDamage
            );
        }
    }
}

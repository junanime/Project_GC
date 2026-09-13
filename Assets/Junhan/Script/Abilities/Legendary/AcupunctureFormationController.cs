using UnityEngine;

namespace Vampire
{
    // 특수 증강: 침술진
    //
    // 수정 내용:
    // - 대쉬 시작 위치에서 360도 원형으로 침을 발사한다.
    // - 침술진 발사 침에는 다른 특수 증강 효과를 적용하지 않는다.
    // - 일반 스탯 강화: 데미지, 투사체 속도, 투사체 수, 크기, 사거리만 반영한다.
    // - 침술진이 준 피해는 AugmentDamageTracker에 "침술진"으로 별도 기록한다.
    public class AcupunctureFormationController : MonoBehaviour
    {
        private Character sourceCharacter;
        private EntityManager entityManager;
        private SyringeDartAbility sourceNeedleAbility;

        private GameObject projectilePrefab;
        private LayerMask monsterLayer;
        private int projectilePoolIndex;

        private int baseNeedleCount;
        private float damageMultiplier;

        private bool previousIsDashing = false;
        private Vector3 previousPlayerPosition;


        [Header("Debug")]
        [Tooltip("체크하면 침술진 발사 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = false;


        // =========================================================
        // Create
        // =========================================================

        public static AcupunctureFormationController Create(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceNeedleAbility,
            float formationLifetime,
            int needleCount,
            float damageMultiplier,
            float visualScale
        )
        {
            GameObject controllerObject =
                new GameObject(
                    "Acupuncture Formation Controller"
                );


            AcupunctureFormationController controller =
                controllerObject.AddComponent<AcupunctureFormationController>();


            controller.Init(
                sourceCharacter,
                entityManager,
                sourceNeedleAbility,
                needleCount,
                damageMultiplier
            );


            return controller;
        }


        // =========================================================
        // Init
        // =========================================================

        private void Init(
            Character sourceCharacter,
            EntityManager entityManager,
            SyringeDartAbility sourceNeedleAbility,
            int needleCount,
            float damageMultiplier
        )
        {
            this.sourceCharacter =
                sourceCharacter;

            this.entityManager =
                entityManager;

            this.sourceNeedleAbility =
                sourceNeedleAbility;


            baseNeedleCount =
                Mathf.Max(
                    1,
                    needleCount
                );


            this.damageMultiplier =
                Mathf.Max(
                    0.01f,
                    damageMultiplier
                );


            if (sourceNeedleAbility == null ||
                entityManager == null)
            {
                Debug.LogWarning(
                    "[침술진] 초기화 실패 - 필요한 참조가 없습니다."
                );

                Destroy(gameObject);

                return;
            }


            projectilePrefab =
                sourceNeedleAbility.ProjectilePrefab;


            monsterLayer =
                sourceNeedleAbility.MonsterLayer;


            projectilePoolIndex =
                entityManager.AddPoolForProjectile(
                    projectilePrefab
                );


            if (sourceCharacter != null)
            {
                previousPlayerPosition =
                    sourceCharacter.CenterTransform.position;


                previousIsDashing =
                    sourceCharacter.IsDashing;


                if (sourceCharacter.OnDeath != null)
                {
                    sourceCharacter.OnDeath.AddListener(
                        DestroySelf
                    );
                }
            }


            if (debugLog)
            {
                Debug.Log(
                    "[침술진] 컨트롤러 생성 완료 - " +
                    "특수 증강 제외, 일반 스탯만 반영"
                );
            }
        }


        // =========================================================
        // Update
        // =========================================================

        private void Update()
        {
            if (sourceCharacter == null ||
                entityManager == null ||
                sourceNeedleAbility == null)
            {
                Destroy(gameObject);

                return;
            }


            bool currentIsDashing =
                sourceCharacter.IsDashing;


            // false → true가 되는 순간이 대쉬 시작.
            //
            // previousPlayerPosition은
            // 대쉬 직전 플레이어 위치로 사용한다.
            if (!previousIsDashing &&
                currentIsDashing)
            {
                FireRadialNeedles(
                    previousPlayerPosition
                );
            }


            previousIsDashing =
                currentIsDashing;


            previousPlayerPosition =
                sourceCharacter.CenterTransform.position;
        }


        // =========================================================
        // Fire Radial Needles
        // =========================================================

        private void FireRadialNeedles(
            Vector3 origin
        )
        {
            int finalNeedleCount =
                GetFinalNeedleCount();


            if (debugLog)
            {
                Debug.Log(
                    $"[침술진] 원형 침 발사 | " +
                    $"발사 수 {finalNeedleCount} | " +
                    $"특수 증강 적용 안 함"
                );
            }


            for (
                int i = 0;
                i < finalNeedleCount;
                i++
            )
            {
                float angle =
                    i *
                    360f /
                    finalNeedleCount;


                Vector2 direction =
                    AngleToVector(
                        angle
                    );


                Projectile projectile =
                    entityManager.SpawnProjectile(
                        projectilePoolIndex,
                        origin,
                        sourceNeedleAbility
                            .GetAcupunctureFormationDamage()
                            * damageMultiplier,
                        sourceNeedleAbility
                            .GetAcupunctureFormationKnockback(),
                        sourceNeedleAbility
                            .GetAcupunctureFormationSpeed(),
                        monsterLayer
                    );


                if (projectile == null)
                {
                    continue;
                }


                // =================================================
                // 크기
                // =================================================

                projectile.transform.localScale =
                    Vector3.one *
                    sourceNeedleAbility
                        .GetAcupunctureFormationProjectileSizeMultiplier()
                    *
                    0.85f;


                // =================================================
                // 사거리
                // =================================================

                projectile.maxDistance =
                    sourceNeedleAbility
                        .GetAcupunctureFormationMaxDistance();


                // =================================================
                // 특수 증강 적용 안 함
                // =================================================
                //
                // 여기서
                //
                // SyringeProjectile.ConfigureSpecials(...)
                //
                // 를 호출하지 않는다.
                //
                // 따라서:
                //
                // 독
                // 폭발
                // 유도
                // 관통
                // 꿀
                // 모기
                // 침귀환
                //
                // 등의 특수 효과는 침술진 침에 적용되지 않는다.
                // =================================================


                // =================================================
                // 피해량 기록
                // =================================================
                //
                // 기존:
                //
                // projectile.OnHitDamageable.AddListener(
                //     sourceCharacter.OnDealDamage.Invoke
                // );
                //
                //
                // 변경:
                //
                // ReportFormationDamage()
                //
                // 실제 적중 피해량을:
                //
                // 1. 기존 총 피해량 시스템
                // 2. AugmentDamageTracker "침술진"
                //
                // 에 동시에 기록한다.
                // =================================================

                projectile.OnHitDamageable.AddListener(
                    ReportFormationDamage
                );


                // 발사
                projectile.Launch(
                    direction
                );
            }
        }


        // =========================================================
        // Damage Report
        // =========================================================

        private void ReportFormationDamage(
            float dealtDamage
        )
        {
            if (dealtDamage <= 0f)
            {
                return;
            }


            // =====================================================
            // 기존 전체 피해량
            // =====================================================

            if (sourceCharacter != null)
            {
                sourceCharacter.OnDealDamage.Invoke(
                    dealtDamage
                );
            }


            // =====================================================
            // 증강별 피해량
            // =====================================================

            if (AugmentDamageTracker.Instance != null)
            {
                AugmentDamageTracker.Instance.RecordDamage(
                    "침술진",
                    dealtDamage
                );
            }
        }


        // =========================================================
        // Final Needle Count
        // =========================================================

        private int GetFinalNeedleCount()
        {
            int finalCount =
                baseNeedleCount;


            if (sourceNeedleAbility != null)
            {
                // 기본 발사체 수 1개는
                // 침술진 기본 needleCount에 이미 포함되어 있다고 보고,
                //
                // 추가 발사체 증가분만 더한다.
                int projectileBonus =
                    Mathf.Max(
                        0,
                        sourceNeedleAbility
                            .GetAcupunctureFormationProjectileCount()
                        -
                        1
                    );


                finalCount +=
                    projectileBonus;
            }


            return Mathf.Max(
                1,
                finalCount
            );
        }


        // =========================================================
        // Angle → Vector
        // =========================================================

        private Vector2 AngleToVector(
            float angleDegrees
        )
        {
            float rad =
                angleDegrees *
                Mathf.Deg2Rad;


            return new Vector2(
                Mathf.Cos(rad),
                Mathf.Sin(rad)
            ).normalized;
        }


        // =========================================================
        // Destroy
        // =========================================================

        private void DestroySelf()
        {
            if (gameObject != null)
            {
                Destroy(
                    gameObject
                );
            }
        }


        private void OnDestroy()
        {
            if (sourceCharacter != null &&
                sourceCharacter.OnDeath != null)
            {
                sourceCharacter.OnDeath.RemoveListener(
                    DestroySelf
                );
            }
        }
    }
}
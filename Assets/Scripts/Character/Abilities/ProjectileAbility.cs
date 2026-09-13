using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class ProjectileAbility : Ability
    {
        [Header("Projectile Stats")]
        [Tooltip("발사할 투사체 프리팹입니다.")]
        [SerializeField] protected GameObject projectilePrefab;

        [Tooltip("투사체가 맞출 몬스터 레이어입니다.")]
        [SerializeField] protected LayerMask monsterLayer;

        [Tooltip("투사체 기본 피해량입니다.")]
        [SerializeField] protected UpgradeableDamage damage;

        [Tooltip("투사체 이동 속도입니다.")]
        [SerializeField] protected UpgradeableProjectileSpeed speed;

        [Tooltip("투사체 넉백 수치입니다.")]
        [SerializeField] protected UpgradeableKnockback knockback;

        [Tooltip("투사체 발사 쿨타임입니다.")]
        [SerializeField] protected UpgradeableWeaponCooldown cooldown;


        [Header("Projectile Spawn Position / 투사체 생성 위치")]
        [Tooltip(
            "체크하면 캐릭터 중심이 아니라 " +
            "캐릭터 앞쪽/아래쪽으로 보정된 위치에서 투사체가 생성됩니다."
        )]
        [SerializeField] protected bool useProjectileSpawnOffset = true;

        [Tooltip(
            "캐릭터가 바라보는 방향으로 투사체 생성 위치를 얼마나 앞당길지 정합니다. " +
            "손 쪽에서 나가게 하려면 0.2~0.45 사이로 조절하세요."
        )]
        [SerializeField] protected float projectileSpawnForwardOffset = 0.28f;

        [Tooltip(
            "투사체 생성 위치를 캐릭터 중심에서 아래로 얼마나 내릴지 정합니다. " +
            "머리에서 나가면 이 값을 올리세요."
        )]
        [SerializeField] protected float projectileSpawnDownOffset = 0.18f;

        [Tooltip(
            "직접 지정한 발사 위치 Transform입니다. " +
            "비워두면 Character의 CenterTransform을 기준으로 위 보정값을 적용합니다."
        )]
        [SerializeField] protected Transform projectileSpawnPoint;

        [Tooltip(
            "체크하면 부채꼴 발사 시 각 투사체의 벌어진 방향을 기준으로 " +
            "생성 위치를 조금씩 다르게 잡습니다. " +
            "꺼두면 모든 투사체가 같은 손 위치에서 나갑니다."
        )]
        [SerializeField] protected bool useSpreadDirectionForSpawnOffset = false;


        [Header("Spread Settings / 부채꼴 발사")]
        [Tooltip("추가 투사체가 있을 때 각 투사체 사이의 각도입니다.")]
        [SerializeField] protected float spreadAngle = 30f;


        [Header("Debug")]
        [Tooltip("체크하면 투사체 발사 로그를 Console에 출력합니다.")]
        [SerializeField] protected bool debugProjectileLog = false;


        protected float timeSinceLastAttack;
        protected int projectileIndex;


        // =========================================================
        // Use
        // =========================================================

        protected override void Use()
        {
            base.Use();

            gameObject.SetActive(true);

            timeSinceLastAttack = cooldown.Value;

            projectileIndex =
                entityManager.AddPoolForProjectile(projectilePrefab);
        }


        // =========================================================
        // Update
        // =========================================================

        protected virtual void Update()
        {
            timeSinceLastAttack += Time.deltaTime;


            float attackSpeedMultiplier =
                Mathf.Max(
                    0.01f,
                    playerCharacter.AttackSpeedMultiplier
                );


            float effectiveCooldown =
                cooldown.Value / attackSpeedMultiplier;


            if (timeSinceLastAttack >= effectiveCooldown)
            {
                timeSinceLastAttack =
                    Mathf.Repeat(
                        timeSinceLastAttack,
                        effectiveCooldown
                    );

                Attack();
            }
        }


        // =========================================================
        // Attack
        // =========================================================

        protected virtual void Attack()
        {
            LaunchProjectile();
        }


        // =========================================================
        // Launch Projectile
        // =========================================================

        protected virtual void LaunchProjectile()
        {
            int extra =
                playerCharacter.AdditionalProjectiles;

            int total =
                1 + extra;


            if (debugProjectileLog)
            {
                Debug.Log(
                    $"<color=orange>[LaunchProjectile 호출]</color> " +
                    $"총 {total}발 부채꼴 발사 시도"
                );
            }


            float totalDamage =
                damage.Value *
                playerCharacter.DamageMultiplier;


            Vector2 baseDirection =
                GetBaseFireDirection();


            for (int i = 0; i < total; i++)
            {
                float offsetAngle =
                    (i - (total - 1) / 2f) *
                    spreadAngle;


                Quaternion rotation =
                    Quaternion.Euler(
                        0f,
                        0f,
                        offsetAngle
                    );


                Vector2 shotDirection =
                    rotation *
                    baseDirection;


                if (shotDirection.sqrMagnitude <= 0.0001f)
                {
                    shotDirection = Vector2.right;
                }


                shotDirection.Normalize();


                Vector2 spawnOffsetDirection =
                    useSpreadDirectionForSpawnOffset
                        ? shotDirection
                        : baseDirection;


                Vector2 spawnPosition =
                    GetProjectileSpawnPosition(
                        spawnOffsetDirection
                    );


                Projectile projectile =
                    entityManager.SpawnProjectile(
                        projectileIndex,
                        spawnPosition,
                        totalDamage,
                        knockback.Value,
                        speed.Value,
                        monsterLayer
                    );


                if (projectile == null)
                {
                    continue;
                }


                if (debugProjectileLog)
                {
                    Debug.Log(
                        $"<color=cyan>[생성 완료]</color> " +
                        $"{i + 1}번째 발사체 " +
                        $"ID: {projectile.gameObject.GetInstanceID()} | " +
                        $"각도: {offsetAngle} | " +
                        $"생성 위치: {spawnPosition}"
                    );
                }


                // =================================================
                // 피해 기록
                // =================================================
                //
                // 기존:
                //
                // projectile.OnHitDamageable.AddListener(
                //     playerCharacter.OnDealDamage.Invoke
                // );
                //
                // 변경:
                //
                // 실제로 적에게 준 피해가 발생하면
                // Ability.ReportDamage()를 호출합니다.
                //
                // ReportDamage에서는:
                //
                // 1. Character.OnDealDamage
                //    → 기존 StatsManager 총 피해량 기록
                //
                // 2. AugmentDamageTracker
                //    → 이 Ability의 누적 피해량 기록
                //
                // 두 작업을 동시에 처리합니다.
                // =================================================

                projectile.OnHitDamageable.AddListener(
                    ReportDamage
                );


                projectile.Launch(
                    shotDirection
                );
            }
        }


        // =========================================================
        // Base Fire Direction
        // =========================================================

        protected virtual Vector2 GetBaseFireDirection()
        {
            if (playerCharacter == null)
            {
                return Vector2.right;
            }


            Vector2 lookDirection =
                playerCharacter.LookDirection;


            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.right;
            }


            return lookDirection.normalized;
        }


        // =========================================================
        // Projectile Spawn Position
        // =========================================================

        protected virtual Vector2 GetProjectileSpawnPosition(
            Vector2 fireDirection
        )
        {
            Vector2 spawnPosition =
                GetProjectileSpawnBasePosition();


            if (!useProjectileSpawnOffset)
            {
                return spawnPosition;
            }


            if (fireDirection.sqrMagnitude <= 0.0001f)
            {
                fireDirection = Vector2.right;
            }


            fireDirection.Normalize();


            spawnPosition +=
                fireDirection *
                projectileSpawnForwardOffset;


            spawnPosition +=
                Vector2.down *
                projectileSpawnDownOffset;


            return spawnPosition;
        }


        // =========================================================
        // Projectile Spawn Base Position
        // =========================================================

        protected virtual Vector2 GetProjectileSpawnBasePosition()
        {
            if (projectileSpawnPoint != null)
            {
                return projectileSpawnPoint.position;
            }


            if (playerCharacter != null &&
                playerCharacter.CenterTransform != null)
            {
                return playerCharacter
                    .CenterTransform
                    .position;
            }


            if (playerCharacter != null)
            {
                return playerCharacter
                    .transform
                    .position;
            }


            return transform.position;
        }
    }
}
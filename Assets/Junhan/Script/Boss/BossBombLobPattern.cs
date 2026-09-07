using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 Yellow Core 계열 폭탄 투척 패턴입니다.
    ///
    /// Step 7 추가:
    /// - 기존 플레이어 폭발 피해 유지
    /// - 폭발 반경 안에 소유 보스가 있으면 BossSelf 피해
    /// - 여러 보스 파츠가 동시에 들어와도 가장 가까운 살아있는 파츠 1개만 피해
    /// - 보스 자폭 피해는 전체 Max HP 비율로 계산
    /// - Core Open/Groggy 플레이어 피해 배율은 BossSelf 피해에 적용되지 않음
    /// </summary>
    public class BossBombLobPattern : BossPatternBase
    {
        [Header("Bomb Lob - Warning")]

        [Tooltip("폭발 전 바닥에 표시할 경고 원 프리팹입니다.")]
        [SerializeField]
        private GameObject warningCirclePrefab;

        [Tooltip(
            "경고 원이 표시된 뒤 실제 폭발하기까지 기다리는 시간입니다. " +
            "폭탄 투사체도 이 시간 동안 포물선으로 이동합니다."
        )]
        [SerializeField]
        private float warningDuration = 1f;

        [Header("Bomb Lob - Projectile Visual")]

        [Tooltip(
            "보스가 던지는 폭탄 투사체 프리팹입니다. " +
            "SpriteRenderer가 들어있는 프리팹을 넣으면 됩니다."
        )]
        [SerializeField]
        private GameObject bombProjectilePrefab;

        [Tooltip(
            "보스 중심 위치에서 폭탄이 시작될 상대 위치입니다. " +
            "Y값을 올리면 보스 머리 위에서 시작합니다."
        )]
        [SerializeField]
        private Vector2 projectileStartOffsetFromBoss =
            new Vector2(0f, 1.6f);

        [Tooltip(
            "폭탄이 포물선으로 날아오를 높이입니다. " +
            "값이 클수록 더 높게 떠서 떨어집니다."
        )]
        [SerializeField]
        private float arcHeight = 3f;

        [Tooltip(
            "폭탄 프리팹 크기 배율입니다. " +
            "1이면 원본 크기를 유지합니다."
        )]
        [SerializeField]
        private float projectileScaleMultiplier = 1f;

        [Tooltip("체크하면 폭탄 투사체가 이동 방향을 바라보도록 회전합니다.")]
        [SerializeField]
        private bool rotateProjectileToMoveDirection = true;

        [Tooltip(
            "투사체 이미지의 기본 방향 보정 각도입니다. " +
            "이미지가 위를 보고 있으면 -90 또는 90을 넣어 맞추면 됩니다."
        )]
        [SerializeField]
        private float projectileVisualAngleOffset = 0f;

        [Tooltip("체크하면 폭탄 투사체가 날아가는 동안 계속 회전합니다.")]
        [SerializeField]
        private bool spinProjectile = false;

        [Tooltip(
            "Spin Projectile이 켜져 있을 때 초당 회전 각도입니다."
        )]
        [SerializeField]
        private float projectileSpinSpeed = 360f;

        [Header("Projectile Physics Safety")]

        [Tooltip(
            "체크하면 폭탄 투사체 프리팹 안의 Collider2D를 비활성화합니다. " +
            "이 패턴은 착탄 시 범위 판정으로 데미지를 주기 때문에 보통 켜두는 것을 추천합니다."
        )]
        [SerializeField]
        private bool disableProjectileColliders = true;

        [Tooltip(
            "체크하면 폭탄 투사체 프리팹의 Rigidbody2D 물리를 비활성화합니다. " +
            "포물선 이동을 코드로 직접 처리하기 때문에 보통 켜두는 것을 추천합니다."
        )]
        [SerializeField]
        private bool disableProjectileRigidbodySimulation = true;

        [Header("Projectile Sorting")]

        [Tooltip(
            "체크하면 폭탄 투사체의 SpriteRenderer Sorting Order를 강제로 설정합니다."
        )]
        [SerializeField]
        private bool forceProjectileSortingOrder = true;

        [Tooltip(
            "폭탄 투사체의 화면 표시 순서입니다. " +
            "보스나 몬스터 뒤에 가려지면 값을 높이세요."
        )]
        [SerializeField]
        private int projectileSortingOrder = 800;

        [Header("Explosion")]

        [Tooltip("폭발 반경입니다.")]
        [SerializeField]
        private float explosionRadius = 2f;

        [Tooltip(
            "폭발 기본 데미지입니다. " +
            "실제 플레이어 데미지는 보스 현재 페이즈의 Damage Multiplier가 곱해집니다."
        )]
        [SerializeField]
        private float explosionDamage = 20f;

        [Tooltip(
            "폭발할 때 생성할 이펙트 프리팹입니다. " +
            "비워두면 이펙트 없이 데미지만 들어갑니다."
        )]
        [SerializeField]
        private GameObject explosionEffectPrefab;

        [Tooltip(
            "폭발 후 투사체를 바로 삭제하지 않고 잠깐 남겨둘 시간입니다. " +
            "보통 0으로 둡니다."
        )]
        [SerializeField]
        private float projectileDestroyDelayAfterImpact = 0f;

        [Header("Boss Self Damage / 보스 자폭")]

        [Tooltip(
            "체크하면 폭탄 폭발 반경 안에 크리피커피 자신이 있을 때 BossSelf 피해를 받습니다."
        )]
        [SerializeField]
        private bool enableBossSelfDamage = true;

        [Tooltip(
            "폭탄 한 발이 보스에게 주는 자폭 피해입니다. " +
            "보스 전체 Max HP 기준 비율이며 0.0125는 1.25%입니다. " +
            "P2/P3에서 폭탄 수가 많아져도 과도한 고정 피해가 되지 않도록 낮게 시작하는 것을 권장합니다."
        )]
        [SerializeField, Range(0f, 1f)]
        private float bossSelfMaxHpDamagePercent = 0.0125f;

        [Tooltip(
            "폭발 범위가 실제 폭발 반경보다 이 값만큼 추가로 보스 파츠를 감지합니다. " +
            "시각적으로 닿았는데 판정이 안 나는 경우 0.05~0.15 정도 사용하세요."
        )]
        [SerializeField, Min(0f)]
        private float bossSelfDamageRadiusPadding = 0f;

        [Header("Bomb Count By Phase")]

        [Tooltip("1페이즈에서 떨어뜨릴 폭탄 수입니다.")]
        [SerializeField]
        private int bombCountPhase1 = 1;

        [Tooltip("2페이즈에서 떨어뜨릴 폭탄 수입니다.")]
        [SerializeField]
        private int bombCountPhase2 = 3;

        [Tooltip("3페이즈에서 떨어뜨릴 폭탄 수입니다.")]
        [SerializeField]
        private int bombCountPhase3 = 4;

        [Header("Targeting")]

        [Tooltip(
            "플레이어 위치 주변으로 폭탄 위치를 흩뿌리는 랜덤 반경입니다."
        )]
        [SerializeField]
        private float randomOffsetRadius = 1.5f;

        [Tooltip(
            "여러 개의 폭탄을 던질 때, 다음 폭탄 경고가 시작되기까지의 간격입니다."
        )]
        [SerializeField]
        private float intervalBetweenBombs = 0.15f;

        [Header("Debug")]

        [Tooltip(
            "체크하면 폭탄 패턴, 플레이어 피해, 보스 자폭 피해 로그를 Console에 출력합니다."
        )]
        [SerializeField]
        private bool debugBomb = false;

        protected override IEnumerator ExecutePattern()
        {
            if (bossController == null ||
                bossController.PlayerCharacter == null)
            {
                yield break;
            }

            int bombCount =
                GetPhaseBombCount();

            for (int i = 0;
                 i < bombCount;
                 i++)
            {
                Vector2 targetPosition =
                    GetBombTargetPosition();

                yield return
                    StartCoroutine(
                        SpawnBombWarningProjectileAndExplode(
                            targetPosition));

                if (intervalBetweenBombs > 0f &&
                    i < bombCount - 1)
                {
                    yield return
                        new WaitForSeconds(
                            intervalBetweenBombs);
                }
            }
        }

        private int GetPhaseBombCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return Mathf.Max(
                    1,
                    bombCountPhase3);
            }

            if (bossController.CurrentPhase == 2)
            {
                return Mathf.Max(
                    1,
                    bombCountPhase2);
            }

            return Mathf.Max(
                1,
                bombCountPhase1);
        }

        private Vector2 GetBombTargetPosition()
        {
            Vector2 playerPosition =
                bossController.PlayerCharacter
                    .transform.position;

            Vector2 randomOffset =
                Random.insideUnitCircle *
                Mathf.Max(
                    0f,
                    randomOffsetRadius);

            return
                playerPosition +
                randomOffset;
        }

        private IEnumerator SpawnBombWarningProjectileAndExplode(
            Vector2 targetPosition)
        {
            GameObject warning =
                CreateWarningCircle(
                    targetPosition);

            GameObject projectile =
                CreateBombProjectile(
                    targetPosition);

            float flightDuration =
                Mathf.Max(
                    0.01f,
                    warningDuration);

            if (projectile != null)
            {
                Vector3 startPosition =
                    GetProjectileStartPosition();

                Vector3 endPosition =
                    new Vector3(
                        targetPosition.x,
                        targetPosition.y,
                        startPosition.z);

                yield return
                    StartCoroutine(
                        MoveProjectileArc(
                            projectile,
                            startPosition,
                            endPosition,
                            flightDuration));
            }
            else
            {
                yield return
                    new WaitForSeconds(
                        flightDuration);
            }

            if (warning != null)
            {
                Destroy(
                    warning);
            }

            SpawnExplosionEffect(
                targetPosition);

            ApplyExplosionDamage(
                targetPosition);

            if (projectile != null)
            {
                if (projectileDestroyDelayAfterImpact >
                    0f)
                {
                    Destroy(
                        projectile,
                        projectileDestroyDelayAfterImpact);
                }
                else
                {
                    Destroy(
                        projectile);
                }
            }
        }

        private GameObject CreateWarningCircle(
            Vector2 targetPosition)
        {
            if (warningCirclePrefab == null)
            {
                return null;
            }

            GameObject warning =
                Instantiate(
                    warningCirclePrefab,
                    targetPosition,
                    Quaternion.identity);

            warning.transform.localScale =
                Vector3.one *
                explosionRadius;

            return warning;
        }

        private GameObject CreateBombProjectile(
            Vector2 targetPosition)
        {
            if (bombProjectilePrefab == null)
            {
                if (debugBomb)
                {
                    Debug.LogWarning(
                        "[BossBombLobPattern] Bomb Projectile Prefab이 비어 있습니다. " +
                        "경고원과 폭발 판정만 실행합니다.",
                        this
                    );
                }

                return null;
            }

            Vector3 startPosition =
                GetProjectileStartPosition();

            GameObject projectile =
                Instantiate(
                    bombProjectilePrefab,
                    startPosition,
                    Quaternion.identity);

            if (projectileScaleMultiplier > 0f &&
                !Mathf.Approximately(
                    projectileScaleMultiplier,
                    1f))
            {
                projectile.transform.localScale *=
                    projectileScaleMultiplier;
            }

            PrepareProjectilePhysics(
                projectile);

            ApplyProjectileSortingOrder(
                projectile);

            Vector2 startToTarget =
                targetPosition -
                (Vector2)startPosition;

            ApplyProjectileRotation(
                projectile,
                startToTarget);

            if (debugBomb)
            {
                Debug.Log(
                    $"[BossBombLobPattern] 폭탄 투사체 생성 | " +
                    $"Start={startPosition}, " +
                    $"Target={targetPosition}, " +
                    $"Prefab={bombProjectilePrefab.name}",
                    projectile
                );
            }

            return projectile;
        }

        private Vector3 GetProjectileStartPosition()
        {
            Vector3 bossCenter =
                bossController.BossCenterPosition;

            Vector3 offset =
                new Vector3(
                    projectileStartOffsetFromBoss.x,
                    projectileStartOffsetFromBoss.y,
                    0f);

            return
                bossCenter +
                offset;
        }

        private void PrepareProjectilePhysics(
            GameObject projectile)
        {
            if (projectile == null)
            {
                return;
            }

            if (disableProjectileColliders)
            {
                Collider2D[] colliders =
                    projectile
                        .GetComponentsInChildren
                        <
                            Collider2D
                        >(true);

                for (int i = 0;
                     i < colliders.Length;
                     i++)
                {
                    if (colliders[i] != null)
                    {
                        colliders[i].enabled =
                            false;
                    }
                }
            }

            if (disableProjectileRigidbodySimulation)
            {
                Rigidbody2D[] rigidbodies =
                    projectile
                        .GetComponentsInChildren
                        <
                            Rigidbody2D
                        >(true);

                for (int i = 0;
                     i < rigidbodies.Length;
                     i++)
                {
                    Rigidbody2D body =
                        rigidbodies[i];

                    if (body == null)
                    {
                        continue;
                    }

                    body.velocity =
                        Vector2.zero;

                    body.angularVelocity =
                        0f;

                    body.gravityScale =
                        0f;

                    body.simulated =
                        false;
                }
            }
        }

        private void ApplyProjectileSortingOrder(
            GameObject projectile)
        {
            if (projectile == null ||
                !forceProjectileSortingOrder)
            {
                return;
            }

            SpriteRenderer[] renderers =
                projectile
                    .GetComponentsInChildren
                    <
                        SpriteRenderer
                    >(true);

            for (int i = 0;
                 i < renderers.Length;
                 i++)
            {
                SpriteRenderer renderer =
                    renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                renderer.sortingOrder =
                    projectileSortingOrder;
            }
        }

        private IEnumerator MoveProjectileArc(
            GameObject projectile,
            Vector3 startPosition,
            Vector3 endPosition,
            float duration)
        {
            if (projectile == null)
            {
                yield break;
            }

            float elapsed =
                0f;

            Vector3 previousPosition =
                startPosition;

            projectile.transform.position =
                startPosition;

            while (elapsed <
                   duration)
            {
                if (projectile == null)
                {
                    yield break;
                }

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        duration);

                Vector3 nextPosition =
                    CalculateArcPosition(
                        startPosition,
                        endPosition,
                        t);

                projectile.transform.position =
                    nextPosition;

                Vector2 moveDirection =
                    nextPosition -
                    previousPosition;

                if (moveDirection.sqrMagnitude >
                    0.0001f)
                {
                    ApplyProjectileRotation(
                        projectile,
                        moveDirection);
                }

                if (spinProjectile)
                {
                    projectile.transform.Rotate(
                        0f,
                        0f,
                        projectileSpinSpeed *
                        Time.deltaTime);
                }

                previousPosition =
                    nextPosition;

                elapsed +=
                    Time.deltaTime;

                yield return null;
            }

            if (projectile != null)
            {
                projectile.transform.position =
                    endPosition;

                Vector2 finalDirection =
                    endPosition -
                    previousPosition;

                if (finalDirection.sqrMagnitude >
                    0.0001f)
                {
                    ApplyProjectileRotation(
                        projectile,
                        finalDirection);
                }
            }
        }

        private Vector3 CalculateArcPosition(
            Vector3 startPosition,
            Vector3 endPosition,
            float t)
        {
            Vector3 linearPosition =
                Vector3.Lerp(
                    startPosition,
                    endPosition,
                    t);

            float arcOffset =
                Mathf.Sin(
                    t *
                    Mathf.PI) *
                Mathf.Max(
                    0f,
                    arcHeight);

            linearPosition.y +=
                arcOffset;

            return
                linearPosition;
        }

        private void ApplyProjectileRotation(
            GameObject projectile,
            Vector2 moveDirection)
        {
            if (projectile == null ||
                !rotateProjectileToMoveDirection ||
                moveDirection.sqrMagnitude <=
                0.0001f)
            {
                return;
            }

            float angle =
                Mathf.Atan2(
                    moveDirection.y,
                    moveDirection.x) *
                Mathf.Rad2Deg;

            projectile.transform.rotation =
                Quaternion.Euler(
                    0f,
                    0f,
                    angle +
                    projectileVisualAngleOffset);
        }

        private void SpawnExplosionEffect(
            Vector2 targetPosition)
        {
            if (explosionEffectPrefab == null)
            {
                return;
            }

            Instantiate(
                explosionEffectPrefab,
                targetPosition,
                Quaternion.identity);
        }

        /// <summary>
        /// 하나의 폭발 판정으로 플레이어 피해와 보스 자폭 피해를 함께 처리합니다.
        /// </summary>
        private void ApplyExplosionDamage(
            Vector2 targetPosition)
        {
            if (bossController == null)
            {
                return;
            }

            float selfCheckRadius =
                Mathf.Max(
                    0f,
                    explosionRadius +
                    bossSelfDamageRadiusPadding);

            float overlapRadius =
                Mathf.Max(
                    explosionRadius,
                    selfCheckRadius);

            Collider2D[] hits =
                Physics2D.OverlapCircleAll(
                    targetPosition,
                    overlapRadius);

            ApplyPlayerExplosionDamage(
                targetPosition,
                hits);

            if (enableBossSelfDamage)
            {
                ApplyBossSelfExplosionDamage(
                    targetPosition,
                    hits,
                    selfCheckRadius);
            }
        }

        private void ApplyPlayerExplosionDamage(
            Vector2 targetPosition,
            Collider2D[] hits)
        {
            if (bossController == null ||
                bossController.PlayerCharacter == null ||
                hits == null)
            {
                return;
            }

            float finalDamage =
                bossController.GetModifiedDamage(
                    explosionDamage);

            HashSet<int> damagedCharacterIds =
                new HashSet<int>();

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider2D hit =
                    hits[i];

                if (hit == null)
                {
                    continue;
                }

                // Padding 영역은 보스 자폭 판정에만 사용합니다.
                // 플레이어 피해는 기존 explosionRadius 안에서만 발생합니다.
                float distanceToExplosion =
                    Vector2.Distance(
                        targetPosition,
                        hit.ClosestPoint(
                            targetPosition));

                if (distanceToExplosion >
                    explosionRadius +
                    0.001f)
                {
                    continue;
                }

                Character character =
                    hit.GetComponentInParent
                    <
                        Character
                    >();

                if (character == null ||
                    character !=
                    bossController.PlayerCharacter)
                {
                    continue;
                }

                int characterId =
                    character.gameObject
                        .GetInstanceID();

                if (!damagedCharacterIds.Add(
                        characterId))
                {
                    continue;
                }

                character.TakeDamage(
                    finalDamage,
                    Vector2.zero,
                    false);

                if (debugBomb)
                {
                    Debug.Log(
                        $"[BossBombLobPattern] Player hit | " +
                        $"Damage={finalDamage:0.##}, " +
                        $"Phase={bossController.CurrentPhase}, " +
                        $"Position={targetPosition}",
                        character
                    );
                }
            }
        }

        /// <summary>
        /// 폭발 범위 안의 소유 보스 파츠 중 가장 가까운 살아있는 파츠 하나에만
        /// BossSelf 피해를 적용합니다.
        /// </summary>
        private void ApplyBossSelfExplosionDamage(
            Vector2 targetPosition,
            Collider2D[] hits,
            float selfCheckRadius)
        {
            if (bossController == null ||
                hits == null ||
                hits.Length == 0 ||
                bossSelfMaxHpDamagePercent <=
                0f)
            {
                return;
            }

            Transform ownerRoot =
                bossController.transform.root;

            BossPartDamageTestPart nearestPart =
                null;

            float nearestDistanceSqr =
                float.MaxValue;

            HashSet<int> visitedPartIds =
                new HashSet<int>();

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                Collider2D hit =
                    hits[i];

                if (hit == null)
                {
                    continue;
                }

                BossPartDamageTestPart part =
                    hit.GetComponentInParent
                    <
                        BossPartDamageTestPart
                    >(true);

                if (part == null ||
                    part.IsBroken)
                {
                    continue;
                }

                if (ownerRoot != null &&
                    part.transform.root !=
                    ownerRoot)
                {
                    continue;
                }

                int partId =
                    part.gameObject
                        .GetInstanceID();

                if (!visitedPartIds.Add(
                        partId))
                {
                    continue;
                }

                Vector2 closestPoint =
                    hit.ClosestPoint(
                        targetPosition);

                float colliderDistance =
                    Vector2.Distance(
                        targetPosition,
                        closestPoint);

                if (colliderDistance >
                    selfCheckRadius +
                    0.001f)
                {
                    continue;
                }

                float distanceSqr =
                    (
                        (Vector2)
                        part.transform.position -
                        targetPosition
                    ).sqrMagnitude;

                if (distanceSqr <
                    nearestDistanceSqr)
                {
                    nearestDistanceSqr =
                        distanceSqr;

                    nearestPart =
                        part;
                }
            }

            if (nearestPart != null)
            {
                ApplySelfDamageToPart(
                    nearestPart,
                    targetPosition);

                return;
            }

            // 현재 테스트 보스는 파츠 구조이므로 보통 여기로 오지 않습니다.
            // 이후 파츠가 없는 BossController 프리팹을 테스트할 경우에만 fallback.
            ApplySelfDamageToBossControllerFallback(
                targetPosition);
        }

        private void ApplySelfDamageToPart(
            BossPartDamageTestPart nearestPart,
            Vector2 targetPosition)
        {
            if (nearestPart == null)
            {
                return;
            }

            BossPartDamageTestRootController
                rootController =
                    nearestPart.transform.root
                        .GetComponentInChildren
                        <
                            BossPartDamageTestRootController
                        >(true);

            if (rootController == null ||
                rootController.TotalMaxHealth <=
                0f)
            {
                if (debugBomb)
                {
                    Debug.LogWarning(
                        "[BossBombLobPattern] 보스 자폭 대상 파츠는 찾았지만 " +
                        "BossPartDamageTestRootController 또는 TotalMaxHealth를 찾지 못했습니다.",
                        this
                    );
                }

                return;
            }

            float selfDamage =
                rootController.TotalMaxHealth *
                bossSelfMaxHpDamagePercent;

            float actualDamage =
                nearestPart.TakeDamageFromSource(
                    selfDamage,
                    Vector2.zero,
                    false,
                    BossDamageSourceType.BossSelf);

            if (debugBomb)
            {
                Debug.Log(
                    $"[BossBombLobPattern] Boss Self Explosion | " +
                    $"Part={nearestPart.PartType}, " +
                    $"Percent={bossSelfMaxHpDamagePercent * 100f:0.##}%, " +
                    $"Requested={selfDamage:0.##}, " +
                    $"Actual={actualDamage:0.##}, " +
                    $"Position={targetPosition}",
                    this
                );
            }
        }

        private void ApplySelfDamageToBossControllerFallback(
            Vector2 targetPosition)
        {
            if (bossController == null)
            {
                return;
            }

            // 파츠 없는 보스는 현재 BossController의 maxHp가 public이 아니므로
            // 비율 기반 피해량을 안전하게 계산할 수 없습니다.
            // 잘못된 추측 피해를 넣지 않고 Debug 경고만 남깁니다.
            if (debugBomb)
            {
                Debug.LogWarning(
                    $"[BossBombLobPattern] 폭발 범위에 BossController는 있으나 " +
                    $"BossPartDamageTestPart를 찾지 못했습니다. " +
                    $"현재 Max HP 비율 자폭은 파츠 보스에서만 적용합니다. " +
                    $"Position={targetPosition}",
                    this
                );
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (bossController == null)
            {
                return;
            }

            Gizmos.color =
                Color.yellow;

            Gizmos.DrawWireSphere(
                GetProjectileStartPosition(),
                0.2f);
        }
    }
}

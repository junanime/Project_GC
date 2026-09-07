using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 Blue Core 계열 호밍 미사일 패턴입니다.
    ///
    /// Step 6A:
    /// - 일정 시간 플레이어 추적
    /// - 추진력 상실
    /// - 낙하
    /// - 바닥 매립
    /// - 매립 후 플레이어 투사체로 기폭
    /// - 폭발 시 보스/일반 몬스터 피해
    /// </summary>
    public class BossHomingMissilePattern :
        BossPatternBase
    {
        [Header("Homing Missile Settings")]

        [Tooltip(
            "보스가 발사할 호밍 미사일 프리팹입니다. " +
            "Project 창의 Boss_HomingMissile 프리팹을 넣으세요."
        )]
        [SerializeField]
        private GameObject missilePrefab;

        [Tooltip("1페이즈에서 한 번에 발사할 호밍 미사일 개수입니다.")]
        [SerializeField]
        private int missileCountPhase1 = 4;

        [Tooltip("2페이즈에서 한 번에 발사할 호밍 미사일 개수입니다.")]
        [SerializeField]
        private int missileCountPhase2 = 6;

        [Tooltip("3페이즈에서 한 번에 발사할 호밍 미사일 개수입니다.")]
        [SerializeField]
        private int missileCountPhase3 = 8;

        [Tooltip(
            "플레이어 방향을 중심으로 미사일이 퍼질 전체 각도입니다. " +
            "90이면 -45도부터 +45도까지 발사됩니다."
        )]
        [SerializeField]
        private float totalSpreadAngle = 90f;

        [Tooltip(
            "호밍 미사일 기본 이동 속도입니다. " +
            "실제 속도는 현재 페이즈 Projectile Speed Multiplier가 곱해집니다."
        )]
        [SerializeField]
        private float missileSpeed = 7f;

        [Tooltip(
            "Tracking 중 플레이어와 충돌했을 때의 기본 피해입니다. " +
            "실제 피해는 현재 페이즈 Damage Multiplier가 곱해집니다."
        )]
        [SerializeField]
        private float missileDamage = 15f;

        [Tooltip(
            "구버전 동작의 자동 소멸 시간입니다. " +
            "신규 Embedded Behavior가 켜져 있으면 아래 Tracking/Falling/Embedded 시간이 우선합니다."
        )]
        [SerializeField]
        private float missileLifeTime = 30f;

        [Header("Homing Movement")]

        [Tooltip("호밍 미사일이 플레이어 방향으로 꺾이는 속도입니다.")]
        [SerializeField]
        private float turnSpeed = 3f;

        [Tooltip("여러 미사일이 서로 겹치지 않도록 좌우로 벌어지는 거리입니다.")]
        [SerializeField]
        private float laneOffsetDistance = 1.4f;

        [Tooltip("플레이어에게 가까워질수록 좌우 동선 보정을 줄이는 거리입니다.")]
        [SerializeField]
        private float laneFadeDistance = 2.2f;

        [Tooltip("발사 직후 부채꼴 방향으로 먼저 퍼지는 시간입니다.")]
        [SerializeField]
        private float initialSpreadDuration = 0.2f;

        [Header("Embedded Missile / 매립 미사일")]

        [Tooltip(
            "체크하면 신규 Tracking → Falling → Embedded 구조를 사용합니다. " +
            "현재 크리피커피 보스에서는 켜두세요."
        )]
        [SerializeField]
        private bool enableEmbeddedBehavior = true;

        [Tooltip(
            "플레이어를 추적하는 시간입니다. " +
            "기획 기준 약 8초입니다."
        )]
        [SerializeField, Min(0.05f)]
        private float trackingDuration = 8f;

        [Tooltip(
            "추진력을 잃은 뒤 바닥에 박히기까지의 낙하 연출 시간입니다."
        )]
        [SerializeField, Min(0f)]
        private float fallingDuration = 0.6f;

        [Tooltip(
            "바닥에 매립된 뒤 사용되지 않았을 때 자동 제거되기까지의 시간입니다."
        )]
        [SerializeField, Min(0.1f)]
        private float embeddedLifetime = 18f;

        [Tooltip(
            "같은 보스가 필드에 유지할 수 있는 매립 미사일 최대 개수입니다. " +
            "초과하면 가장 오래된 매립 미사일부터 조용히 제거됩니다."
        )]
        [SerializeField, Min(1)]
        private int maxEmbeddedMissiles = 4;

        [Tooltip(
            "낙하 후 매립된 미사일의 크기 배율입니다."
        )]
        [SerializeField, Min(0.05f)]
        private float embeddedScaleMultiplier = 0.8f;

        [Tooltip(
            "Falling 동안 미사일이 회전할 총 각도입니다. " +
            "0이면 회전 연출을 하지 않습니다."
        )]
        [SerializeField]
        private float fallingSpinDegrees = 180f;

        [Header("Embedded Explosion / 매립 폭발")]

        [Tooltip(
            "매립 미사일이 기폭될 때의 폭발 반경입니다."
        )]
        [SerializeField, Min(0.05f)]
        private float embeddedExplosionRadius = 2.25f;

        [Tooltip(
            "플레이어가 매립 미사일을 기폭했을 때 보스에게 주는 피해입니다. " +
            "보스 전체 Max HP 기준 비율이며 0.025는 2.5%입니다."
        )]
        [SerializeField, Range(0f, 1f)]
        private float embeddedBossMaxHpDamagePercent = 0.025f;

        [Tooltip(
            "폭발 반경 안의 일반 Monster에게 주는 고정 피해입니다. " +
            "보스 피해와 별도로 적용됩니다."
        )]
        [SerializeField, Min(0f)]
        private float embeddedMonsterDamage = 50f;

        [Tooltip(
            "미사일이 바닥에 박히는 순간 생성할 이펙트입니다. " +
            "비워두어도 동작합니다."
        )]
        [SerializeField]
        private GameObject embeddedLandingEffectPrefab;

        [Tooltip(
            "매립 미사일이 폭발할 때 생성할 이펙트입니다. " +
            "비워두면 기존 Destroy Effect Prefab을 사용합니다."
        )]
        [SerializeField]
        private GameObject embeddedExplosionEffectPrefab;

        [Header("Spawn Position")]

        [Tooltip("보스 중심에서 미사일이 생성될 거리입니다.")]
        [SerializeField]
        private float muzzleOffsetFromBoss = 0.9f;

        [Header("Burst")]

        [Tooltip("1페이즈에서 미사일 부채꼴 발사를 몇 번 반복할지 정합니다.")]
        [SerializeField]
        private int burstCountPhase1 = 1;

        [Tooltip("2페이즈에서 미사일 부채꼴 발사를 몇 번 반복할지 정합니다.")]
        [SerializeField]
        private int burstCountPhase2 = 1;

        [Tooltip("3페이즈에서 미사일 부채꼴 발사를 몇 번 반복할지 정합니다.")]
        [SerializeField]
        private int burstCountPhase3 = 2;

        [Tooltip("Burst Count가 2 이상일 때 다음 발사까지 기다리는 시간입니다.")]
        [SerializeField]
        private float burstInterval = 0.35f;

        [Header("Player Detonation")]

        [Tooltip(
            "체크하면 Embedded 상태의 미사일을 플레이어 침/투사체로 기폭할 수 있습니다. " +
            "Tracking 상태에서는 이 값과 관계없이 플레이어 투사체로 파괴되지 않습니다."
        )]
        [SerializeField]
        private bool destroyByPlayerProjectile = true;

        [Tooltip(
            "매립 미사일을 기폭할 수 있는 플레이어 투사체 레이어입니다. " +
            "테스트 중에는 Everything으로 두면 편합니다."
        )]
        [SerializeField]
        private LayerMask playerProjectileLayerMask = ~0;

        [Tooltip(
            "Trigger 충돌이 누락될 때 보조로 플레이어 투사체를 검사하는 반경입니다."
        )]
        [SerializeField, Min(0f)]
        private float projectileHitCheckRadius = 0.25f;

        [Header("Visual")]

        [Tooltip("체크하면 미사일 스프라이트가 이동 방향을 바라보도록 회전합니다.")]
        [SerializeField]
        private bool rotateToMoveDirection = true;

        [Tooltip(
            "미사일 이미지 기본 방향 보정값입니다. " +
            "오른쪽=0, 위쪽=-90, 아래쪽=90, 왼쪽=180 정도로 조정하세요."
        )]
        [SerializeField]
        private float visualForwardAngleOffset = 0f;

        [Tooltip(
            "Tracking 중 플레이어 충돌 또는 구버전 파괴 시 생성할 이펙트입니다. " +
            "Embedded Explosion Effect가 비어 있으면 매립 기폭에도 사용됩니다."
        )]
        [SerializeField]
        private GameObject destroyEffectPrefab;

        [Header("Visual Sorting")]

        [Tooltip("체크하면 미사일 SpriteRenderer의 Sorting Order를 아래 값으로 강제합니다.")]
        [SerializeField]
        private bool forceMissileSortingOrder = true;

        [Tooltip("미사일의 화면 표시 순서입니다.")]
        [SerializeField]
        private int missileSortingOrder = 560;

        [Header("Debug")]

        [Tooltip("체크하면 미사일 패턴 발사 로그를 출력합니다.")]
        [SerializeField]
        private bool debugShot = false;

        protected override IEnumerator ExecutePattern()
        {
            if (bossController == null ||
                bossController.PlayerCharacter == null)
            {
                yield break;
            }

            int missileCount =
                Mathf.Max(
                    1,
                    GetPhaseMissileCount());

            int burstCount =
                Mathf.Max(
                    1,
                    GetPhaseBurstCount());

            for (int burst = 0;
                 burst < burstCount;
                 burst++)
            {
                FireHomingMissileFan(
                    missileCount);

                if (burst <
                    burstCount - 1)
                {
                    yield return
                        new WaitForSeconds(
                            Mathf.Max(
                                0f,
                                burstInterval));
                }
            }
        }

        private int GetPhaseMissileCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return missileCountPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return missileCountPhase2;
            }

            return missileCountPhase1;
        }

        private int GetPhaseBurstCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return burstCountPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return burstCountPhase2;
            }

            return burstCountPhase1;
        }

        private void FireHomingMissileFan(
            int missileCount)
        {
            if (missilePrefab == null)
            {
                Debug.LogWarning(
                    "[BossHomingMissilePattern] Missile Prefab이 비어 있습니다.",
                    this
                );

                return;
            }

            Character player =
                bossController.PlayerCharacter;

            Vector3 bossPosition =
                bossController.BossCenterPosition;

            Transform playerTarget =
                player.CenterTransform != null
                    ? player.CenterTransform
                    : player.transform;

            Vector2 baseDirection =
                (
                    (Vector2)playerTarget.position -
                    (Vector2)bossPosition
                ).normalized;

            if (baseDirection ==
                Vector2.zero)
            {
                baseDirection =
                    Vector2.right;
            }

            float startAngle =
                -totalSpreadAngle *
                0.5f;

            float angleStep =
                missileCount > 1
                    ? totalSpreadAngle /
                      (missileCount - 1)
                    : 0f;

            for (int i = 0;
                 i < missileCount;
                 i++)
            {
                float angle =
                    startAngle +
                    angleStep * i;

                Vector2 fireDirection =
                    RotateVector(
                        baseDirection,
                        angle);

                Vector3 spawnPosition =
                    bossPosition +
                    (Vector3)(
                        fireDirection.normalized *
                        muzzleOffsetFromBoss);

                SpawnMissile(
                    spawnPosition,
                    fireDirection,
                    i,
                    missileCount);
            }

            if (debugShot)
            {
                Debug.Log(
                    $"[BossHomingMissilePattern] Fired {missileCount} missiles | " +
                    $"Phase={bossController.CurrentPhase}, " +
                    $"EmbeddedMode={enableEmbeddedBehavior}",
                    this
                );
            }
        }

        private void SpawnMissile(
            Vector3 spawnPosition,
            Vector2 fireDirection,
            int currentLaneIndex,
            int totalLaneCount)
        {
            GameObject missileObject =
                Instantiate(
                    missilePrefab,
                    spawnPosition,
                    Quaternion.identity
                );

            ApplyMissileSortingOrder(
                missileObject);

            BossHomingMissile missile =
                missileObject.GetComponent
                <
                    BossHomingMissile
                >();

            if (missile == null)
            {
                missile =
                    missileObject.GetComponentInChildren
                    <
                        BossHomingMissile
                    >(true);
            }

            if (missile == null)
            {
                missile =
                    missileObject.AddComponent
                    <
                        BossHomingMissile
                    >();

                Debug.LogWarning(
                    "[BossHomingMissilePattern] " +
                    "Missile Prefab에 BossHomingMissile이 없어 자동 추가했습니다.",
                    missileObject
                );
            }

            float finalSpeed =
                bossController
                    .GetModifiedProjectileSpeed(
                        missileSpeed);

            float finalDamage =
                bossController
                    .GetModifiedDamage(
                        missileDamage);

            // 기존 Init 시그니처 유지.
            missile.Init(
                bossController.PlayerCharacter,
                fireDirection,
                finalSpeed,
                finalDamage,
                missileLifeTime,
                currentLaneIndex,
                totalLaneCount,
                turnSpeed,
                laneOffsetDistance,
                laneFadeDistance,
                initialSpreadDuration,
                destroyByPlayerProjectile,
                playerProjectileLayerMask,
                projectileHitCheckRadius,
                rotateToMoveDirection,
                visualForwardAngleOffset,
                destroyEffectPrefab
            );

            if (enableEmbeddedBehavior)
            {
                missile.ConfigureEmbeddedBehavior(
                    bossController,
                    trackingDuration,
                    fallingDuration,
                    embeddedLifetime,
                    maxEmbeddedMissiles,
                    embeddedScaleMultiplier,
                    fallingSpinDegrees,
                    embeddedExplosionRadius,
                    embeddedBossMaxHpDamagePercent,
                    embeddedMonsterDamage,
                    embeddedLandingEffectPrefab,
                    embeddedExplosionEffectPrefab
                );
            }
        }

        private void ApplyMissileSortingOrder(
            GameObject missileObject)
        {
            if (!forceMissileSortingOrder ||
                missileObject == null)
            {
                return;
            }

            SpriteRenderer[] renderers =
                missileObject
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

                if (renderer != null)
                {
                    renderer.sortingOrder =
                        missileSortingOrder;
                }
            }
        }

        private Vector2 RotateVector(
            Vector2 vector,
            float angleDegrees)
        {
            float rad =
                angleDegrees *
                Mathf.Deg2Rad;

            float cos =
                Mathf.Cos(rad);

            float sin =
                Mathf.Sin(rad);

            return new Vector2(
                vector.x * cos -
                vector.y * sin,
                vector.x * sin +
                vector.y * cos
            ).normalized;
        }

        private void Reset()
        {
            patternName =
                "Homing Missile";

            cooldown =
                7f;
        }
    }
}

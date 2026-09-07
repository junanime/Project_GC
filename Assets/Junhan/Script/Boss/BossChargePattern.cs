using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 크리피커피 돌진 패턴입니다.
    ///
    /// Step 6B 추가:
    /// - 돌진 경로에서 같은 보스가 생성한 Embedded 호밍 미사일을 감지합니다.
    /// - 충돌하면 BossHomingMissile.TryDetonateByBossCharge()로 기폭합니다.
    /// - 돌진을 즉시 중단하고 남은 연속 돌진도 취소합니다.
    /// - BossCoreDamageStateBridge를 통해 일정 시간 Groggy를 적용합니다.
    ///
    /// 기존 경고선 / 지그재그 / 페이즈별 거리·속도·피해 /
    /// 플레이어 히트 판정 / 물리 충돌 무시 구조는 유지합니다.
    /// </summary>
    public class BossChargePattern : BossPatternBase
    {
        [Header("Charge Telegraph")]

        [Tooltip("돌진 전 경고선을 표시할 프리팹입니다. 비워두면 경고선 없이 돌진합니다.")]
        [SerializeField]
        private GameObject warningLinePrefab;

        [Tooltip("첫 번째 돌진 전에 경고선을 표시한 뒤 실제 돌진하기까지 기다리는 시간입니다.")]
        [SerializeField]
        private float warningTime = 0.9f;

        [Tooltip("경고선의 두께입니다.")]
        [SerializeField]
        private float warningWidth = 1.5f;

        [Tooltip("경고선 색상입니다.")]
        [SerializeField]
        private Color warningColor =
            new Color(0.4f, 0.85f, 1f, 0.45f);

        [Header("Charge Combo")]

        [Tooltip("1페이즈에서 연속으로 돌진하는 횟수입니다.")]
        [SerializeField]
        private int chargeCountPhase1 = 1;

        [Tooltip("2페이즈에서 연속으로 돌진하는 횟수입니다.")]
        [SerializeField]
        private int chargeCountPhase2 = 2;

        [Tooltip("3페이즈에서 연속으로 돌진하는 횟수입니다.")]
        [SerializeField]
        private int chargeCountPhase3 = 3;

        [Tooltip("2페이즈에서 돌진 후 다음 돌진까지의 텀입니다. 이 시간 동안 다음 돌진 경고선이 표시됩니다.")]
        [SerializeField]
        private float betweenChargeDelayPhase2 = 0.5f;

        [Tooltip("3페이즈에서 돌진 후 다음 돌진까지의 텀입니다. 이 시간 동안 다음 돌진 경고선이 표시됩니다.")]
        [SerializeField]
        private float betweenChargeDelayPhase3 = 0.2f;

        [Tooltip("체크하면 매 돌진 직전에 플레이어의 현재 위치를 다시 조준합니다.")]
        [SerializeField]
        private bool reAimBeforeEachCharge = true;

        [Tooltip("2회 이상 연속 돌진할 때 좌우로 살짝 꺾는 각도입니다. 0이면 정확히 플레이어 방향으로만 돌진합니다.")]
        [SerializeField]
        private float zigzagAngle = 12f;

        [Tooltip("체크하면 첫 지그재그 방향을 매번 랜덤으로 정합니다. 꺼두면 왼쪽/오른쪽 순서가 고정됩니다.")]
        [SerializeField]
        private bool randomizeFirstZigzagSide = true;

        [Header("Charge Distance")]

        [Tooltip("1페이즈에서 보스가 무조건 돌진하는 거리입니다. 플레이어와 충돌해도 이 거리만큼 이동합니다.")]
        [SerializeField]
        private float chargeDistancePhase1 = 6f;

        [Tooltip("2페이즈에서 보스가 무조건 돌진하는 거리입니다. 플레이어와 충돌해도 이 거리만큼 이동합니다.")]
        [SerializeField]
        private float chargeDistancePhase2 = 8f;

        [Tooltip("3페이즈에서 보스가 무조건 돌진하는 거리입니다. 플레이어와 충돌해도 이 거리만큼 이동합니다.")]
        [SerializeField]
        private float chargeDistancePhase3 = 9f;

        [Header("Charge Speed")]

        [Tooltip("1페이즈 돌진 기본 속도입니다. 실제 속도는 BossController의 현재 페이즈 Movement Speed Multiplier가 곱해집니다.")]
        [SerializeField]
        private float chargeSpeedPhase1 = 14f;

        [Tooltip("2페이즈 돌진 기본 속도입니다. 실제 속도는 BossController의 현재 페이즈 Movement Speed Multiplier가 곱해집니다.")]
        [SerializeField]
        private float chargeSpeedPhase2 = 18f;

        [Tooltip("3페이즈 돌진 기본 속도입니다. 실제 속도는 BossController의 현재 페이즈 Movement Speed Multiplier가 곱해집니다.")]
        [SerializeField]
        private float chargeSpeedPhase3 = 20f;

        [Header("Charge Damage")]

        [Tooltip("1페이즈 돌진 기본 데미지입니다. 실제 데미지는 BossController의 현재 페이즈 Damage Multiplier가 곱해집니다.")]
        [SerializeField]
        private float chargeDamagePhase1 = 15f;

        [Tooltip("2페이즈 돌진 기본 데미지입니다. 실제 데미지는 BossController의 현재 페이즈 Damage Multiplier가 곱해집니다.")]
        [SerializeField]
        private float chargeDamagePhase2 = 25f;

        [Tooltip("3페이즈 돌진 기본 데미지입니다. 실제 데미지는 BossController의 현재 페이즈 Damage Multiplier가 곱해집니다.")]
        [SerializeField]
        private float chargeDamagePhase3 = 30f;

        [Tooltip("마지막 돌진이 끝난 뒤 보스가 잠깐 멈춰 있는 시간입니다.")]
        [SerializeField]
        private float endLag = 0.4f;

        [Header("Hit Settings")]

        [Tooltip("돌진 중 플레이어를 맞추는 히트박스 크기입니다. X는 돌진 방향 앞뒤 판정 여유, Y는 돌진 경로의 좌우 폭입니다.")]
        [SerializeField]
        private Vector2 hitboxSize =
            new Vector2(1.6f, 1.6f);

        [Tooltip("플레이어가 속한 레이어입니다. 돌진 히트 판정에 사용됩니다. Player 레이어를 지정하세요.")]
        [SerializeField]
        private LayerMask playerLayer;

        [Tooltip("체크하면 한 번의 돌진 패턴 안에서 플레이어가 돌진마다 각각 한 번씩 맞을 수 있습니다.")]
        [SerializeField]
        private bool canHitPlayerOncePerCharge = true;

        [Tooltip("체크하면 돌진 중 보스와 플레이어의 물리 충돌을 잠시 무시합니다. 돌진이 플레이어 몸에 막히지 않게 하려면 켜두세요.")]
        [SerializeField]
        private bool ignorePlayerPhysicsCollisionDuringCharge = true;

        [Tooltip("체크하면 Rigidbody2D.MovePosition이 아니라 위치를 직접 갱신해서 돌진 거리를 강제로 보장합니다.")]
        [SerializeField]
        private bool forceExactChargeMovement = true;

        [Tooltip("체크하면 Scene 뷰에서 돌진 히트박스 크기를 Gizmo로 표시합니다.")]
        [SerializeField]
        private bool showDebugHitbox = false;

        [Header("Embedded Missile Crash / 매립 미사일 돌진 충돌")]

        [Tooltip(
            "체크하면 돌진 중 같은 보스가 생성한 Embedded 호밍 미사일과 충돌할 수 있습니다. " +
            "충돌 시 미사일이 기폭되고 현재 돌진 및 남은 연속 돌진이 즉시 종료됩니다."
        )]
        [SerializeField]
        private bool enableEmbeddedMissileCrash = true;

        [Tooltip(
            "돌진 중 매립 미사일을 검색할 레이어입니다. " +
            "현재 Boss_HomingMissile 레이어가 확정되지 않았다면 Everything으로 두세요."
        )]
        [SerializeField]
        private LayerMask embeddedMissileLayerMask = ~0;

        [Tooltip(
            "매립 미사일을 돌진으로 터뜨릴 때 BossHomingMissile의 기본 BossSelf 피해에 곱할 값입니다. " +
            "Step 6A 기본 2.5%에 2를 곱하면 보스 전체 Max HP의 약 5% 피해입니다."
        )]
        [SerializeField, Min(0f)]
        private float embeddedMissileBossDamageMultiplier = 2f;

        [Tooltip(
            "돌진 히트박스보다 매립 미사일 충돌 판정을 추가로 넓힐 여유값입니다. " +
            "너무 크게 잡으면 실제로 닿기 전에 충돌하므로 0~0.15 정도를 권장합니다."
        )]
        [SerializeField, Min(0f)]
        private float embeddedMissileCrashPadding = 0.1f;

        [Tooltip(
            "매립 미사일에 돌진 충돌한 뒤 보스가 Groggy 상태로 멈춰 있는 시간입니다. " +
            "Groggy 동안 BossPartDamageRules 기본값 기준 Core에 대한 플레이어 피해가 x2가 됩니다."
        )]
        [SerializeField, Min(0f)]
        private float embeddedMissileGroggyDuration = 2f;

        [Tooltip(
            "Groggy 상태를 BossPartDamageRules와 연결하는 BossCoreDamageStateBridge입니다. " +
            "비워 두면 BossController의 현재 오브젝트/부모/최상위 루트 자식에서 자동 탐색합니다."
        )]
        [SerializeField]
        private BossCoreDamageStateBridge coreDamageStateBridge;

        [Header("Debug")]

        [Tooltip("체크하면 돌진 패턴 실행, 속도, 데미지, 매립 미사일 충돌/Groggy 로그를 Console에 출력합니다.")]
        [SerializeField]
        private bool debugCharge = false;

        private readonly List<Collider2D>
            ignoredBossColliders =
                new List<Collider2D>();

        private readonly List<Collider2D>
            ignoredPlayerColliders =
                new List<Collider2D>();

        private bool embeddedMissileCrashTriggered;
        private bool groggyAppliedByThisPattern;

        protected override IEnumerator ExecutePattern()
        {
            if (bossController == null ||
                bossController.PlayerCharacter == null ||
                bossController.IsDead)
            {
                yield break;
            }

            embeddedMissileCrashTriggered = false;
            groggyAppliedByThisPattern = false;

            ResolveCoreDamageStateBridge();

            bossController.SetExternalMovementLock(true);
            bossController.SetSuppressContactDamage(true);

            try
            {
                int chargeCount =
                    Mathf.Max(
                        1,
                        GetPhaseChargeCount());

                float chargeDistance =
                    Mathf.Max(
                        0f,
                        GetPhaseChargeDistance());

                float chargeSpeed =
                    Mathf.Max(
                        0.01f,
                        bossController.GetModifiedMovementSpeed(
                            GetPhaseChargeSpeed()));

                float chargeDamage =
                    bossController.GetModifiedDamage(
                        GetPhaseChargeDamage());

                int firstZigzagSide =
                    randomizeFirstZigzagSide &&
                    Random.value < 0.5f
                        ? -1
                        : 1;

                HashSet<Character> sharedHitTargets =
                    new HashSet<Character>();

                for (int i = 0;
                     i < chargeCount;
                     i++)
                {
                    if (bossController == null ||
                        bossController.PlayerCharacter == null ||
                        bossController.IsDead ||
                        bossController.IsPhaseTransitioning)
                    {
                        break;
                    }

                    Vector2 startPosition =
                        bossController.BossCenterPosition;

                    Vector2 direction =
                        GetChargeDirection(
                            startPosition,
                            i,
                            chargeCount,
                            firstZigzagSide);

                    float telegraphTime =
                        i == 0
                            ? warningTime
                            : GetPhaseBetweenChargeDelay();

                    GameObject warningObject =
                        CreateWarningLine(
                            startPosition,
                            direction,
                            chargeDistance);

                    if (telegraphTime > 0f)
                    {
                        yield return
                            new WaitForSeconds(
                                telegraphTime);
                    }

                    if (warningObject != null)
                    {
                        Destroy(
                            warningObject);
                    }

                    if (bossController == null ||
                        bossController.IsDead ||
                        bossController.IsPhaseTransitioning)
                    {
                        break;
                    }

                    HashSet<Character> hitTargets =
                        canHitPlayerOncePerCharge
                            ? new HashSet<Character>()
                            : sharedHitTargets;

                    if (ignorePlayerPhysicsCollisionDuringCharge)
                    {
                        SetPlayerCollisionIgnore(
                            true);
                    }

                    yield return
                        StartCoroutine(
                            ChargeForward(
                                direction,
                                chargeDistance,
                                chargeSpeed,
                                chargeDamage,
                                hitTargets
                            )
                        );

                    if (ignorePlayerPhysicsCollisionDuringCharge)
                    {
                        SetPlayerCollisionIgnore(
                            false);
                    }

                    if (embeddedMissileCrashTriggered)
                    {
                        if (debugCharge)
                        {
                            Debug.Log(
                                $"[BossChargePattern] " +
                                $"Embedded missile crash -> charge combo aborted | " +
                                $"charge={i + 1}/{chargeCount}",
                                this
                            );
                        }

                        break;
                    }

                    if (debugCharge)
                    {
                        Debug.Log(
                            $"[BossChargePattern] Charge {i + 1}/{chargeCount} complete / " +
                            $"phase={bossController.CurrentPhase}, " +
                            $"distance={chargeDistance}, " +
                            $"speed={chargeSpeed}, " +
                            $"damage={chargeDamage}",
                            this
                        );
                    }
                }

                if (embeddedMissileCrashTriggered)
                {
                    yield return
                        WaitForEmbeddedMissileGroggy();
                }
                else if (endLag > 0f)
                {
                    yield return
                        new WaitForSeconds(
                            endLag);
                }
            }
            finally
            {
                SetPlayerCollisionIgnore(
                    false);

                ClearGroggyAppliedByThisPattern();

                if (bossController != null)
                {
                    StopBossRigidbody();

                    bossController.SetSuppressContactDamage(
                        false);

                    bossController.SetExternalMovementLock(
                        false);
                }

                embeddedMissileCrashTriggered =
                    false;
            }
        }

        private int GetPhaseChargeCount()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return chargeCountPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return chargeCountPhase2;
            }

            return chargeCountPhase1;
        }

        private float GetPhaseBetweenChargeDelay()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return betweenChargeDelayPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return betweenChargeDelayPhase2;
            }

            return 0f;
        }

        private float GetPhaseChargeDistance()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return chargeDistancePhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return chargeDistancePhase2;
            }

            return chargeDistancePhase1;
        }

        private float GetPhaseChargeSpeed()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return chargeSpeedPhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return chargeSpeedPhase2;
            }

            return chargeSpeedPhase1;
        }

        private float GetPhaseChargeDamage()
        {
            if (bossController.CurrentPhase >= 3)
            {
                return chargeDamagePhase3;
            }

            if (bossController.CurrentPhase == 2)
            {
                return chargeDamagePhase2;
            }

            return chargeDamagePhase1;
        }

        private Vector2 GetChargeDirection(
            Vector2 startPosition,
            int chargeIndex,
            int totalChargeCount,
            int firstZigzagSide)
        {
            Vector2 targetPosition;

            if (reAimBeforeEachCharge &&
                bossController.PlayerCharacter != null)
            {
                targetPosition =
                    bossController.PlayerCharacter
                        .transform.position;
            }
            else
            {
                targetPosition =
                    startPosition +
                    Vector2.right;
            }

            Vector2 direction =
                (targetPosition -
                 startPosition).normalized;

            if (direction == Vector2.zero)
            {
                direction =
                    Vector2.right;
            }

            if (totalChargeCount > 1 &&
                Mathf.Abs(zigzagAngle) > 0.01f)
            {
                int side =
                    chargeIndex % 2 == 0
                        ? firstZigzagSide
                        : -firstZigzagSide;

                direction =
                    RotateVector(
                        direction,
                        zigzagAngle * side);
            }

            return direction.normalized;
        }

        private GameObject CreateWarningLine(
            Vector2 startPosition,
            Vector2 direction,
            float chargeDistance)
        {
            if (warningLinePrefab == null)
            {
                if (debugCharge)
                {
                    Debug.LogWarning(
                        "[BossChargePattern] Warning Line Prefab이 비어 있어 경고선 없이 돌진합니다.",
                        this
                    );
                }

                return null;
            }

            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x) *
                Mathf.Rad2Deg;

            float bossBodyOffset =
                0.9f;

            float visualLength =
                Mathf.Max(
                    0.1f,
                    chargeDistance -
                    bossBodyOffset);

            Vector2 visualStart =
                startPosition +
                direction *
                bossBodyOffset;

            Vector2 centerPosition =
                visualStart +
                direction *
                (visualLength * 0.5f);

            GameObject warning =
                Instantiate(
                    warningLinePrefab,
                    centerPosition,
                    Quaternion.Euler(
                        0f,
                        0f,
                        angle)
                );

            warning.transform.localScale =
                new Vector3(
                    visualLength,
                    warningWidth,
                    1f);

            SpriteRenderer sr =
                warning.GetComponent<SpriteRenderer>();

            if (sr == null)
            {
                sr =
                    warning.GetComponentInChildren
                    <
                        SpriteRenderer
                    >();
            }

            if (sr != null)
            {
                sr.color =
                    warningColor;

                sr.sortingOrder =
                    100;
            }

            return warning;
        }

        private IEnumerator ChargeForward(
            Vector2 direction,
            float chargeDistance,
            float chargeSpeed,
            float chargeDamage,
            HashSet<Character> hitTargets)
        {
            Rigidbody2D rb =
                bossController.Rigidbody;

            float traveledDistance =
                0f;

            Vector2 chargeStartPosition =
                rb != null
                    ? rb.position
                    : (Vector2)
                      bossController.transform.position;

            Vector2 exactEndPosition =
                chargeStartPosition +
                direction *
                chargeDistance;

            Vector2 previousPosition =
                chargeStartPosition;

            while (
                traveledDistance <
                    chargeDistance &&
                bossController != null &&
                !bossController.IsDead &&
                !bossController.IsPhaseTransitioning)
            {
                float remainingDistance =
                    chargeDistance -
                    traveledDistance;

                float step =
                    Mathf.Min(
                        chargeSpeed *
                        Time.fixedDeltaTime,
                        remainingDistance);

                if (step <= 0f)
                {
                    break;
                }

                Vector2 currentPosition =
                    rb != null
                        ? rb.position
                        : (Vector2)
                          bossController.transform.position;

                Vector2 nextPosition =
                    currentPosition +
                    direction *
                    step;

                // --------------------------------------------------
                // Step 6B:
                // 이동하기 전에 현재 FixedUpdate 이동 구간을 Sweep 검사합니다.
                //
                // 미사일을 먼저 감지하므로 같은 구간 안에 플레이어가 있어도
                // 매립 미사일 충돌이 성립하면 돌진 플레이어 피해보다
                // 충돌/정지가 우선합니다.
                // --------------------------------------------------
                BossHomingMissile embeddedMissile =
                    FindEmbeddedMissileOnChargePath(
                        currentPosition,
                        nextPosition);

                if (embeddedMissile != null)
                {
                    Vector2 crashPosition =
                        CalculateEmbeddedMissileCrashPosition(
                            currentPosition,
                            nextPosition,
                            embeddedMissile);

                    ForceMoveBossTo(
                        crashPosition);

                    StopBossRigidbody();

                    bool detonated =
                        embeddedMissile.TryDetonateByBossCharge(
                            embeddedMissileBossDamageMultiplier);

                    if (detonated)
                    {
                        embeddedMissileCrashTriggered =
                            true;

                        ApplyGroggyFromEmbeddedMissileCrash();

                        if (debugCharge)
                        {
                            Debug.Log(
                                $"[BossChargePattern] " +
                                $"Embedded missile hit | " +
                                $"BossDamageMultiplier=x{embeddedMissileBossDamageMultiplier:0.##}, " +
                                $"Groggy={embeddedMissileGroggyDuration:0.##}s, " +
                                $"Position={crashPosition}",
                                this
                            );
                        }

                        yield break;
                    }
                }

                if (forceExactChargeMovement)
                {
                    ForceMoveBossTo(
                        nextPosition);
                }
                else
                {
                    if (rb != null)
                    {
                        rb.MovePosition(
                            nextPosition);
                    }
                    else
                    {
                        bossController.transform.position =
                            nextPosition;
                    }
                }

                CheckChargePathHit(
                    previousPosition,
                    nextPosition,
                    chargeDamage,
                    hitTargets);

                previousPosition =
                    nextPosition;

                traveledDistance +=
                    step;

                yield return
                    new WaitForFixedUpdate();
            }

            if (bossController != null &&
                !bossController.IsDead &&
                !bossController.IsPhaseTransitioning &&
                forceExactChargeMovement &&
                !embeddedMissileCrashTriggered)
            {
                Vector2 currentPosition =
                    rb != null
                        ? rb.position
                        : (Vector2)
                          bossController.transform.position;

                if (Vector2.Distance(
                        currentPosition,
                        exactEndPosition) >
                    0.001f)
                {
                    BossHomingMissile embeddedMissile =
                        FindEmbeddedMissileOnChargePath(
                            currentPosition,
                            exactEndPosition);

                    if (embeddedMissile != null)
                    {
                        Vector2 crashPosition =
                            CalculateEmbeddedMissileCrashPosition(
                                currentPosition,
                                exactEndPosition,
                                embeddedMissile);

                        ForceMoveBossTo(
                            crashPosition);

                        StopBossRigidbody();

                        bool detonated =
                            embeddedMissile.TryDetonateByBossCharge(
                                embeddedMissileBossDamageMultiplier);

                        if (detonated)
                        {
                            embeddedMissileCrashTriggered =
                                true;

                            ApplyGroggyFromEmbeddedMissileCrash();

                            if (debugCharge)
                            {
                                Debug.Log(
                                    "[BossChargePattern] " +
                                    "Embedded missile hit during exact-end correction.",
                                    this
                                );
                            }

                            yield break;
                        }
                    }

                    ForceMoveBossTo(
                        exactEndPosition);

                    CheckChargePathHit(
                        currentPosition,
                        exactEndPosition,
                        chargeDamage,
                        hitTargets);
                }
            }

            StopBossRigidbody();
        }

        /// <summary>
        /// 현재 돌진 이동 구간에 같은 보스 소유의 Embedded 미사일이 있는지 찾습니다.
        /// 여러 개가 겹치면 진행 방향상 가장 먼저 만나는 미사일을 선택합니다.
        /// </summary>
        private BossHomingMissile FindEmbeddedMissileOnChargePath(
            Vector2 fromPosition,
            Vector2 toPosition)
        {
            if (!enableEmbeddedMissileCrash ||
                bossController == null)
            {
                return null;
            }

            Vector2 segment =
                toPosition -
                fromPosition;

            float segmentLength =
                segment.magnitude;

            Vector2 direction =
                segmentLength > 0.001f
                    ? segment / segmentLength
                    : Vector2.right;

            Vector2 center =
                segmentLength > 0.001f
                    ? (fromPosition + toPosition) * 0.5f
                    : fromPosition;

            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x) *
                Mathf.Rad2Deg;

            Vector2 searchSize =
                new Vector2(
                    Mathf.Max(
                        0.05f,
                        segmentLength +
                        hitboxSize.x +
                        embeddedMissileCrashPadding *
                        2f),
                    Mathf.Max(
                        0.05f,
                        hitboxSize.y +
                        embeddedMissileCrashPadding *
                        2f)
                );

            Collider2D[] hits =
                Physics2D.OverlapBoxAll(
                    center,
                    searchSize,
                    angle,
                    embeddedMissileLayerMask
                );

            BossHomingMissile nearestMissile =
                null;

            float nearestForwardDistance =
                float.MaxValue;

            HashSet<int> visitedMissiles =
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

                BossHomingMissile missile =
                    hit.GetComponentInParent
                    <
                        BossHomingMissile
                    >(true);

                if (missile == null)
                {
                    missile =
                        hit.GetComponent
                        <
                            BossHomingMissile
                        >();
                }

                if (missile == null)
                {
                    missile =
                        hit.GetComponentInChildren
                        <
                            BossHomingMissile
                        >(true);
                }

                if (missile == null ||
                    !missile.IsEmbedded)
                {
                    continue;
                }

                // 다른 보스의 매립 미사일은 무시합니다.
                if (missile.OwnerBossController !=
                    bossController)
                {
                    continue;
                }

                int missileId =
                    missile.gameObject
                        .GetInstanceID();

                if (!visitedMissiles.Add(
                        missileId))
                {
                    continue;
                }

                float forwardDistance;

                if (segmentLength > 0.001f)
                {
                    forwardDistance =
                        Vector2.Dot(
                            (Vector2)
                            missile.transform.position -
                            fromPosition,
                            direction);

                    forwardDistance =
                        Mathf.Clamp(
                            forwardDistance,
                            0f,
                            segmentLength);
                }
                else
                {
                    forwardDistance =
                        Vector2.Distance(
                            fromPosition,
                            missile.transform.position);
                }

                if (forwardDistance <
                    nearestForwardDistance)
                {
                    nearestForwardDistance =
                        forwardDistance;

                    nearestMissile =
                        missile;
                }
            }

            return nearestMissile;
        }

        /// <summary>
        /// 미사일 중심까지 그대로 이동하지 않고,
        /// 돌진 히트박스 앞쪽이 미사일에 닿는 정도에서 멈추게 합니다.
        /// </summary>
        private Vector2 CalculateEmbeddedMissileCrashPosition(
            Vector2 fromPosition,
            Vector2 toPosition,
            BossHomingMissile missile)
        {
            if (missile == null)
            {
                return fromPosition;
            }

            Vector2 segment =
                toPosition -
                fromPosition;

            float segmentLength =
                segment.magnitude;

            if (segmentLength <= 0.001f)
            {
                return fromPosition;
            }

            Vector2 direction =
                segment /
                segmentLength;

            float missileDistanceOnPath =
                Vector2.Dot(
                    (Vector2)
                    missile.transform.position -
                    fromPosition,
                    direction);

            float bossFrontHalfLength =
                Mathf.Max(
                    0f,
                    hitboxSize.x * 0.5f +
                    embeddedMissileCrashPadding);

            float stopDistance =
                Mathf.Clamp(
                    missileDistanceOnPath -
                    bossFrontHalfLength,
                    0f,
                    segmentLength);

            return
                fromPosition +
                direction *
                stopDistance;
        }

        private void ApplyGroggyFromEmbeddedMissileCrash()
        {
            ResolveCoreDamageStateBridge();

            if (coreDamageStateBridge == null)
            {
                Debug.LogWarning(
                    "[BossChargePattern] " +
                    "매립 미사일 돌진 충돌은 발생했지만 " +
                    "BossCoreDamageStateBridge를 찾지 못해 Groggy/Core x2를 적용할 수 없습니다. " +
                    "BossPartDamageTestRoot 또는 BossController 루트의 Bridge 배치를 확인하세요.",
                    this
                );

                return;
            }

            coreDamageStateBridge.SetGroggy(
                true);

            groggyAppliedByThisPattern =
                true;
        }

        private IEnumerator WaitForEmbeddedMissileGroggy()
        {
            if (!groggyAppliedByThisPattern ||
                embeddedMissileGroggyDuration <= 0f)
            {
                ClearGroggyAppliedByThisPattern();
                yield break;
            }

            float endTime =
                Time.time +
                embeddedMissileGroggyDuration;

            while (Time.time < endTime)
            {
                if (bossController == null ||
                    bossController.IsDead ||
                    bossController.IsPhaseTransitioning)
                {
                    break;
                }

                StopBossRigidbody();

                yield return null;
            }

            ClearGroggyAppliedByThisPattern();
        }

        private void ClearGroggyAppliedByThisPattern()
        {
            if (!groggyAppliedByThisPattern)
            {
                return;
            }

            if (coreDamageStateBridge != null)
            {
                coreDamageStateBridge.SetGroggy(
                    false);
            }

            groggyAppliedByThisPattern =
                false;
        }

        private void ResolveCoreDamageStateBridge()
        {
            if (coreDamageStateBridge != null)
            {
                return;
            }

            if (bossController == null)
            {
                return;
            }

            coreDamageStateBridge =
                bossController.GetComponent
                <
                    BossCoreDamageStateBridge
                >();

            if (coreDamageStateBridge == null)
            {
                coreDamageStateBridge =
                    bossController.GetComponentInParent
                    <
                        BossCoreDamageStateBridge
                    >(true);
            }

            if (coreDamageStateBridge == null &&
                bossController.transform.root != null)
            {
                coreDamageStateBridge =
                    bossController.transform.root
                        .GetComponentInChildren
                        <
                            BossCoreDamageStateBridge
                        >(true);
            }
        }

        private void ForceMoveBossTo(
            Vector2 position)
        {
            if (bossController == null)
            {
                return;
            }

            Rigidbody2D rb =
                bossController.Rigidbody;

            if (rb != null)
            {
                rb.velocity =
                    Vector2.zero;

                rb.angularVelocity =
                    0f;

                rb.position =
                    position;
            }

            Vector3 worldPosition =
                bossController.transform.position;

            worldPosition.x =
                position.x;

            worldPosition.y =
                position.y;

            bossController.transform.position =
                worldPosition;

            Physics2D.SyncTransforms();
        }

        private void StopBossRigidbody()
        {
            if (bossController == null)
            {
                return;
            }

            Rigidbody2D rb =
                bossController.Rigidbody;

            if (rb == null)
            {
                return;
            }

            rb.velocity =
                Vector2.zero;

            rb.angularVelocity =
                0f;
        }

        private void CheckChargePathHit(
            Vector2 fromPosition,
            Vector2 toPosition,
            float damage,
            HashSet<Character> hitTargets)
        {
            Vector2 segment =
                toPosition -
                fromPosition;

            float segmentLength =
                segment.magnitude;

            if (segmentLength <= 0.001f)
            {
                CheckChargeHitAtPosition(
                    toPosition,
                    damage,
                    hitTargets);

                return;
            }

            Vector2 direction =
                segment.normalized;

            Vector2 center =
                (fromPosition +
                 toPosition) *
                0.5f;

            float angle =
                Mathf.Atan2(
                    direction.y,
                    direction.x) *
                Mathf.Rad2Deg;

            Vector2 boxSize =
                new Vector2(
                    Mathf.Max(
                        hitboxSize.x,
                        segmentLength +
                        hitboxSize.x),
                    Mathf.Max(
                        0.05f,
                        hitboxSize.y)
                );

            Collider2D[] hits =
                Physics2D.OverlapBoxAll(
                    center,
                    boxSize,
                    angle,
                    playerLayer
                );

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                TryDamagePlayerFromCollider(
                    hits[i],
                    damage,
                    hitTargets);
            }
        }

        private void CheckChargeHitAtPosition(
            Vector2 center,
            float damage,
            HashSet<Character> hitTargets)
        {
            Collider2D[] hits =
                Physics2D.OverlapBoxAll(
                    center,
                    hitboxSize,
                    0f,
                    playerLayer
                );

            for (int i = 0;
                 i < hits.Length;
                 i++)
            {
                TryDamagePlayerFromCollider(
                    hits[i],
                    damage,
                    hitTargets);
            }
        }

        private void TryDamagePlayerFromCollider(
            Collider2D hit,
            float damage,
            HashSet<Character> hitTargets)
        {
            if (hit == null ||
                bossController == null)
            {
                return;
            }

            Character character =
                hit.GetComponentInParent
                <
                    Character
                >();

            if (character == null)
            {
                return;
            }

            if (character !=
                bossController.PlayerCharacter)
            {
                return;
            }

            if (hitTargets.Contains(
                    character))
            {
                return;
            }

            hitTargets.Add(
                character);

            character.TakeDamage(
                damage);

            if (debugCharge)
            {
                Debug.Log(
                    $"[BossChargePattern] Player hit by charge / damage={damage}",
                    this
                );
            }
        }

        private void SetPlayerCollisionIgnore(
            bool ignore)
        {
            if (!ignore)
            {
                RestoreIgnoredPlayerCollisions();
                return;
            }

            RestoreIgnoredPlayerCollisions();

            if (bossController == null ||
                bossController.PlayerCharacter == null)
            {
                return;
            }

            Collider2D[] bossColliders =
                bossController
                    .GetComponentsInChildren
                    <
                        Collider2D
                    >(true);

            Collider2D[] playerColliders =
                bossController.PlayerCharacter
                    .GetComponentsInChildren
                    <
                        Collider2D
                    >(true);

            for (int i = 0;
                 i < bossColliders.Length;
                 i++)
            {
                Collider2D bossCollider =
                    bossColliders[i];

                if (bossCollider == null)
                {
                    continue;
                }

                for (int j = 0;
                     j < playerColliders.Length;
                     j++)
                {
                    Collider2D playerCollider =
                        playerColliders[j];

                    if (playerCollider == null)
                    {
                        continue;
                    }

                    if (Physics2D.GetIgnoreCollision(
                            bossCollider,
                            playerCollider))
                    {
                        continue;
                    }

                    Physics2D.IgnoreCollision(
                        bossCollider,
                        playerCollider,
                        true);

                    ignoredBossColliders.Add(
                        bossCollider);

                    ignoredPlayerColliders.Add(
                        playerCollider);
                }
            }
        }

        private void RestoreIgnoredPlayerCollisions()
        {
            int pairCount =
                Mathf.Min(
                    ignoredBossColliders.Count,
                    ignoredPlayerColliders.Count);

            for (int i = 0;
                 i < pairCount;
                 i++)
            {
                Collider2D bossCollider =
                    ignoredBossColliders[i];

                Collider2D playerCollider =
                    ignoredPlayerColliders[i];

                if (bossCollider != null &&
                    playerCollider != null)
                {
                    Physics2D.IgnoreCollision(
                        bossCollider,
                        playerCollider,
                        false);
                }
            }

            ignoredBossColliders.Clear();
            ignoredPlayerColliders.Clear();
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

        private void OnDrawGizmosSelected()
        {
            if (!showDebugHitbox)
            {
                return;
            }

            Gizmos.color =
                Color.cyan;

            Gizmos.matrix =
                Matrix4x4.identity;

            Gizmos.DrawWireCube(
                transform.position,
                hitboxSize);
        }
    }
}

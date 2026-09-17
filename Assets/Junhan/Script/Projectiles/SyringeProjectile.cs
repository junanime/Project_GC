using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    public class SyringeProjectile : Projectile
    {
        private enum NeedleFlightState
        {
            Normal,
            ReturnForwardPass,
            ReturnCurveToPlayer,
            ReturnToPlayer
        }

        private SyringeSpecialRuntime specials;
        private int remainingPierces;
        // 일반 몬스터용 기존 Pierce.

       
        private int remainingBossPierces;
        // 보스 파츠 전용 별도 Pierce.
        // 일반 Pierce / 대물침 무한관통과 절대로 공유하지 않습니다.
        private int remainingReflects;
        // 섬유침 선분 생성용 위치 기록
        private Vector2 fiberSegmentStartPosition;
        private float fiberDistanceSinceLastSegment = 0f;

        // 압력침 거리 계산용 발사 시작 위치
        private Vector2 pressureLaunchPosition;
        // 한 투사체가 이미 맞힌 대상 기록
        private readonly HashSet<int> hitTargetIds = new HashSet<int>();

        // 원본 사거리 저장
        private float baseMaxDistance;

        // 침귀환 상태
        private NeedleFlightState flightState = NeedleFlightState.Normal;
        private float returnTimer = 0f;

        // 침귀환: 적중 후 1회 관통
        private Vector2 returnForwardDirection;
        private float returnForwardTravelled = 0f;

        // 침귀환: 물방울 호 형태 곡선 복귀
        private float returnCurveTimer = 0f;
        private Vector2 returnCurveStart;
        private Vector2 returnCurveControl1;
        private Vector2 returnCurveControl2;
        private Vector2 returnCurveEnd;

        [Header("Needle Flight / 침 비행")]
        [Tooltip("침 스프라이트가 실제 이동 방향과 맞지 않을 때 보정하는 각도입니다. 새 창 이미지처럼 기본 방향이 왼쪽을 향하면 180을 먼저 테스트하세요.")]
        [SerializeField] private float visualForwardAngleOffset = 135f;

        [Tooltip("침귀환 발동 시, 적중 지점에서 기존 방향으로 한 번 더 앞으로 지나가는 거리입니다.")]
        [SerializeField] private float returnNeedleForwardPassDistance = 0.65f;

        [Tooltip("침귀환이 물방울 형태 곡선을 그리며 플레이어 쪽으로 돌아오기까지 걸리는 시간입니다.")]
        [SerializeField] private float returnNeedleCurveDuration = 0.35f;

        [Tooltip("침귀환 곡선 시작 부분이 기존 진행 방향으로 얼마나 길게 뻗을지 정합니다.")]
        [SerializeField] private float returnNeedleCurveForwardHandle = 0.9f;

        [Tooltip("침귀환 곡선이 옆으로 얼마나 크게 휘어질지 정합니다.")]
        [SerializeField] private float returnNeedleCurveSideOffset = 1.2f;

        [Tooltip("침귀환 곡선이 플레이어 쪽으로 얼마나 강하게 당겨질지 정합니다.")]
        [SerializeField] private float returnNeedleCurveReturnPull = 0.7f;

        [Header("Launch Offset / 발사 위치 보정")]
        [Tooltip("체크하면 침이 플레이어 중심이 아니라 발사 방향 앞쪽에서 시작합니다.")]
        [SerializeField] private bool useLaunchForwardOffset = true;

        [Tooltip("플레이어 중심에서 발사 방향으로 얼마나 앞에서 침이 생성될지 정합니다.")]
        [SerializeField] private float launchForwardOffset = 0.45f;

        [Tooltip("발사 위치 보정 직후 TrailRenderer 잔상을 초기화합니다. 투사체 풀링 사용 시 잔상이 튀는 것을 방지합니다.")]
        [SerializeField] private bool clearTrailsAfterLaunchOffset = true;

        private bool hasAppliedLaunchForwardOffset = false;

        [Header("Stuck Needle Visual / 꽂힌 침 연출")]
        [SerializeField] private bool enableStuckNeedleVisual = true;
        [SerializeField] private float stuckNeedleLifetime = 2.5f;
        [SerializeField] private float stuckNeedleScaleMultiplier = 0.85f;
        [SerializeField, Range(0f, 1f)] private float stuckNeedleAlpha = 0.9f;
        [SerializeField] private int stuckNeedleSortingOrderBonus = 5;

        [Header("Explosion Visuals")]
        [SerializeField] private GameObject explosionEffectPrefab;

        private static bool hasWarnedHealMethodMissing = false;

        private bool IsReturnMode =>
            flightState == NeedleFlightState.ReturnForwardPass ||
            flightState == NeedleFlightState.ReturnCurveToPlayer ||
            flightState == NeedleFlightState.ReturnToPlayer;

        protected override void Awake()
        {
            base.Awake();
            baseMaxDistance = maxDistance;
        }

        public override void Setup(
            int projectileIndex,
            Vector2 position,
            float damage,
            float knockback,
            float speed,
            LayerMask targetLayer)
        {
            base.Setup(projectileIndex, position, damage, knockback, speed, targetLayer);

            specials = default;
            remainingPierces = 0;
            remainingBossPierces = 0;

            remainingReflects = 0;
            hitTargetIds.Clear();
            fiberSegmentStartPosition = position;
            fiberDistanceSinceLastSegment = 0f;
            pressureLaunchPosition = position;
            hasAppliedLaunchForwardOffset = false;

            flightState = NeedleFlightState.Normal;
            returnTimer = 0f;
            returnForwardTravelled = 0f;
            returnCurveTimer = 0f;

            maxDistance = baseMaxDistance;
        }

        public void ConfigureSpecials(
    SyringeSpecialRuntime runtime)
        {
            specials = runtime;

            remainingReflects =
                runtime.reflectCount;

            // --------------------------------------------------
            // 일반 Pierce
            // --------------------------------------------------
            if (runtime.pierceEnabled)
            {
                remainingPierces =
                    Mathf.Max(
                        0,
                        runtime.pierceCount
                    );
            }
            else
            {
                remainingPierces = 0;
            }

            // --------------------------------------------------
            // Boss Pierce
            //
            // 일반 Pierce와 완전히 독립입니다.
            // 대물침의 int.MaxValue 관통도 이 값에는 영향을 주지 않습니다.
            // --------------------------------------------------
            remainingBossPierces =
                BossPierceRuntime.GetBossPierceCount(
                    playerCharacter
                );
        }

        public override void Launch(Vector2 direction)
        {
            if (direction == Vector2.zero)
            {
                direction = Vector2.right;
            }

            this.direction = direction.normalized;

            ApplyLaunchForwardOffsetIfNeeded();
            ApplyVisualRotationToDirection(this.direction);

            moveCoroutine = StartCoroutine(Move());
        }

        private void ApplyLaunchForwardOffsetIfNeeded()
        {
            if (!useLaunchForwardOffset)
            {
                return;
            }

            if (hasAppliedLaunchForwardOffset)
            {
                return;
            }

            if (direction == Vector2.zero)
            {
                return;
            }

            float offset = Mathf.Max(0f, launchForwardOffset);
            if (offset <= 0f)
            {
                hasAppliedLaunchForwardOffset = true;
                return;
            }

            transform.position += (Vector3)(direction.normalized * offset);
            hasAppliedLaunchForwardOffset = true;

            if (clearTrailsAfterLaunchOffset)
            {
                ClearTrailRenderers();
            }
        }

        private void ClearTrailRenderers()
        {
            TrailRenderer[] trailRenderers = GetComponentsInChildren<TrailRenderer>(true);

            for (int i = 0; i < trailRenderers.Length; i++)
            {
                if (trailRenderers[i] != null)
                {
                    trailRenderers[i].Clear();
                }
            }
        }

        public override IEnumerator Move()
        {
            float distanceTravelled = 0f;
            float effectiveMaxDistance = maxDistance + specials.rangeBonus;

            while (speed > 0f && !isDespawning)
            {
                switch (flightState)
                {
                    case NeedleFlightState.Normal:
                        if (distanceTravelled >= effectiveMaxDistance)
                        {
                            if (specials.returnNeedleEnabled)
                            {
                                BeginReturnNeedleFromRangeEnd(direction);
                                break;
                            }

                            HitNothing();
                            yield break;
                        }

                        if (specials.homingEnabled)
                        {
                            UpdateHomingDirection();
                        }

                        Vector2 previousPosition = transform.position;

                        float normalStep = speed * Time.deltaTime;
                        transform.position += normalStep * (Vector3)direction;
                        distanceTravelled += normalStep;

                        TrySpawnFiberTrailSegment(previousPosition, transform.position);

                        ApplyVisualRotationToDirection(direction);

                        speed -= airResistance * Time.deltaTime;
                        break;

                    case NeedleFlightState.ReturnForwardPass:
                        MoveReturnForwardPass();
                        break;

                    case NeedleFlightState.ReturnCurveToPlayer:
                        MoveReturnCurveToPlayer();
                        break;

                    case NeedleFlightState.ReturnToPlayer:
                        if (!MoveReturnToPlayer())
                        {
                            yield break;
                        }
                        break;
                }

                yield return null;
            }

            if (!isDespawning && specials.returnNeedleEnabled && flightState == NeedleFlightState.Normal)
            {
                BeginReturnNeedleFromRangeEnd(direction);

                while (!isDespawning &&
                       flightState != NeedleFlightState.Normal &&
                       speed > 0f)
                {
                    switch (flightState)
                    {
                        case NeedleFlightState.ReturnCurveToPlayer:
                            MoveReturnCurveToPlayer();
                            break;

                        case NeedleFlightState.ReturnToPlayer:
                            if (!MoveReturnToPlayer())
                            {
                                yield break;
                            }
                            break;

                        case NeedleFlightState.ReturnForwardPass:
                            MoveReturnForwardPass();
                            break;
                    }

                    yield return null;
                }
            }
            else
            {
                HitNothing();
            }
        }

        private void UpdateHomingDirection()
        {
            Transform target = FindClosestTarget();

            if (target == null)
            {
                return;
            }

            Vector2 desiredDirection =
                ((Vector2)target.position - (Vector2)transform.position).normalized;

            direction = Vector2.Lerp(
                direction,
                desiredDirection,
                specials.homingLerpSpeed * Time.deltaTime
            ).normalized;
        }

        private void ApplyVisualRotationToDirection(Vector2 moveDirection)
        {
            if (moveDirection == Vector2.zero)
            {
                return;
            }

            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angle + visualForwardAngleOffset);
        }

        // =====================================================================
        // 📦 특수 효과 아이템 함수 모음집 (기존 효과 복구 및 정렬 완료)
        // =====================================================================
        private void ApplySlow(Component damageableComponent)
        {
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null) return;

            SlowStatus slowStatus = monster.GetComponent<SlowStatus>();
            if (slowStatus == null)
            {
                slowStatus = monster.gameObject.AddComponent<SlowStatus>();
            }

            slowStatus.Apply(3f, 0.4f);
        }

        private void ApplyBurn(Component damageableComponent)
        {
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null) return;

            BurnStatus burnStatus = monster.GetComponent<BurnStatus>();
            if (burnStatus == null)
            {
                burnStatus = monster.gameObject.AddComponent<BurnStatus>();
            }

            burnStatus.Apply(3f, 0.5f, 3f);
        }

        private void ApplyPoison(Component damageableComponent)
        {
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null) return;

            PoisonStatus poisonStatus = monster.GetComponent<PoisonStatus>();
            if (poisonStatus == null)
            {
                poisonStatus = monster.gameObject.AddComponent<PoisonStatus>();
            }

            poisonStatus.Apply(
                specials.poisonDuration,
                specials.poisonTickInterval,
                specials.poisonTickDamage
            );
        }

        private void ApplyHoneySlow(Component damageableComponent)
        {
            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null) return;

            HoneySlowStatus honeySlowStatus = monster.GetComponent<HoneySlowStatus>();
            if (honeySlowStatus == null)
            {
                honeySlowStatus = monster.gameObject.AddComponent<HoneySlowStatus>();
            }

            honeySlowStatus.Apply(
                specials.honeyDuration,
                specials.honeySlowMultiplier
            );
        }
        private void TrySpawnFiberTrailSegment(Vector2 previousPosition, Vector2 currentPosition)
        {
            if (!specials.fiberEnabled)
            {
                return;
            }

            float movedDistance = Vector2.Distance(previousPosition, currentPosition);

            if (movedDistance <= 0.001f)
            {
                return;
            }

            fiberDistanceSinceLastSegment += movedDistance;

            float minSegmentDistance = Mathf.Max(0.05f, specials.fiberTrailMinSegmentDistance);

            if (fiberDistanceSinceLastSegment < minSegmentDistance)
            {
                return;
            }

            GameObject segmentObject = new GameObject("Fiber Needle Trail Segment");
            FiberTrailSegment segment = segmentObject.AddComponent<FiberTrailSegment>();

            segment.Init(
                fiberSegmentStartPosition,
                currentPosition,
                targetLayer,
                specials.fiberTrailLifetime,
                specials.fiberTrailDamagePerSecond,
                specials.fiberTrailTickInterval,
                specials.fiberTrailWidth,
                specials.fiberTrailColor
            );

            fiberSegmentStartPosition = currentPosition;
            fiberDistanceSinceLastSegment = 0f;
        }

        private float ApplyPressureDamageIfNeeded(float rawDamage)
        {
            if (!specials.pressureEnabled)
            {
                return rawDamage;
            }

            float travelledDistance = Vector2.Distance(pressureLaunchPosition, transform.position);
            float bonus = travelledDistance * Mathf.Max(0f, specials.pressureDamageBonusPerDistance);
            bonus = Mathf.Min(bonus, Mathf.Max(0f, specials.pressureMaxDamageBonus));

            return rawDamage * (1f + bonus);
        }

        private float ApplyCorrosionDamageTakenMultiplier(Component damageableComponent, float currentDamage)
        {
            if (damageableComponent == null)
            {
                return currentDamage;
            }

            CorrosionStatus corrosionStatus =
                damageableComponent.GetComponent<CorrosionStatus>() ??
                damageableComponent.GetComponentInParent<CorrosionStatus>();

            if (corrosionStatus == null)
            {
                return currentDamage;
            }

            return currentDamage * corrosionStatus.GetDamageTakenMultiplier();
        }

        private void ApplyCorrosion(Component damageableComponent)
        {
            if (!specials.corrosionEnabled)
            {
                return;
            }

            if (damageableComponent == null)
            {
                return;
            }

            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            CorrosionStatus corrosionStatus = monster.GetComponent<CorrosionStatus>();

            if (corrosionStatus == null)
            {
                corrosionStatus = monster.gameObject.AddComponent<CorrosionStatus>();
            }

            corrosionStatus.Apply(
                specials.corrosionDuration,
                specials.corrosionDamageTakenBonusPerStack,
                specials.corrosionBossDamageTakenBonusPerStack,
                specials.corrosionMaxStacks,
                IsBossLikeTarget(damageableComponent)
            );
        }

        private bool TryConsumeNeedleMark(Component damageableComponent)
        {
            if (!specials.markEnabled)
            {
                return false;
            }

            if (damageableComponent == null)
            {
                return false;
            }

            NeedleMarkStatus markStatus =
                damageableComponent.GetComponent<NeedleMarkStatus>() ??
                damageableComponent.GetComponentInParent<NeedleMarkStatus>();

            if (markStatus == null)
            {
                return false;
            }

            return markStatus.TryConsume();
        }
        private void ApplyDigestiveAcidSac(Component damageableComponent)
        {
            if (damageableComponent == null)
            {
                return;
            }

            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            if (monster.HP <= 0f)
            {
                return;
            }

            DigestiveAcidSacStatus status = monster.GetComponent<DigestiveAcidSacStatus>();

            if (status == null)
            {
                status = monster.gameObject.AddComponent<DigestiveAcidSacStatus>();
            }

            status.Apply(
                specials.digestiveAcidPuddleLifetime,
                specials.digestiveAcidPuddleRadius,
                specials.digestiveAcidPuddleDamagePerSecond,
                specials.digestiveAcidPuddleTickInterval,
                specials.digestiveAcidPuddleColor
            );
        }

        private void ApplyHungerNeedleHit()
        {
            HungerNeedleRuntime.RegisterHit(
                playerCharacter,
                specials.hungerStackDuration,
                specials.hungerAttackSpeedBonusPerStack,
                specials.hungerMaxStacks,
                specials.debugHungerNeedle
            );
        }

        private void ApplyGutBacteria(Component damageableComponent)
        {
            if (damageableComponent == null)
            {
                return;
            }

            Monster monster = damageableComponent.GetComponent<Monster>() ??
                              damageableComponent.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            if (monster.HP <= 0f)
            {
                return;
            }

            GutBacteriaStatus status = monster.GetComponent<GutBacteriaStatus>();

            if (status == null)
            {
                status = monster.gameObject.AddComponent<GutBacteriaStatus>();
            }

            status.Apply(
                specials.gutBacteriaStackDuration,
                specials.gutBacteriaRequiredStacks,
                specials.gutBacteriaMaxStacks,
                specials.gutBacteriaBonusGemCount,
                specials.gutBacteriaBonusGemType,
                specials.gutBacteriaBonusGemSpawnRadius,
                specials.debugGutBacteria
            );
        }
        private void ApplyNeedleMark(Component damageableComponent)
        {
            if (!specials.markEnabled)
            {
                return;
            }

            if (damageableComponent == null)
            {
                return;
            }

            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            NeedleMarkStatus markStatus = monster.GetComponent<NeedleMarkStatus>();

            if (markStatus == null)
            {
                markStatus = monster.gameObject.AddComponent<NeedleMarkStatus>();
            }

            markStatus.Apply(specials.markDuration);
        }
        private void ApplyMosquitoHeal(Component damageableComponent)
        {
            if (specials.healingBlocked) return;
            if (playerCharacter == null) return;

            float healAmount = specials.mosquitoHealPerHit;

            if (IsBossLikeTarget(damageableComponent))
            {
                healAmount *= specials.mosquitoBossHealMultiplier;
            }

            TryHealPlayer(healAmount);
        }

        private bool IsBossLikeTarget(Component damageableComponent)
        {
            if (damageableComponent == null) return false;

            Component[] components = damageableComponent.GetComponentsInParent<Component>();

            foreach (Component component in components)
            {
                if (component == null) continue;

                string typeName = component.GetType().Name;
                if (typeName.Contains("Boss")) return true;
            }

            string objectName = damageableComponent.gameObject.name;
            return objectName.Contains("Boss") || objectName.Contains("보스");
        }

        private void TryHealPlayer(float healAmount)
        {
            if (playerCharacter == null || healAmount <= 0f) return;

            string[] healMethodNames = { "GainHealth", "Heal", "AddHealth", "RestoreHealth" };
            MethodInfo healMethod = null;

            foreach (string methodName in healMethodNames)
            {
                healMethod = playerCharacter.GetType().GetMethod(
                    methodName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new System.Type[] { typeof(float) },
                    null
                );

                if (healMethod != null) break;
            }

            if (healMethod == null)
            {
                if (!hasWarnedHealMethodMissing)
                {
                    Debug.LogWarning("[모기침] Character에서 체력 회복 메서드를 찾지 못했습니다.");
                    hasWarnedHealMethodMissing = true;
                }
                return;
            }

            healMethod.Invoke(playerCharacter, new object[] { healAmount });
        }

        // =====================================================================
        // 🔍 타겟팅 및 충돌 헬퍼 함수들
        // =====================================================================
        private bool TryGetValidMonsterTarget(Collider2D collider, out Monster monster)
        {
            monster = null;
            if (collider == null) return false;

            monster = collider.GetComponentInParent<Monster>();
            if (monster == null) return false;

            TrapMonster trapMonster = monster as TrapMonster;
            if (trapMonster != null && !trapMonster.IsActive) return false;

            return true;
        }

        private bool IsSameTarget(GameObject originalTarget, Monster monster)
        {
            if (originalTarget == null || monster == null) return false;
            if (originalTarget == monster.gameObject) return true;

            Monster originalMonster = originalTarget.GetComponentInParent<Monster>();
            return originalMonster != null && originalMonster == monster;
        }

        private Transform FindClosestTarget()
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, specials.homingRange);
            float closestDistance = float.MaxValue;
            Transform closestTarget = null;

            foreach (Collider2D hit in hits)
            {
                Monster monster;
                if (!TryGetValidMonsterTarget(hit, out monster)) continue;

                IDamageable damageable = monster as IDamageable;
                if (damageable == null) continue;

                int targetId = monster.gameObject.GetInstanceID();
                if (hitTargetIds.Contains(targetId)) continue;

                Vector2 targetPosition = monster.CenterTransform != null
                    ? (Vector2)monster.CenterTransform.position
                    : (Vector2)monster.transform.position;

                float distance = Vector2.Distance(transform.position, targetPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = monster.CenterTransform != null ? monster.CenterTransform : monster.transform;
                }
            }

            return closestTarget;
        }

        protected override void OnTriggerEnter2D(
    Collider2D collider)
        {
            if (isDespawning ||
                !gameObject.activeInHierarchy)
            {
                return;
            }

            bool isInTargetLayer =
                (
                    targetLayer &
                    (1 << collider.gameObject.layer)
                ) != 0;

            Monster monsterTarget;

            bool isValidMonsterTarget =
                TryGetValidMonsterTarget(
                    collider,
                    out monsterTarget
                );

            if (!isValidMonsterTarget)
            {
                if (monsterTarget != null)
                {
                    return;
                }

                if (!isInTargetLayer)
                {
                    return;
                }
            }

            IDamageable damageable = null;
            Component damageableComponent = null;
            int targetId;

            if (isValidMonsterTarget &&
                monsterTarget != null)
            {
                damageable =
                    monsterTarget as IDamageable;

                damageableComponent =
                    monsterTarget;

                targetId =
                    monsterTarget.gameObject
                        .GetInstanceID();
            }
            else
            {
                damageable =
                    collider.GetComponentInParent
                    <
                        IDamageable
                    >();

                damageableComponent =
                    damageable as Component;

                if (damageableComponent == null)
                {
                    if (!IsReturnMode)
                    {
                        HitNothing();
                    }

                    return;
                }

                targetId =
                    damageableComponent.gameObject
                        .GetInstanceID();
            }

            if (damageable == null ||
                damageableComponent == null)
            {
                if (!IsReturnMode)
                {
                    HitNothing();
                }

                return;
            }

            if (hitTargetIds.Contains(targetId))
            {
                return;
            }

            hitTargetIds.Add(targetId);

            // --------------------------------------------------
            // 이 대상이 테스트용 보스 파츠인지 확인합니다.
            // --------------------------------------------------
            BossPartDamageTestPart bossPartTarget =
                damageableComponent
                    as BossPartDamageTestPart;

            if (bossPartTarget == null)
            {
                bossPartTarget =
                    damageableComponent
                        .GetComponentInParent
                        <
                            BossPartDamageTestPart
                        >(true);
            }

            float rawDamage =
                IsReturnMode
                    ? damage *
                      Mathf.Max(
                          0.01f,
                          specials.returnNeedleDamageMultiplier
                      )
                    : damage;

            Vector3 hitPosition =
                GetProjectileVisualWorldPosition();

            Quaternion hitRotation =
                GetProjectileVisualWorldRotation();

            Vector3 hitScale =
                GetProjectileVisualWorldScale();

            DamageTarget(
                damageable,
                damageableComponent,
                rawDamage
            );

            TryCreateStuckNeedleVisual(
                damageableComponent,
                hitPosition,
                hitRotation,
                hitScale
            );

            // ==================================================
            // ★ BOSS PART 전용 관통 규칙
            //
            // 이 블록이 일반 Pierce보다 반드시 먼저 실행되어야 합니다.
            // ==================================================
            if (bossPartTarget != null)
            {
                // ----------------------------------------------
                // Boss Pierce가 남아 있으면
                // 다음 보스 파츠까지 진행할 수 있습니다.
                //
                // 예:
                // Boss Pierce 1
                // LeftArm -> Core
                // ----------------------------------------------
                if (remainingBossPierces > 0)
                {
                    remainingBossPierces--;

                    if (col != null)
                    {
                        StartCoroutine(
                            ReenableColliderNextFrame()
                        );
                    }

                    return;
                }

                // ----------------------------------------------
                // 이미 귀환 중인 침이 또 다른 보스 파츠에
                // 닿았는데 Boss Pierce가 없다면 종료합니다.
                //
                // 따라서
                // Arm -> Core 식의 귀환 관통이 발생하지 않습니다.
                // ----------------------------------------------
                if (IsReturnMode)
                {
                    DestroyProjectile();
                    return;
                }

                // ----------------------------------------------
                // 구강청결제 반사는 Pierce가 아니므로
                // 기존 순서를 최대한 유지합니다.
                //
                // 보스 파츠를 뚫는 것이 아니라
                // 맞은 위치에서 다른 방향으로 튕겨나갑니다.
                // ----------------------------------------------
                if (remainingReflects > 0)
                {
                    remainingReflects--;

                    if (TryReflect(direction))
                    {
                        return;
                    }
                }

                // ----------------------------------------------
                // 귀환침
                //
                // 기존 BeginReturnNeedleSequence는
                // 적중 후 앞으로 0.65 정도 더 진행한 뒤
                // 곡선 복귀하기 때문에
                // Arm 뒤 Core까지 맞힐 가능성이 있습니다.
                //
                // 보스에서는 Forward Pass를 생략하고
                // 즉시 곡선 귀환으로 전환합니다.
                // ----------------------------------------------
                if (specials.returnNeedleEnabled)
                {
                    BeginReturnNeedleFromRangeEnd(
                        direction
                    );

                    if (col != null &&
                        !isDespawning)
                    {
                        StartCoroutine(
                            ReenableColliderNextFrame()
                        );
                    }

                    return;
                }

                // Boss Pierce도 없고
                // 반사/귀환도 없으면 여기서 종료.
                DestroyProjectile();
                return;
            }

            // ==================================================
            // 이하 일반 몬스터:
            // 기존 로직을 그대로 유지합니다.
            // ==================================================

            if (IsReturnMode)
            {
                return;
            }

            bool canPierce =
                specials.pierceEnabled &&
                remainingPierces > 0;

            if (canPierce)
            {
                remainingPierces--;

                if (col != null)
                {
                    StartCoroutine(
                        ReenableColliderNextFrame()
                    );
                }

                return;
            }

            // 구강 청결제 기존 반사 로직.
            if (remainingReflects > 0)
            {
                remainingReflects--;

                if (TryReflect(direction))
                {
                    return;
                }
            }

            if (specials.returnNeedleEnabled)
            {
                BeginReturnNeedleSequence(
                    monsterTarget,
                    direction
                );

                return;
            }

            DestroyProjectile();
        }

        private Vector3 GetProjectileVisualWorldPosition()
        {
            return projectileSpriteRenderer != null ? projectileSpriteRenderer.transform.position : transform.position;
        }

        private Quaternion GetProjectileVisualWorldRotation()
        {
            return projectileSpriteRenderer != null ? projectileSpriteRenderer.transform.rotation : transform.rotation;
        }

        private Vector3 GetProjectileVisualWorldScale()
        {
            return projectileSpriteRenderer != null ? projectileSpriteRenderer.transform.lossyScale : transform.lossyScale;
        }

        private void BeginReturnNeedleSequence(Monster hitMonster, Vector2 currentMoveDirection)
        {
            if (playerCharacter == null || playerCharacter.CenterTransform == null)
            {
                DestroyProjectile();
                return;
            }

            if (currentMoveDirection == Vector2.zero)
            {
                currentMoveDirection = direction;
                if (currentMoveDirection == Vector2.zero) currentMoveDirection = Vector2.right;
            }

            returnForwardDirection = currentMoveDirection.normalized;
            returnForwardTravelled = 0f;
            returnTimer = 0f;
            returnCurveTimer = 0f;
            flightState = NeedleFlightState.ReturnForwardPass;

            if (col != null) StartCoroutine(ReenableColliderNextFrame());
        }

        private void BeginReturnNeedleFromRangeEnd(Vector2 currentMoveDirection)
        {
            if (playerCharacter == null || playerCharacter.CenterTransform == null)
            {
                HitNothing();
                return;
            }

            if (currentMoveDirection == Vector2.zero)
            {
                currentMoveDirection = direction;
                if (currentMoveDirection == Vector2.zero) currentMoveDirection = Vector2.right;
            }

            returnForwardDirection = currentMoveDirection.normalized;
            returnForwardTravelled = 0f;
            returnTimer = 0f;
            returnCurveTimer = 0f;

            BeginReturnCurveToPlayer();
        }

        private void MoveReturnForwardPass()
        {
            returnTimer += Time.deltaTime;
            if (returnTimer >= Mathf.Max(0.1f, specials.returnNeedleMaxDuration))
            {
                HitNothing();
                return;
            }

            float step = speed * Time.deltaTime;
            transform.position += step * (Vector3)returnForwardDirection;
            returnForwardTravelled += step;

            direction = returnForwardDirection;
            ApplyVisualRotationToDirection(direction);

            if (returnForwardTravelled >= Mathf.Max(0f, returnNeedleForwardPassDistance))
            {
                BeginReturnCurveToPlayer();
            }
        }

        private void BeginReturnCurveToPlayer()
        {
            if (playerCharacter == null || playerCharacter.CenterTransform == null)
            {
                HitNothing();
                return;
            }

            Vector2 start = transform.position;
            Vector2 playerPosition = playerCharacter.CenterTransform.position;
            Vector2 toPlayer = playerPosition - start;

            if (toPlayer.sqrMagnitude <= 0.0001f) toPlayer = -returnForwardDirection;

            Vector2 toPlayerDirection = toPlayer.normalized;
            Vector2 sideA = new Vector2(-returnForwardDirection.y, returnForwardDirection.x).normalized;
            Vector2 sideB = -sideA;
            Vector2 chosenSide = Vector2.Dot(sideA, toPlayerDirection) >= Vector2.Dot(sideB, toPlayerDirection) ? sideA : sideB;

            float sideOffset = Mathf.Max(0.1f, returnNeedleCurveSideOffset);
            float forwardHandle = Mathf.Max(0.05f, returnNeedleCurveForwardHandle);
            float returnPull = Mathf.Max(0.05f, returnNeedleCurveReturnPull);

            returnCurveStart = start;
            returnCurveControl1 = start + returnForwardDirection * forwardHandle;
            returnCurveControl2 = start + chosenSide * sideOffset + toPlayerDirection * returnPull;
            returnCurveEnd = playerPosition;

            returnCurveTimer = 0f;
            flightState = NeedleFlightState.ReturnCurveToPlayer;
        }

        private void MoveReturnCurveToPlayer()
        {
            if (playerCharacter == null || playerCharacter.CenterTransform == null)
            {
                HitNothing();
                return;
            }

            returnTimer += Time.deltaTime;
            if (returnTimer >= Mathf.Max(0.1f, specials.returnNeedleMaxDuration))
            {
                HitNothing();
                return;
            }

            float duration = Mathf.Max(0.05f, returnNeedleCurveDuration);
            returnCurveTimer += Time.deltaTime;

            float t = Mathf.Clamp01(returnCurveTimer / duration);
            float easedT = Mathf.SmoothStep(0f, 1f, t);

            Vector2 previousPosition = transform.position;
            Vector2 nextPosition = CubicBezier(returnCurveStart, returnCurveControl1, returnCurveControl2, returnCurveEnd, easedT);

            transform.position = nextPosition;
            Vector2 moveDirection = nextPosition - previousPosition;

            if (moveDirection.sqrMagnitude > 0.0001f)
            {
                direction = moveDirection.normalized;
                ApplyVisualRotationToDirection(direction);
            }

            if (t >= 1f)
            {
                flightState = NeedleFlightState.ReturnToPlayer;
                if (col != null) StartCoroutine(ReenableColliderNextFrame());
            }
        }

        private Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
        {
            float oneMinusT = 1f - t;
            return oneMinusT * oneMinusT * oneMinusT * p0 +
                   3f * oneMinusT * oneMinusT * t * p1 +
                   3f * oneMinusT * t * t * p2 +
                   t * t * t * p3;
        }

        private bool MoveReturnToPlayer()
        {
            if (playerCharacter == null || playerCharacter.CenterTransform == null)
            {
                HitNothing();
                return false;
            }

            returnTimer += Time.deltaTime;
            if (returnTimer >= Mathf.Max(0.1f, specials.returnNeedleMaxDuration))
            {
                HitNothing();
                return false;
            }

            Vector2 currentPosition = transform.position;
            Vector2 targetPosition = playerCharacter.CenterTransform.position;
            Vector2 toPlayer = targetPosition - currentPosition;

            if (toPlayer.magnitude <= Mathf.Max(0.05f, specials.returnNeedleArriveDistance))
            {
                DestroyProjectile();
                return false;
            }

            direction = toPlayer.normalized;
            float returnStep = speed * Mathf.Max(0.01f, specials.returnNeedleSpeedMultiplier) * Time.deltaTime;
            transform.position += returnStep * (Vector3)direction;
            ApplyVisualRotationToDirection(direction);

            return true;
        }

        private void DamageTarget(
     IDamageable damageable,
     Component damageableComponent,
     float rawDamage)
        {
            PlayerGeneralStatRuntime statRuntime =
                PlayerGeneralStatRuntime.GetOrCreate(
                    playerCharacter
                );

            bool isCritical = false;

            // 압력침:
            // 이동 거리에 따라 기본 피해 증가.
            rawDamage =
                ApplyPressureDamageIfNeeded(
                    rawDamage
                );

            float finalDamage =
                rawDamage;

            if (statRuntime != null)
            {
                finalDamage =
                    statRuntime.CalculateOffensiveDamage(
                        playerCharacter,
                        damageableComponent,
                        rawDamage,
                        out isCritical
                    );
            }

            // 부식침:
            // 기존 부식 스택만큼 피해 증가.
            finalDamage =
                ApplyCorrosionDamageTakenMultiplier(
                    damageableComponent,
                    finalDamage
                );

            // 표식침.
            bool consumedNeedleMark =
                TryConsumeNeedleMark(
                    damageableComponent
                );

            if (consumedNeedleMark)
            {
                finalDamage +=
                    finalDamage *
                    Mathf.Max(
                        0f,
                        specials.markBonusDamageMultiplier
                    );
            }

            float finalKnockback =
                knockback;

            if (statRuntime != null)
            {
                finalKnockback *=
                    statRuntime.KnockbackMultiplier;
            }

            // --------------------------------------------------
            // 보스 파츠 여부 확인.
            // --------------------------------------------------
            BossPartDamageTestPart bossPart =
                damageableComponent
                    as BossPartDamageTestPart;

            if (bossPart == null)
            {
                bossPart =
                    damageableComponent
                        .GetComponentInParent
                        <
                            BossPartDamageTestPart
                        >(true);
            }

            float actualReportedDamage =
                finalDamage;

            if (bossPart != null)
            {
                // --------------------------------------------------
                // 보스 파츠:
                // 명확하게 PlayerProjectile 출처로 전달합니다.
                //
                // Core Open/Groggy 배율 역시
                // BossPartDamageRules에서 여기 들어온 피해에 적용됩니다.
                // --------------------------------------------------
                actualReportedDamage =
                    bossPart.TakeDamageFromSource(
                        finalDamage,
                        finalKnockback * direction,
                        isCritical,
                        BossDamageSourceType.PlayerProjectile
                    );
            }
            else
            {
                // 일반 몬스터:
                // 기존 IDamageable 경로 그대로 유지.
                damageable.TakeDamage(
                    finalDamage,
                    finalKnockback * direction,
                    isCritical
                );
            }

            // 보스 파츠는 Core Open 배율과
            // 남은 HP까지 반영된 실제 피해를 전달합니다.
            OnHitDamageable?.Invoke(
                actualReportedDamage
            );

            if (isCritical)
            {
                Debug.Log(
                    $"[치명타] 침 공격 치명타 발생 | " +
                    $"피해 {actualReportedDamage:0.##}"
                );
            }

            // --------------------------------------------------
            // 기존 특수 기능 로직은 그대로 유지.
            // --------------------------------------------------

            if (specials.poisonEnabled)
            {
                ApplyPoison(
                    damageableComponent
                );
            }

            if (specials.slowChance > 0 &&
                Random.value < specials.slowChance)
            {
                ApplySlow(
                    damageableComponent
                );
            }

            if (specials.burnChance > 0 &&
                UnityEngine.Random.value <
                specials.burnChance)
            {
                ApplyBurn(
                    damageableComponent
                );
            }

            if (specials.honeyEnabled)
            {
                ApplyHoneySlow(
                    damageableComponent
                );
            }

            if (specials.mosquitoEnabled)
            {
                ApplyMosquitoHeal(
                    damageableComponent
                );
            }

            if (specials.corrosionEnabled)
            {
                ApplyCorrosion(
                    damageableComponent
                );
            }

            if (specials.digestiveAcidSacEnabled)
            {
                ApplyDigestiveAcidSac(
                    damageableComponent
                );
            }

            if (specials.hungerNeedleEnabled)
            {
                ApplyHungerNeedleHit();
            }

            if (specials.gutBacteriaEnabled)
            {
                ApplyGutBacteria(
                    damageableComponent
                );
            }

            if (specials.markEnabled &&
                !consumedNeedleMark)
            {
                ApplyNeedleMark(
                    damageableComponent
                );
            }

            if (specials.explosionEnabled &&
                UnityEngine.Random.value <
                specials.explosionChance)
            {
                ApplyExplosion(
                    damageableComponent.gameObject
                );
            }
        }
        private void TryCreateStuckNeedleVisual(Component damageableComponent, Vector3 hitPosition, Quaternion hitRotation, Vector3 hitScale)
        {
            if (!enableStuckNeedleVisual || IsReturnMode || specials.returnNeedleEnabled || specials.pierceEnabled || damageableComponent == null) return;

            Monster monster = damageableComponent.GetComponent<Monster>() ?? damageableComponent.GetComponentInParent<Monster>();
            if (monster == null) return;

            TrapMonster trapMonster = monster as TrapMonster;
            if (trapMonster != null && !trapMonster.IsActive) return;
            if (monster.HP <= 0f) return;
            if (projectileSpriteRenderer == null || projectileSpriteRenderer.sprite == null) return;

            GameObject stuckNeedleObject = new GameObject("Stuck Needle Visual");
            Transform stuckTransform = stuckNeedleObject.transform;

            stuckTransform.position = hitPosition;
            stuckTransform.rotation = hitRotation;
            stuckTransform.localScale = hitScale * stuckNeedleScaleMultiplier;
            stuckTransform.SetParent(monster.transform, true);

            SpriteRenderer renderer = stuckNeedleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = projectileSpriteRenderer.sprite;
            renderer.flipX = projectileSpriteRenderer.flipX;
            renderer.flipY = projectileSpriteRenderer.flipY;

            Color baseColor = projectileSpriteRenderer.color;
            baseColor.a = stuckNeedleAlpha;
            renderer.color = baseColor;

            SpriteRenderer targetRenderer = monster.GetComponentInChildren<SpriteRenderer>();
            if (targetRenderer != null)
            {
                renderer.sortingLayerID = targetRenderer.sortingLayerID;
                renderer.sortingOrder = targetRenderer.sortingOrder + Mathf.Max(5, stuckNeedleSortingOrderBonus);
            }
            else
            {
                renderer.sortingLayerID = projectileSpriteRenderer.sortingLayerID;
                renderer.sortingOrder = projectileSpriteRenderer.sortingOrder + Mathf.Max(5, stuckNeedleSortingOrderBonus);
            }

            StuckNeedleVisual stuckVisual = stuckNeedleObject.AddComponent<StuckNeedleVisual>();
            stuckVisual.Init(monster, stuckNeedleLifetime);
        }

        private IEnumerator ReenableColliderNextFrame()
        {
            if (col == null) yield break;
            col.enabled = false;
            yield return null;
            if (!isDespawning && gameObject.activeInHierarchy) col.enabled = true;
        }

        private void ApplyExplosion(GameObject originalTarget)
        {
            if (explosionEffectPrefab != null)
            {
                Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            }

            Debug.Log($"<color=orange><b>[💥 항생제 폭탄 발동]</b></color> 중심 타겟: {originalTarget.name} | 폭발 반경: {specials.explosionRadius}");

            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, specials.explosionRadius);
            HashSet<int> damagedIds = new HashSet<int>();
            PlayerGeneralStatRuntime statRuntime = PlayerGeneralStatRuntime.GetOrCreate(playerCharacter);

            foreach (Collider2D hit in hits)
            {
                Monster monster;
                if (!TryGetValidMonsterTarget(hit, out monster)) continue;
                if (IsSameTarget(originalTarget, monster)) continue;

                int splashId = monster.gameObject.GetInstanceID();
                if (damagedIds.Contains(splashId)) continue;

                IDamageable splashDamageable = monster as IDamageable;
                Component splashComponent = monster;
                if (splashDamageable == null || splashComponent == null) continue;

                damagedIds.Add(splashId);

                float splashDamage = specials.explosionDamage;
                bool isCritical = false;

                if (statRuntime != null)
                {
                    splashDamage = statRuntime.CalculateOffensiveDamage(playerCharacter, splashComponent, splashDamage, out isCritical);
                }

                splashDamageable.TakeDamage(splashDamage, Vector2.zero);
                Debug.Log($"<color=yellow><b> └ [↳ 💥 스플래시 피해 완수]</b></color> 휩쓸린 몹: {monster.gameObject.name} | 입은 피해: {splashDamage}");
            }
        }

        // =====================================================================
        // 💡 구강 청결제 반사 전용 엔진
        // =====================================================================
        private bool TryReflect(Vector2 currentDirection)
        {
            Transform closestEnemy = FindClosestReflectTarget();

            if (closestEnemy != null)
            {
                Vector2 targetPosition = closestEnemy.position;
                direction = (targetPosition - (Vector2)transform.position).normalized;
            }
            else
            {
                direction = -currentDirection;
            }

            ApplyVisualRotationToDirection(direction);

            if (col != null)
            {
                StartCoroutine(ReenableColliderNextFrame());
            }

            Debug.Log($"<color=lightblue>[구강 청결제]</color> 투사체 반사 발동! 새로운 타겟 방향으로 꺾임.");
            return true;
        }

        private Transform FindClosestReflectTarget()
        {
            float reflectRadius = 4.0f;
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, reflectRadius);
            float closestDistance = float.MaxValue;
            Transform closestTarget = null;

            foreach (Collider2D hit in hits)
            {
                Monster monster;
                if (!TryGetValidMonsterTarget(hit, out monster)) continue;

                int targetId = monster.gameObject.GetInstanceID();
                if (hitTargetIds.Contains(targetId)) continue;

                float distance = Vector2.Distance(transform.position, monster.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = monster.transform;
                }
            }

            return closestTarget;
        }

    } // 👈 SyringeProjectile 클래스 끝

    // =====================================================================
    // StuckNeedleVisual 서브 클래스
    // =====================================================================
    public class StuckNeedleVisual : MonoBehaviour
    {
        private Monster ownerMonster;
        private float lifetime = 2.5f;
        private Coroutine lifetimeCoroutine;

        public void Init(Monster ownerMonster, float lifetime)
        {
            this.ownerMonster = ownerMonster;
            this.lifetime = Mathf.Max(0.05f, lifetime);

            if (this.ownerMonster != null)
            {
                this.ownerMonster.OnKilled.AddListener(OnOwnerKilled);
            }

            lifetimeCoroutine = StartCoroutine(LifetimeRoutine());
        }

        private IEnumerator LifetimeRoutine()
        {
            yield return new WaitForSeconds(lifetime);
            Destroy(gameObject);
        }

        private void OnOwnerKilled(Monster killedMonster)
        {
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (lifetimeCoroutine != null)
            {
                StopCoroutine(lifetimeCoroutine);
                lifetimeCoroutine = null;
            }

            if (ownerMonster != null)
            {
                ownerMonster.OnKilled.RemoveListener(OnOwnerKilled);
            }
        }
    }
}
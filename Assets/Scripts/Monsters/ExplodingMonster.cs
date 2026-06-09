using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 낮은 체력 + 빠른 이동속도 + 접촉 자폭형 몬스터.
    /// 스타크래프트 스커지처럼 플레이어에게 빠르게 접근하고,
    /// 플레이어 근처에 진입하면 현재 궤도를 유지한 채 돌진한다.
    /// 플레이어에게 적중하면 피해를 1회 주고 사망하며,
    /// 빗나가면 일정 시간 후 자동으로 사망한다.
    /// </summary>
    public class ExplodingMonster : Monster
    {
        private ExplodingMonsterBlueprint explodingBlueprint;

        [Header("Scourge Trajectory Lock Settings")]
        [SerializeField]
        [Tooltip("플레이어와 이 거리 안으로 들어오면 더 이상 플레이어를 따라 꺾지 않고 현재 진행 방향을 고정합니다. 기본값은 3입니다.")]
        private float trajectoryLockDistance = 3f;

        [SerializeField]
        [Tooltip("궤도 고정 후 이 시간 안에 플레이어에게 적중하지 못하면 빗나간 것으로 보고 자동 사망 처리합니다. 기본값은 1초입니다.")]
        private float missAutoDeathDelay = 1f;

        [SerializeField]
        [Tooltip("체크하면 궤도 고정, 빗나감 사망, 자폭 성공 로그를 콘솔에 출력합니다. 테스트가 끝나면 꺼도 됩니다.")]
        private bool localDebugLog = false;

        private float timeSinceSpawn = 0f;
        private float lockedTrajectoryTimer = 0f;

        private bool explosionStarted = false;
        private bool explosionDamageApplied = false;
        private bool warningActive = false;

        private bool trajectoryLocked = false;
        private bool missedAutoDeathStarted = false;

        private Vector2 lockedMoveDirection = Vector2.zero;

        private Color originalColor = Color.white;
        private Vector3 originalScale = Vector3.one;

        private Coroutine warningCoroutine;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            explodingBlueprint = monsterBlueprint as ExplodingMonsterBlueprint;

            if (explodingBlueprint == null)
            {
                Debug.LogError(
                    "[ExplodingMonster] ExplodingMonsterBlueprint가 연결되지 않았습니다. " +
                    "자폭 몬스터 프리팹에는 Exploding Monster Blueprint를 사용해야 합니다.",
                    this
                );

                return;
            }

            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            timeSinceSpawn = 0f;
            lockedTrajectoryTimer = 0f;

            explosionStarted = false;
            explosionDamageApplied = false;
            warningActive = false;

            trajectoryLocked = false;
            missedAutoDeathStarted = false;
            lockedMoveDirection = Vector2.zero;

            originalScale = transform.localScale;

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = true;
                originalColor = monsterSpriteRenderer.color;
                monsterSpriteRenderer.color = originalColor;
            }

            if (shadow != null)
            {
                shadow.SetActive(true);
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = true;
            }

            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
                warningCoroutine = null;
            }

            if (ShouldDebugLog())
            {
                Debug.Log(
                    $"[ExplodingMonster] 스폰 완료 | HP={currentHealth:0.##} | " +
                    $"Speed={explodingBlueprint.movespeed:0.##} | Damage={explodingBlueprint.explosionDamage:0.##} | " +
                    $"LockDistance={trajectoryLockDistance:0.##} | MissDeathDelay={missAutoDeathDelay:0.##}",
                    this
                );
            }
        }

        protected override void Update()
        {
            base.Update();

            if (!alive || explosionStarted || missedAutoDeathStarted || playerCharacter == null)
            {
                return;
            }

            timeSinceSpawn += Time.deltaTime;

            UpdateWarningState();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();

            if (!alive || explosionStarted || missedAutoDeathStarted || playerCharacter == null || rb == null)
            {
                return;
            }

            if (timeSinceSpawn < Mathf.Max(0f, explodingBlueprint.armDelay))
            {
                MoveTowardPlayer();
                UpdateGridPosition();
                return;
            }

            TryLockTrajectoryNearPlayer();

            if (trajectoryLocked)
            {
                MoveAlongLockedTrajectory();
                UpdateLockedTrajectoryTimer();
            }
            else
            {
                MoveTowardPlayer();
            }

            UpdateGridPosition();

            // 충돌 이벤트가 누락되거나, 몬스터끼리 겹쳐서 물리 판정이 흔들리는 경우를 대비한 안전장치.
            TryExplodeByDistance();
        }

        private void MoveTowardPlayer()
        {
            if (playerCharacter == null || rb == null || explodingBlueprint == null)
            {
                return;
            }

            Vector2 toPlayer =
                GetPlayerCenterPosition() -
                (Vector2)transform.position;

            if (toPlayer.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Vector2 moveDirection = toPlayer.normalized;

            rb.velocity +=
                moveDirection *
                explodingBlueprint.acceleration *
                Time.fixedDeltaTime;

            ClampVelocityToMaxSpeed();
        }

        private void TryLockTrajectoryNearPlayer()
        {
            if (trajectoryLocked)
            {
                return;
            }

            if (playerCharacter == null || rb == null)
            {
                return;
            }

            float lockDistance = Mathf.Max(0.05f, trajectoryLockDistance);
            float distanceToPlayer = Vector2.Distance(transform.position, GetPlayerCenterPosition());

            if (distanceToPlayer > lockDistance)
            {
                return;
            }

            trajectoryLocked = true;
            lockedTrajectoryTimer = 0f;

            if (rb.velocity.sqrMagnitude > 0.0001f)
            {
                lockedMoveDirection = rb.velocity.normalized;
            }
            else
            {
                Vector2 toPlayer =
                    GetPlayerCenterPosition() -
                    (Vector2)transform.position;

                lockedMoveDirection = toPlayer.sqrMagnitude > 0.0001f
                    ? toPlayer.normalized
                    : Vector2.right;
            }

            float maxSpeed = GetMaxMoveSpeed();
            rb.velocity = lockedMoveDirection * maxSpeed;

            if (ShouldDebugLog())
            {
                Debug.Log(
                    $"[ExplodingMonster] 궤도 고정 시작 | Distance={distanceToPlayer:0.##} | " +
                    $"Direction={lockedMoveDirection} | AutoDeathAfter={missAutoDeathDelay:0.##}s",
                    this
                );
            }
        }

        private void MoveAlongLockedTrajectory()
        {
            if (rb == null)
            {
                return;
            }

            if (lockedMoveDirection.sqrMagnitude <= 0.0001f)
            {
                lockedMoveDirection = Vector2.right;
            }

            rb.velocity = lockedMoveDirection.normalized * GetMaxMoveSpeed();
        }

        private void UpdateLockedTrajectoryTimer()
        {
            if (!trajectoryLocked || explosionStarted || missedAutoDeathStarted)
            {
                return;
            }

            lockedTrajectoryTimer += Time.fixedDeltaTime;

            float deathDelay = Mathf.Max(0.05f, missAutoDeathDelay);

            if (lockedTrajectoryTimer >= deathDelay)
            {
                StartMissedAutoDeath();
            }
        }

        private void ClampVelocityToMaxSpeed()
        {
            if (rb == null)
            {
                return;
            }

            float maxSpeed = GetMaxMoveSpeed();

            if (rb.velocity.magnitude > maxSpeed)
            {
                rb.velocity = rb.velocity.normalized * maxSpeed;
            }
        }

        private float GetMaxMoveSpeed()
        {
            if (explodingBlueprint == null)
            {
                return 0.1f;
            }

            return Mathf.Max(0.1f, explodingBlueprint.movespeed);
        }

        private Vector2 GetPlayerCenterPosition()
        {
            if (playerCharacter == null)
            {
                return transform.position;
            }

            return playerCharacter.CenterTransform != null
                ? (Vector2)playerCharacter.CenterTransform.position
                : (Vector2)playerCharacter.transform.position;
        }

        private void UpdateGridPosition()
        {
            if (entityManager != null && entityManager.Grid != null)
            {
                entityManager.Grid.UpdateClient(this);
            }
        }

        private void UpdateWarningState()
        {
            if (warningActive)
            {
                return;
            }

            if (explodingBlueprint == null || playerCharacter == null)
            {
                return;
            }

            float warningDistance = Mathf.Max(0.1f, explodingBlueprint.warningDistance);

            float distanceToPlayer =
                Vector2.Distance(transform.position, playerCharacter.transform.position);

            if (distanceToPlayer <= warningDistance)
            {
                warningActive = true;
                warningCoroutine = StartCoroutine(WarningBlinkRoutine());

                if (ShouldDebugLog())
                {
                    Debug.Log("[ExplodingMonster] 폭발 경고 상태 진입", this);
                }
            }
        }

        private IEnumerator WarningBlinkRoutine()
        {
            if (monsterSpriteRenderer == null)
            {
                yield break;
            }

            float blinkSpeed = Mathf.Max(1f, explodingBlueprint.warningBlinkSpeed);

            while (alive && !explosionStarted && !missedAutoDeathStarted)
            {
                float t = Mathf.PingPong(Time.time * blinkSpeed, 1f);

                monsterSpriteRenderer.color = Color.Lerp(
                    originalColor,
                    Color.red,
                    t
                );

                yield return null;
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.color = originalColor;
            }
        }

        private void TryExplodeByDistance()
        {
            if (!alive || explosionStarted || missedAutoDeathStarted)
            {
                return;
            }

            if (explodingBlueprint == null || playerCharacter == null)
            {
                return;
            }

            if (timeSinceSpawn < Mathf.Max(0f, explodingBlueprint.armDelay))
            {
                return;
            }

            if (playerCharacter.IsDashing)
            {
                // 대쉬 중에는 자폭 판정을 발생시키지 않는다.
                // 플레이어가 자폭 몬스터를 회피할 수 있게 하기 위한 처리.
                return;
            }

            float radius = Mathf.Max(0.05f, explodingBlueprint.explosionRadius);
            float distance = Vector2.Distance(transform.position, GetPlayerCenterPosition());

            if (distance <= radius)
            {
                StartExplosion(playerCharacter);
            }
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            TryExplodeFromCollider(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            TryExplodeFromCollider(collision.collider);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryExplodeFromCollider(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryExplodeFromCollider(other);
        }

        private void TryExplodeFromCollider(Collider2D other)
        {
            if (!alive || explosionStarted || missedAutoDeathStarted)
            {
                return;
            }

            if (explodingBlueprint == null)
            {
                return;
            }

            if (timeSinceSpawn < Mathf.Max(0f, explodingBlueprint.armDelay))
            {
                return;
            }

            if (other == null)
            {
                return;
            }

            bool isPlayerLayer =
                (explodingBlueprint.playerLayer & (1 << other.gameObject.layer)) != 0;

            if (!isPlayerLayer)
            {
                return;
            }

            Character hitCharacter = other.GetComponentInParent<Character>();

            if (hitCharacter == null)
            {
                return;
            }

            if (hitCharacter != playerCharacter)
            {
                return;
            }

            if (hitCharacter.IsDashing)
            {
                // 대쉬 중에는 피해도, 자폭도 발생하지 않는다.
                return;
            }

            StartExplosion(hitCharacter);
        }

        private void StartExplosion(Character targetCharacter)
        {
            if (!alive || explosionStarted || missedAutoDeathStarted)
            {
                return;
            }

            explosionStarted = true;
            alive = false;

            StopWarningCoroutine();

            DisableCollisionAndMovement();

            ApplyExplosionDamageOnce(targetCharacter);

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            StartCoroutine(ExplosionRoutine());
        }

        private void StartMissedAutoDeath()
        {
            if (!alive || explosionStarted || missedAutoDeathStarted)
            {
                return;
            }

            missedAutoDeathStarted = true;
            alive = false;

            StopWarningCoroutine();

            DisableCollisionAndMovement();

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            if (ShouldDebugLog())
            {
                Debug.Log(
                    $"[ExplodingMonster] 회피 성공 판정으로 자동 사망 | LockedTime={lockedTrajectoryTimer:0.##}",
                    this
                );
            }

            StartCoroutine(MissedAutoDeathRoutine());
        }

        private void StopWarningCoroutine()
        {
            if (warningCoroutine != null)
            {
                StopCoroutine(warningCoroutine);
                warningCoroutine = null;
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.color = originalColor;
            }
        }

        private void DisableCollisionAndMovement()
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;

                // 충돌/트리거가 계속 유지되며 데미지가 반복되는 현상을 막기 위해
                // 자폭 또는 빗나감 사망이 시작되는 즉시 물리 시뮬레이션을 끊는다.
                rb.simulated = false;
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = false;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = false;
            }
        }

        private IEnumerator ExplosionRoutine()
        {
            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.color = Color.red;
                transform.localScale = originalScale * 1.15f;
            }

            yield return new WaitForSeconds(0.08f);

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = false;
            }

            if (shadow != null)
            {
                shadow.SetActive(false);
            }

            if (deathParticles != null)
            {
                float waitTime = Mathf.Max(0.05f, deathParticles.main.duration - 0.08f);
                yield return new WaitForSeconds(waitTime);
            }

            RestoreVisualAndPhysicsForPool();

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            bool countAsPlayerKill =
                explodingBlueprint != null &&
                explodingBlueprint.rewardOnSelfExplosion;

            if (entityManager != null)
            {
                entityManager.DespawnMonster(monsterIndex, this, countAsPlayerKill);
            }

            if (ShouldDebugLog())
            {
                Debug.Log(
                    $"[ExplodingMonster] 자폭 완료 | Reward={countAsPlayerKill}",
                    this
                );
            }
        }

        private IEnumerator MissedAutoDeathRoutine()
        {
            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.color = Color.gray;
                transform.localScale = originalScale * 0.85f;
            }

            yield return new WaitForSeconds(0.08f);

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = false;
            }

            if (shadow != null)
            {
                shadow.SetActive(false);
            }

            if (deathParticles != null)
            {
                float waitTime = Mathf.Max(0.05f, deathParticles.main.duration - 0.08f);
                yield return new WaitForSeconds(waitTime);
            }

            RestoreVisualAndPhysicsForPool();

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            // 빗나가서 스스로 사망한 것이므로 플레이어 처치 보상은 주지 않는다.
            if (entityManager != null)
            {
                entityManager.DespawnMonster(monsterIndex, this, false);
            }

            if (ShouldDebugLog())
            {
                Debug.Log("[ExplodingMonster] 빗나감 자동 사망 완료 | Reward=false", this);
            }
        }

        private void RestoreVisualAndPhysicsForPool()
        {
            transform.localScale = originalScale;

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = true;
                monsterSpriteRenderer.color = originalColor;
            }

            if (shadow != null)
            {
                shadow.SetActive(true);
            }

            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void ApplyExplosionDamageOnce(Character targetCharacter)
        {
            if (explosionDamageApplied)
            {
                return;
            }

            explosionDamageApplied = true;

            if (targetCharacter == null)
            {
                return;
            }

            if (explodingBlueprint == null)
            {
                return;
            }

            if (targetCharacter.IsDashing)
            {
                return;
            }

            Vector2 playerCenter = targetCharacter.CenterTransform != null
                ? (Vector2)targetCharacter.CenterTransform.position
                : (Vector2)targetCharacter.transform.position;

            float radius = Mathf.Max(0.05f, explodingBlueprint.explosionRadius);
            float distance = Vector2.Distance(transform.position, playerCenter);

            if (distance > radius)
            {
                if (ShouldDebugLog())
                {
                    Debug.Log(
                        $"[ExplodingMonster] 자폭은 시작됐지만 플레이어가 폭발 반경 밖입니다. Distance={distance:0.##}, Radius={radius:0.##}",
                        this
                    );
                }

                return;
            }

            Vector2 knockbackDirection =
                (playerCenter - (Vector2)transform.position).normalized;

            if (knockbackDirection == Vector2.zero)
            {
                knockbackDirection = Vector2.right;
            }

            Vector2 knockback =
                knockbackDirection *
                Mathf.Max(0f, explodingBlueprint.explosionKnockback);

            targetCharacter.TakeDamage(
                Mathf.Max(0f, explodingBlueprint.explosionDamage),
                knockback
            );

            if (ShouldDebugLog())
            {
                Debug.Log(
                    $"[ExplodingMonster] 플레이어에게 자폭 피해 1회 적용 | Damage={explodingBlueprint.explosionDamage}",
                    this
                );
            }
        }

        private bool ShouldDebugLog()
        {
            return localDebugLog || (explodingBlueprint != null && explodingBlueprint.debugLog);
        }

        protected override void DropLoot()
        {
            // 플레이어가 직접 처치했을 때는 부모 Monster의 DropLoot를 그대로 사용한다.
            // 자폭 성공/빗나감 자동 사망에서는 각 루틴에서 DropLoot를 호출하지 않으므로 보상이 없다.
            base.DropLoot();
        }
    }
}
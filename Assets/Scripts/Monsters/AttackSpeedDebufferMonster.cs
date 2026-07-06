using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 공격속도 디버퍼 특수 몬스터입니다.
    ///
    /// 기존 RangedMonster는 일반 원거리 몬스터용이라 projectileSpawnPosition 미할당 오류가 나기 쉽고,
    /// SniperMonster처럼 플레이어 기준 거리 보정 스폰을 처리하기 어렵습니다.
    ///
    /// 이 스크립트는 SniperMonster 구조를 참고해서:
    /// 1. 플레이어 기준 일정 거리로 스폰 위치 보정
    /// 2. 발사 위치가 비어 있으면 CenterTransform에서 발사
    /// 3. 공격속도 감소 전용 투사체 발사
    /// 를 담당합니다.
    /// </summary>
    public class AttackSpeedDebufferMonster : Monster
    {
        [Header("Attack Speed Debuffer")]
        [Tooltip("탄환이 발사될 위치입니다. 비워두면 CenterTransform에서 발사합니다.")]
        [SerializeField] private Transform projectileSpawnPosition;

        [Header("Debuffer Debug")]
        [Tooltip("체크하면 공격속도 디버퍼 몬스터 로그를 출력합니다.")]
        [SerializeField] private bool attackSpeedDebufferDebugLog = true;

        private AttackSpeedDebufferMonsterBlueprint debufferBlueprint;
        private int projectileIndex = -1;
        private Coroutine attackCoroutine;

        protected override void Awake()
        {
            base.Awake();
        }

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint incomingBlueprint,
            float hpBuff = 0)
        {
            debufferBlueprint = incomingBlueprint as AttackSpeedDebufferMonsterBlueprint;

            if (debufferBlueprint == null)
            {
                Debug.LogError("[공속감소디버퍼] AttackSpeedDebufferMonsterBlueprint이 아닌 블루프린트가 들어왔습니다.", this);
                return;
            }

            this.monsterIndex = monsterIndex;
            monsterBlueprint = debufferBlueprint;

            Vector2 finalSpawnPosition = GetAdjustedSpawnPosition(position, debufferBlueprint);

            rb.position = finalSpawnPosition;
            transform.position = finalSpawnPosition;

            currentHealth = debufferBlueprint.hp + hpBuff;
            alive = true;

            if (!entityManager.LivingMonsters.Contains(this))
            {
                entityManager.LivingMonsters.Add(this);
            }

            SetupVisualAndHitbox();

            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (debufferBlueprint.freezePositionAfterSpawn)
            {
                rb.constraints =
                    RigidbodyConstraints2D.FreezePositionX |
                    RigidbodyConstraints2D.FreezePositionY |
                    RigidbodyConstraints2D.FreezeRotation;
            }
            else
            {
                rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            }

            StopAttackLoop();

            projectileIndex = -1;

            if (debufferBlueprint.projectilePrefab != null)
            {
                projectileIndex = entityManager.AddPoolForProjectile(debufferBlueprint.projectilePrefab);

                DebugDebuffer(
                    $"탄환 풀 등록 완료 | Projectile={debufferBlueprint.projectilePrefab.name} | PoolIndex={projectileIndex}");
            }
            else
            {
                Debug.LogWarning("[공속감소디버퍼] projectilePrefab이 비어 있습니다. 탄환을 발사할 수 없습니다.", this);
            }

            DebugDebuffer(
                $"스폰 완료 | 원래 위치={position} | 보정 위치={finalSpawnPosition} | HP={currentHealth}");

            attackCoroutine = StartCoroutine(AttackLoop());
        }

        private void SetupVisualAndHitbox()
        {
            bool hasValidAnimation =
                debufferBlueprint.walkSpriteSequence != null &&
                debufferBlueprint.walkSpriteSequence.Length > 0 &&
                debufferBlueprint.walkFrameTime > 0f;

            if (monsterSpriteAnimator != null && hasValidAnimation)
            {
                monsterSpriteAnimator.Init(
                    debufferBlueprint.walkSpriteSequence,
                    debufferBlueprint.walkFrameTime,
                    true);

                monsterSpriteAnimator.StartAnimating(true);
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;

                if (monsterSpriteRenderer != null && monsterSpriteRenderer.sprite != null)
                {
                    monsterHitbox.size = monsterSpriteRenderer.bounds.size;
                    monsterHitbox.offset = Vector2.up * monsterHitbox.size.y / 2f;
                }
                else
                {
                    monsterHitbox.size = Vector2.one;
                    monsterHitbox.offset = Vector2.up * 0.5f;
                }
            }

            if (monsterLegsCollider != null && monsterHitbox != null)
            {
                monsterLegsCollider.radius = Mathf.Max(0.1f, monsterHitbox.size.x / 2.5f);
            }

            if (centerTransform == null)
            {
                centerTransform = new GameObject("Center Transform").transform;
                centerTransform.SetParent(transform);
            }

            Vector3 centerOffset = monsterHitbox != null
                ? (Vector3)monsterHitbox.offset
                : Vector3.zero;

            centerTransform.position = transform.position + centerOffset;
        }

        protected override void Update()
        {
            base.Update();

            if (!alive)
            {
                StopAttackLoop();
            }
        }

        protected override void FixedUpdate()
        {
            if (!alive || rb == null || debufferBlueprint == null)
            {
                return;
            }

            if (debufferBlueprint.freezePositionAfterSpawn)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                return;
            }

            MoveIfTooFarFromPlayer();
        }

        private void MoveIfTooFarFromPlayer()
        {
            if (playerCharacter == null)
            {
                return;
            }

            Vector2 currentPosition = rb.position;
            Vector2 playerPosition = playerCharacter.transform.position;
            Vector2 toPlayer = playerPosition - currentPosition;

            float desiredDistance = Mathf.Max(0.1f, debufferBlueprint.desiredDistanceFromPlayer);

            if (toPlayer.magnitude <= desiredDistance)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            Vector2 direction = toPlayer.normalized;
            float acceleration = monsterBlueprint != null ? monsterBlueprint.acceleration : 4f;

            rb.velocity += direction * acceleration * Time.fixedDeltaTime;
        }

        private Vector2 GetAdjustedSpawnPosition(
            Vector2 originalPosition,
            AttackSpeedDebufferMonsterBlueprint blueprint)
        {
            if (!blueprint.enforceSpawnDistance || playerCharacter == null)
            {
                return originalPosition;
            }

            Vector2 playerPosition = playerCharacter.transform.position;
            Vector2 directionFromPlayer = originalPosition - playerPosition;

            if (directionFromPlayer.sqrMagnitude <= 0.0001f)
            {
                directionFromPlayer = Random.insideUnitCircle.normalized;
            }

            if (directionFromPlayer.sqrMagnitude <= 0.0001f)
            {
                directionFromPlayer = Vector2.right;
            }

            float distance = Mathf.Max(0.1f, blueprint.spawnDistanceFromPlayer);

            return playerPosition + directionFromPlayer.normalized * distance;
        }

        private IEnumerator AttackLoop()
        {
            if (debufferBlueprint == null)
            {
                yield break;
            }

            yield return new WaitForSeconds(Mathf.Max(0f, debufferBlueprint.firstAttackDelay));

            while (alive)
            {
                TryFireProjectileAtPlayer();

                yield return new WaitForSeconds(Mathf.Max(0.05f, debufferBlueprint.attackCooldown));
            }
        }

        private void TryFireProjectileAtPlayer()
        {
            if (projectileIndex < 0 ||
                entityManager == null ||
                debufferBlueprint == null ||
                playerCharacter == null)
            {
                return;
            }

            Vector2 spawnPosition = GetProjectileSpawnWorldPosition();
            Vector2 targetPosition = GetPlayerAimPosition();
            Vector2 direction = targetPosition - spawnPosition;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            float distanceToPlayer = direction.magnitude;
            float range = Mathf.Max(0.1f, debufferBlueprint.attackRange);

            if (distanceToPlayer > range)
            {
                return;
            }

            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                spawnPosition,
                debufferBlueprint.atk,
                0f,
                debufferBlueprint.projectileSpeed,
                debufferBlueprint.targetLayer);

            if (projectile == null)
            {
                return;
            }

            projectile.Launch(direction.normalized);

            DebugDebuffer(
                $"디버프 탄환 발사 | Spawn={spawnPosition} | Target={targetPosition} | Speed={debufferBlueprint.projectileSpeed}");
        }

        private Vector2 GetPlayerAimPosition()
        {
            if (playerCharacter != null && playerCharacter.CenterTransform != null)
            {
                return playerCharacter.CenterTransform.position;
            }

            if (playerCharacter != null)
            {
                return playerCharacter.transform.position;
            }

            return transform.position;
        }

        private Vector2 GetProjectileSpawnWorldPosition()
        {
            if (projectileSpawnPosition != null)
            {
                return projectileSpawnPosition.position;
            }

            if (centerTransform != null)
            {
                return centerTransform.position;
            }

            return transform.position;
        }

        public override void Knockback(Vector2 knockback)
        {
            if (debufferBlueprint != null && debufferBlueprint.freezePositionAfterSpawn)
            {
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                }

                return;
            }

            base.Knockback(knockback);
        }

        public override void TakeDamage(
            float damage,
            Vector2 knockback = default(Vector2),
            bool isCritical = false)
        {
            Vector2 finalKnockback =
                debufferBlueprint != null && debufferBlueprint.freezePositionAfterSpawn
                    ? Vector2.zero
                    : knockback;

            base.TakeDamage(damage, finalKnockback, isCritical);

            if (debufferBlueprint != null && debufferBlueprint.freezePositionAfterSpawn && rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            StopAttackLoop();
            yield return base.Killed(killedByPlayer);
        }

        private void OnDisable()
        {
            StopAttackLoop();
        }

        private void StopAttackLoop()
        {
            if (attackCoroutine == null)
            {
                return;
            }

            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        private void DebugDebuffer(string message)
        {
            if (!attackSpeedDebufferDebugLog)
            {
                return;
            }

            Debug.Log($"[공속감소디버퍼] {message}", this);
        }
    }
}
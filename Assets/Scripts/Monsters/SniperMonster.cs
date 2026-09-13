using System.Collections;
using UnityEngine;

namespace Vampire
{
    // 원거리 저격수 몬스터
    // 움직이지 않고, 레이저로 플레이어를 조준한 뒤
    // 고정 위치로 빠른 탄환을 발사한다.
    public class SniperMonster : Monster
    {
        [Header("Sniper Monster")]
        [Tooltip("탄환이 발사될 위치입니다. 비워두면 몬스터 CenterTransform에서 발사합니다.")]
        [SerializeField] private Transform projectileSpawnPosition;

        [Header("Sniper Debug")]
        [Tooltip("체크하면 저격수 스폰, 조준, 발사 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugSniperMonster = true;

        private SniperMonsterBlueprint sniperBlueprint;

        private int projectileIndex = -1;
        private LineRenderer laserRenderer;
        private Coroutine attackCoroutine;

        protected override void Awake()
        {
            base.Awake();

            CreateLaserRenderer();

            DebugSniper("Awake 호출 - 저격수 프리팹 초기화");
        }

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint incomingBlueprint,
            float hpBuff = 0)
        {
            sniperBlueprint = incomingBlueprint as SniperMonsterBlueprint;

            if (sniperBlueprint == null)
            {
                Debug.LogError(
                    "[저격수] SniperMonsterBlueprint이 아닌 블루프린트가 들어왔습니다.",
                    this
                );

                return;
            }

            // Monster 부모 클래스에서 사용하는 기본 필드 세팅
            this.monsterIndex = monsterIndex;
            monsterBlueprint = sniperBlueprint;

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            Vector2 finalSpawnPosition =
                GetAdjustedSpawnPosition(position, sniperBlueprint);

            if (rb != null)
            {
                rb.position = finalSpawnPosition;
            }

            transform.position = finalSpawnPosition;

            currentHealth = sniperBlueprint.hp + hpBuff;
            alive = true;

            if (entityManager != null &&
                !entityManager.LivingMonsters.Contains(this))
            {
                entityManager.LivingMonsters.Add(this);
            }

            SetupVisualAndHitbox();

            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;

                // 저격수는 움직이지 않는 몬스터이므로
                // 위치와 회전을 고정한다.
                rb.constraints =
                    RigidbodyConstraints2D.FreezePositionX |
                    RigidbodyConstraints2D.FreezePositionY |
                    RigidbodyConstraints2D.FreezeRotation;
            }

            StopSniperLoop();
            HideLaser();

            projectileIndex = -1;

            if (sniperBlueprint.projectilePrefab != null)
            {
                if (entityManager != null)
                {
                    projectileIndex =
                        entityManager.AddPoolForProjectile(
                            sniperBlueprint.projectilePrefab
                        );

                    DebugSniper(
                        $"탄환 풀 등록 완료 | " +
                        $"Projectile Prefab: {sniperBlueprint.projectilePrefab.name} | " +
                        $"Pool Index: {projectileIndex}"
                    );
                }
                else
                {
                    Debug.LogWarning(
                        "[저격수] EntityManager가 없어 탄환 풀을 등록할 수 없습니다.",
                        this
                    );
                }
            }
            else
            {
                Debug.LogWarning(
                    "[저격수] projectilePrefab이 비어 있습니다. 탄환을 발사할 수 없습니다.",
                    this
                );
            }

            DebugSniper(
                $"스폰 완료 | Blueprint: {sniperBlueprint.name} | " +
                $"원래 위치: {position} | " +
                $"보정 위치: {finalSpawnPosition} | " +
                $"HP: {currentHealth} | " +
                $"ATK: {sniperBlueprint.atk}"
            );

            // 필드 몬스터가 MiniStage Suspend 상태에서
            // Pool에서 꺼내질 일은 EntityManager에서 차단되지만,
            // 안전장치로 Suspend 상태라면 공격을 시작하지 않는다.
            if (!IsFieldRuntimeSuspended)
            {
                attackCoroutine = StartCoroutine(SniperAttackLoop());
            }
        }

        private void SetupVisualAndHitbox()
        {
            // walkSpriteSequence가 비어 있거나
            // walkFrameTime이 0이면 SpriteAnimator에서 문제가 날 수 있으므로
            // 유효한 애니메이션이 있을 때만 초기화한다.
            bool hasValidAnimation =
                sniperBlueprint != null &&
                sniperBlueprint.walkSpriteSequence != null &&
                sniperBlueprint.walkSpriteSequence.Length > 0 &&
                sniperBlueprint.walkFrameTime > 0f;

            if (monsterSpriteAnimator != null && hasValidAnimation)
            {
                monsterSpriteAnimator.Init(
                    sniperBlueprint.walkSpriteSequence,
                    sniperBlueprint.walkFrameTime,
                    true
                );

                monsterSpriteAnimator.StartAnimating(true);
            }
            else
            {
                DebugSniper(
                    "걷기 애니메이션 없음 - SpriteAnimator 초기화를 건너뜁니다."
                );
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;

                if (monsterSpriteRenderer != null &&
                    monsterSpriteRenderer.sprite != null)
                {
                    monsterHitbox.size =
                        monsterSpriteRenderer.bounds.size;

                    monsterHitbox.offset =
                        Vector2.up * monsterHitbox.size.y / 2f;
                }
                else
                {
                    // 스프라이트가 비어 있어도 테스트 가능하도록
                    // 기본 충돌 크기를 사용한다.
                    monsterHitbox.size = Vector2.one;
                    monsterHitbox.offset = Vector2.up * 0.5f;

                    DebugSniper(
                        "SpriteRenderer 또는 Sprite가 비어 있어 " +
                        "기본 Hitbox 크기를 사용합니다."
                    );
                }
            }

            if (monsterLegsCollider != null &&
                monsterHitbox != null)
            {
                monsterLegsCollider.radius =
                    Mathf.Max(
                        0.1f,
                        monsterHitbox.size.x / 2.5f
                    );
            }

            if (centerTransform == null)
            {
                centerTransform =
                    new GameObject("Center Transform").transform;

                centerTransform.SetParent(transform);
            }

            Vector3 centerOffset =
                monsterHitbox != null
                    ? (Vector3)monsterHitbox.offset
                    : Vector3.zero;

            centerTransform.position =
                transform.position + centerOffset;
        }

        protected override void Update()
        {
            // 필드 저격수가 MiniStage 진입 때문에 정지된 상태라면
            // 플레이어 추적, 레이저 등의 처리를 하지 않는다.
            if (IsFieldRuntimeSuspended)
            {
                HideLaser();
                return;
            }

            base.Update();

            if (!alive)
            {
                HideLaser();
            }
        }

        protected override void FixedUpdate()
        {
            if (IsFieldRuntimeSuspended)
            {
                return;
            }

            // 저격수는 움직이지 않는 특수 몬스터.
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public override void Knockback(Vector2 knockback)
        {
            if (IsFieldRuntimeSuspended)
            {
                return;
            }

            // 저격수 몬스터는 위치 고정형이므로 넉백을 무시한다.
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public override void TakeDamage(
            float damage,
            Vector2 knockback = default(Vector2),
            bool isCritical = false)
        {
            if (IsFieldRuntimeSuspended)
            {
                return;
            }

            base.TakeDamage(
                damage,
                Vector2.zero,
                isCritical
            );

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public override IEnumerator Killed(
            bool killedByPlayer = true)
        {
            DebugSniper("사망 처리 시작");

            HideLaser();
            StopSniperLoop();

            yield return base.Killed(killedByPlayer);
        }

        /// <summary>
        /// 기존 필드 저격수가 MiniStage 진입 때문에
        /// Suspend될 때 호출된다.
        ///
        /// Rigidbody 정지만으로는 공격 Coroutine이 계속 돌기 때문에
        /// 반드시 레이저와 공격 루프를 같이 중지한다.
        /// </summary>
        protected override void OnFieldRuntimeSuspended()
        {
            HideLaser();
            StopSniperLoop();

            DebugSniper(
                "MiniStage 진입 - 필드 저격 행동 정지"
            );
        }

        /// <summary>
        /// MiniStage 종료 후 필드로 돌아왔을 때
        /// 공격 루프를 안전하게 새로 시작한다.
        ///
        /// 기존 조준 중간부터 이어가지 않고
        /// FirstAttackDelay부터 다시 시작한다.
        /// </summary>
        protected override void OnFieldRuntimeResumed()
        {
            if (!alive)
            {
                return;
            }

            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (sniperBlueprint == null)
            {
                return;
            }

            if (attackCoroutine != null)
            {
                return;
            }

            HideLaser();

            attackCoroutine =
                StartCoroutine(SniperAttackLoop());

            DebugSniper(
                "MiniStage 종료 - 필드 저격 행동 재개"
            );
        }

        private void OnDisable()
        {
            HideLaser();
            StopSniperLoop();
        }

        private void StopSniperLoop()
        {
            if (attackCoroutine == null)
            {
                return;
            }

            StopCoroutine(attackCoroutine);
            attackCoroutine = null;
        }

        private Vector2 GetAdjustedSpawnPosition(
            Vector2 originalPosition,
            SniperMonsterBlueprint blueprint)
        {
            if (blueprint == null)
            {
                return originalPosition;
            }

            if (!blueprint.enforceSpawnDistance ||
                playerCharacter == null)
            {
                return originalPosition;
            }

            Vector2 playerPosition =
                playerCharacter.transform.position;

            Vector2 directionFromPlayer =
                originalPosition - playerPosition;

            if (directionFromPlayer.sqrMagnitude <= 0.0001f)
            {
                directionFromPlayer =
                    Random.insideUnitCircle.normalized;
            }

            if (directionFromPlayer.sqrMagnitude <= 0.0001f)
            {
                directionFromPlayer = Vector2.right;
            }

            return playerPosition +
                   directionFromPlayer.normalized *
                   Mathf.Max(
                       0.1f,
                       blueprint.spawnDistanceFromPlayer
                   );
        }

        private IEnumerator SniperAttackLoop()
        {
            if (sniperBlueprint == null)
            {
                DebugSniper(
                    "공격 루프 시작 실패 - sniperBlueprint가 null"
                );

                attackCoroutine = null;
                yield break;
            }

            DebugSniper(
                $"공격 루프 시작 | " +
                $"First Delay: {sniperBlueprint.firstAttackDelay} | " +
                $"Aim: {sniperBlueprint.aimDuration} | " +
                $"Lock: {sniperBlueprint.lockDuration} | " +
                $"Cooldown: {sniperBlueprint.attackCooldown}"
            );

            // MiniStage Suspend가 걸리면 이 Coroutine 자체를
            // StopSniperLoop()에서 중단한다.
            yield return new WaitForSeconds(
                Mathf.Max(
                    0f,
                    sniperBlueprint.firstAttackDelay
                )
            );

            while (alive)
            {
                if (IsFieldRuntimeSuspended)
                {
                    HideLaser();
                    break;
                }

                yield return AimLockAndShoot();

                if (!alive ||
                    IsFieldRuntimeSuspended)
                {
                    break;
                }

                yield return new WaitForSeconds(
                    Mathf.Max(
                        0.05f,
                        sniperBlueprint.attackCooldown
                    )
                );
            }

            attackCoroutine = null;
        }

        private IEnumerator AimLockAndShoot()
        {
            if (playerCharacter == null ||
                sniperBlueprint == null)
            {
                DebugSniper(
                    "조준 실패 - playerCharacter 또는 sniperBlueprint가 null"
                );

                yield break;
            }

            if (IsFieldRuntimeSuspended)
            {
                HideLaser();
                yield break;
            }

            ShowLaser();

            float aimTimer = 0f;

            Vector2 lockedTargetPosition =
                GetPlayerAimPosition();

            DebugSniper(
                "조준 시작 - 레이저가 플레이어를 따라갑니다."
            );

            // ========================================================
            // 1단계
            // 레이저가 플레이어를 따라다니며 조준
            // ========================================================
            float aimDuration =
                Mathf.Max(
                    0.01f,
                    sniperBlueprint.aimDuration
                );

            while (aimTimer < aimDuration)
            {
                if (!alive ||
                    playerCharacter == null ||
                    IsFieldRuntimeSuspended)
                {
                    HideLaser();
                    yield break;
                }

                lockedTargetPosition =
                    GetPlayerAimPosition();

                UpdateLaser(
                    GetProjectileSpawnWorldPosition(),
                    lockedTargetPosition,
                    sniperBlueprint.aimingLaserColor
                );

                aimTimer += Time.deltaTime;

                yield return null;
            }

            if (IsFieldRuntimeSuspended)
            {
                HideLaser();
                yield break;
            }

            DebugSniper(
                $"조준 고정 | 고정 위치: {lockedTargetPosition}"
            );

            // ========================================================
            // 2단계
            // 조준 위치 고정
            // ========================================================
            UpdateLaser(
                GetProjectileSpawnWorldPosition(),
                lockedTargetPosition,
                sniperBlueprint.lockedLaserColor
            );

            float lockTimer = 0f;

            float lockDuration =
                Mathf.Max(
                    0f,
                    sniperBlueprint.lockDuration
                );

            while (lockTimer < lockDuration)
            {
                if (!alive ||
                    IsFieldRuntimeSuspended)
                {
                    HideLaser();
                    yield break;
                }

                UpdateLaser(
                    GetProjectileSpawnWorldPosition(),
                    lockedTargetPosition,
                    sniperBlueprint.lockedLaserColor
                );

                lockTimer += Time.deltaTime;

                yield return null;
            }

            if (!alive ||
                IsFieldRuntimeSuspended)
            {
                HideLaser();
                yield break;
            }

            // ========================================================
            // 3단계
            // 고정된 위치로 탄환 발사
            // ========================================================
            FireSniperProjectile(
                lockedTargetPosition
            );

            HideLaser();
        }

        private Vector2 GetPlayerAimPosition()
        {
            if (playerCharacter != null &&
                playerCharacter.CenterTransform != null)
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

        private void FireSniperProjectile(
            Vector2 lockedTargetPosition)
        {
            // MiniStage 진입 프레임과 발사 프레임이 겹쳐도
            // 마지막 안전장치로 탄환 생성을 막는다.
            if (IsFieldRuntimeSuspended)
            {
                HideLaser();
                return;
            }

            if (!alive)
            {
                HideLaser();
                return;
            }

            if (projectileIndex < 0 ||
                entityManager == null ||
                sniperBlueprint == null)
            {
                DebugSniper(
                    $"탄환 발사 실패 | " +
                    $"projectileIndex: {projectileIndex} | " +
                    $"entityManager null: {entityManager == null} | " +
                    $"sniperBlueprint null: {sniperBlueprint == null}"
                );

                return;
            }

            Vector2 spawnPosition =
                GetProjectileSpawnWorldPosition();

            Vector2 direction =
                lockedTargetPosition - spawnPosition;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                DebugSniper(
                    "탄환 발사 실패 - 방향 벡터가 너무 작음"
                );

                return;
            }

            Projectile projectile =
                entityManager.SpawnProjectile(
                    projectileIndex,
                    spawnPosition,
                    sniperBlueprint.atk,
                    0f,
                    sniperBlueprint.projectileSpeed,
                    sniperBlueprint.targetLayer
                );

            if (projectile == null)
            {
                DebugSniper(
                    "탄환 발사 실패 - SpawnProjectile 결과가 null"
                );

                return;
            }

            // 결과 화면용: 이 탄환을 발사한 몬스터 정보를 Projectile에 전달
            projectile.SetSourceMonster(sniperBlueprint);

            projectile.Launch(
                direction.normalized
            );

            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.SniperFire
            );

            DebugSniper(
                $"탄환 발사 | " +
                $"시작 위치: {spawnPosition} | " +
                $"목표 위치: {lockedTargetPosition} | " +
                $"방향: {direction.normalized} | " +
                $"속도: {sniperBlueprint.projectileSpeed} | " +
                $"데미지: {sniperBlueprint.atk}"
            );
        }

        private void CreateLaserRenderer()
        {
            // Pool 재사용이나 잘못된 중복 Awake 상황을 대비한 안전장치.
            if (laserRenderer != null)
            {
                return;
            }

            GameObject laserObject =
                new GameObject("Sniper Laser Pointer");

            laserObject.transform.SetParent(transform);
            laserObject.transform.localPosition = Vector3.zero;

            laserRenderer =
                laserObject.AddComponent<LineRenderer>();

            laserRenderer.positionCount = 2;
            laserRenderer.useWorldSpace = true;
            laserRenderer.enabled = false;

            Shader shader =
                Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find(
                    "Unlit/Color"
                );
            }

            if (shader != null)
            {
                laserRenderer.material =
                    new Material(shader);
            }

            laserRenderer.startWidth = 0.04f;
            laserRenderer.endWidth = 0.04f;
            laserRenderer.numCapVertices = 2;
            laserRenderer.sortingOrder = 50;
        }

        private void ShowLaser()
        {
            if (IsFieldRuntimeSuspended)
            {
                HideLaser();
                return;
            }

            if (laserRenderer == null)
            {
                return;
            }

            laserRenderer.enabled = true;

            if (sniperBlueprint != null)
            {
                laserRenderer.startWidth =
                    sniperBlueprint.laserWidth;

                laserRenderer.endWidth =
                    sniperBlueprint.laserWidth;

                laserRenderer.sortingOrder =
                    sniperBlueprint.laserSortingOrder;
            }
        }

        private void HideLaser()
        {
            if (laserRenderer == null)
            {
                return;
            }

            laserRenderer.enabled = false;
        }

        private void UpdateLaser(
            Vector2 startPosition,
            Vector2 endPosition,
            Color color)
        {
            if (IsFieldRuntimeSuspended)
            {
                HideLaser();
                return;
            }

            if (laserRenderer == null)
            {
                return;
            }

            laserRenderer.SetPosition(
                0,
                startPosition
            );

            laserRenderer.SetPosition(
                1,
                endPosition
            );

            laserRenderer.startColor = color;
            laserRenderer.endColor = color;
        }

        private void DebugSniper(string message)
        {
            if (!debugSniperMonster)
            {
                return;
            }

            Debug.Log(
                $"[저격수] {message}",
                this
            );
        }
    }
}
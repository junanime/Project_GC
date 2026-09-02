using System.Collections;
using UnityEngine;

namespace Vampire
{
    // 원거리 저격수 몬스터
    // 움직이지 않고, 레이저로 플레이어를 조준한 뒤 고정 위치로 빠른 탄환을 발사한다.
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

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint incomingBlueprint, float hpBuff = 0)
        {
            sniperBlueprint = incomingBlueprint as SniperMonsterBlueprint;

            if (sniperBlueprint == null)
            {
                Debug.LogError("[저격수] SniperMonsterBlueprint이 아닌 블루프린트가 들어왔습니다.");
                return;
            }

            // Monster 부모 클래스에서 사용하는 기본 필드 세팅
            this.monsterIndex = monsterIndex;
            monsterBlueprint = sniperBlueprint;

            Vector2 finalSpawnPosition = GetAdjustedSpawnPosition(position, sniperBlueprint);

            rb.position = finalSpawnPosition;
            transform.position = finalSpawnPosition;

            currentHealth = sniperBlueprint.hp + hpBuff;
            alive = true;

            if (!entityManager.LivingMonsters.Contains(this))
            {
                entityManager.LivingMonsters.Add(this);
            }

            SetupVisualAndHitbox();

            rb.velocity = Vector2.zero;
            rb.angularVelocity = 0f;

            // 저격수는 움직이지 않는 몬스터이므로 위치와 회전을 고정한다.
            rb.constraints = RigidbodyConstraints2D.FreezePositionX |
                             RigidbodyConstraints2D.FreezePositionY |
                             RigidbodyConstraints2D.FreezeRotation;

            StopSniperLoop();
            HideLaser();

            projectileIndex = -1;

            if (sniperBlueprint.projectilePrefab != null)
            {
                projectileIndex = entityManager.AddPoolForProjectile(sniperBlueprint.projectilePrefab);

                DebugSniper(
                    $"탄환 풀 등록 완료 | Projectile Prefab: {sniperBlueprint.projectilePrefab.name} | Pool Index: {projectileIndex}"
                );
            }
            else
            {
                Debug.LogWarning("[저격수] projectilePrefab이 비어 있습니다. 탄환을 발사할 수 없습니다.");
            }

            DebugSniper(
                $"스폰 완료 | Blueprint: {sniperBlueprint.name} | 원래 위치: {position} | 보정 위치: {finalSpawnPosition} | HP: {currentHealth} | ATK: {sniperBlueprint.atk}"
            );

            attackCoroutine = StartCoroutine(SniperAttackLoop());
        }

        private void SetupVisualAndHitbox()
        {
            // 기존 오류 원인:
            // walkSpriteSequence가 비어 있거나 walkFrameTime이 0이면 SpriteAnimator.Setup()에서 DivideByZeroException 발생.
            // 저격수는 정지형 몬스터라 애니메이션이 없어도 되므로 안전 검사 후에만 Init한다.
            bool hasValidAnimation =
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
                DebugSniper("걷기 애니메이션 없음 - SpriteAnimator 초기화를 건너뜁니다.");
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
                    // 스프라이트가 비어 있어도 테스트 가능하도록 기본 충돌 크기 사용
                    monsterHitbox.size = Vector2.one;
                    monsterHitbox.offset = Vector2.up * 0.5f;

                    DebugSniper("SpriteRenderer 또는 Sprite가 비어 있어 기본 Hitbox 크기를 사용합니다.");
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
                HideLaser();
            }
        }

        // 부모 Monster의 FixedUpdate가 virtual이 아닐 가능성도 있으므로 new로 안전하게 처리.
        protected new void FixedUpdate()
        {
            // 움직이지 않는 특수 몬스터.
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public override void Knockback(Vector2 knockback)
        {
            // 저격수 몬스터는 위치 고정형이므로 넉백을 무시한다.
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public override void TakeDamage(
    float damage,
    Vector2 knockback = default(Vector2),
    bool isCritical = false)
        {
            base.TakeDamage(damage, Vector2.zero, isCritical);

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            DebugSniper("사망 처리 시작");

            HideLaser();
            StopSniperLoop();

            yield return base.Killed(killedByPlayer);
        }

        private void OnDisable()
        {
            HideLaser();
            StopSniperLoop();
        }

        private void StopSniperLoop()
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
        }

        private Vector2 GetAdjustedSpawnPosition(Vector2 originalPosition, SniperMonsterBlueprint sniperBlueprint)
        {
            if (!sniperBlueprint.enforceSpawnDistance || playerCharacter == null)
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

            return playerPosition + directionFromPlayer.normalized * Mathf.Max(0.1f, sniperBlueprint.spawnDistanceFromPlayer);
        }

        private IEnumerator SniperAttackLoop()
        {
            if (sniperBlueprint == null)
            {
                DebugSniper("공격 루프 시작 실패 - sniperBlueprint가 null");
                yield break;
            }

            DebugSniper(
                $"공격 루프 시작 | First Delay: {sniperBlueprint.firstAttackDelay} | Aim: {sniperBlueprint.aimDuration} | Lock: {sniperBlueprint.lockDuration} | Cooldown: {sniperBlueprint.attackCooldown}"
            );

            yield return new WaitForSeconds(Mathf.Max(0f, sniperBlueprint.firstAttackDelay));

            while (alive)
            {
                yield return AimLockAndShoot();

                yield return new WaitForSeconds(Mathf.Max(0.05f, sniperBlueprint.attackCooldown));
            }
        }

        private IEnumerator AimLockAndShoot()
        {
            if (playerCharacter == null || sniperBlueprint == null)
            {
                DebugSniper("조준 실패 - playerCharacter 또는 sniperBlueprint가 null");
                yield break;
            }

            ShowLaser();

            float aimTimer = 0f;
            Vector2 lockedTargetPosition = GetPlayerAimPosition();

            DebugSniper("조준 시작 - 레이저가 플레이어를 따라갑니다.");

            // 1단계: 레이저가 플레이어를 따라다니며 조준
            while (aimTimer < Mathf.Max(0.01f, sniperBlueprint.aimDuration))
            {
                if (!alive || playerCharacter == null)
                {
                    HideLaser();
                    yield break;
                }

                lockedTargetPosition = GetPlayerAimPosition();

                UpdateLaser(
                    GetProjectileSpawnWorldPosition(),
                    lockedTargetPosition,
                    sniperBlueprint.aimingLaserColor
                );

                aimTimer += Time.deltaTime;
                yield return null;
            }

            DebugSniper($"조준 고정 | 고정 위치: {lockedTargetPosition}");

            // 2단계: 조준 위치 고정
            UpdateLaser(
                GetProjectileSpawnWorldPosition(),
                lockedTargetPosition,
                sniperBlueprint.lockedLaserColor
            );

            float lockTimer = 0f;

            while (lockTimer < Mathf.Max(0f, sniperBlueprint.lockDuration))
            {
                if (!alive)
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

            // 3단계: 고정된 위치로 탄환 발사
            FireSniperProjectile(lockedTargetPosition);

            HideLaser();
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

        private void FireSniperProjectile(Vector2 lockedTargetPosition)
        {
            if (projectileIndex < 0 || entityManager == null || sniperBlueprint == null)
            {
                DebugSniper(
                    $"탄환 발사 실패 | projectileIndex: {projectileIndex} | entityManager null: {entityManager == null} | sniperBlueprint null: {sniperBlueprint == null}"
                );
                return;
            }

            Vector2 spawnPosition = GetProjectileSpawnWorldPosition();
            Vector2 direction = lockedTargetPosition - spawnPosition;

            if (direction.sqrMagnitude <= 0.0001f)
            {
                DebugSniper("탄환 발사 실패 - 방향 벡터가 너무 작음");
                return;
            }

            Projectile projectile = entityManager.SpawnProjectile(
                projectileIndex,
                spawnPosition,
                sniperBlueprint.atk,
                0f,
                sniperBlueprint.projectileSpeed,
                sniperBlueprint.targetLayer
            );

            if (projectile == null)
            {
                DebugSniper("탄환 발사 실패 - SpawnProjectile 결과가 null");
                return;
            }

            projectile.Launch(direction.normalized);

            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.SniperFire
);

            DebugSniper(
                $"탄환 발사 | 시작 위치: {spawnPosition} | 목표 위치: {lockedTargetPosition} | 방향: {direction.normalized} | 속도: {sniperBlueprint.projectileSpeed} | 데미지: {sniperBlueprint.atk}"
            );
        }

        private void CreateLaserRenderer()
        {
            GameObject laserObject = new GameObject("Sniper Laser Pointer");
            laserObject.transform.SetParent(transform);
            laserObject.transform.localPosition = Vector3.zero;

            laserRenderer = laserObject.AddComponent<LineRenderer>();
            laserRenderer.positionCount = 2;
            laserRenderer.useWorldSpace = true;
            laserRenderer.enabled = false;

            Shader shader = Shader.Find("Sprites/Default");

            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader != null)
            {
                laserRenderer.material = new Material(shader);
            }

            laserRenderer.startWidth = 0.04f;
            laserRenderer.endWidth = 0.04f;
            laserRenderer.numCapVertices = 2;
            laserRenderer.sortingOrder = 50;
        }

        private void ShowLaser()
        {
            if (laserRenderer == null)
            {
                return;
            }

            laserRenderer.enabled = true;

            if (sniperBlueprint != null)
            {
                laserRenderer.startWidth = sniperBlueprint.laserWidth;
                laserRenderer.endWidth = sniperBlueprint.laserWidth;
                laserRenderer.sortingOrder = sniperBlueprint.laserSortingOrder;
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

        private void UpdateLaser(Vector2 startPosition, Vector2 endPosition, Color color)
        {
            if (laserRenderer == null)
            {
                return;
            }

            laserRenderer.SetPosition(0, startPosition);
            laserRenderer.SetPosition(1, endPosition);
            laserRenderer.startColor = color;
            laserRenderer.endColor = color;
        }

        private void DebugSniper(string message)
        {
            if (!debugSniperMonster)
            {
                return;
            }

            Debug.Log($"[저격수] {message}", this);
        }
    }
}
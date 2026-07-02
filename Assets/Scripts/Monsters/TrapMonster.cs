using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class TrapMonster : Monster
    {
        private enum TrapState
        {
            Dormant,
            Active,
            Dying,
            Dead
        }

        [Header("Trap Components")]
        [Tooltip("함정 몬스터의 외형을 보여줄 SpriteRenderer입니다.")]
        [SerializeField] private SpriteRenderer trapSpriteRenderer;

        [Tooltip("플레이어가 밟았는지 확인하고, 활성화 후 공격받는 판정으로 사용할 Collider2D입니다.")]
        [SerializeField] private Collider2D triggerCollider;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        private TrapMonsterBlueprint trapBlueprint;
        private TrapState currentState = TrapState.Dormant;

        private Character trappedCharacter;
        private IDamageable trappedDamageable;
        private PlayerTrapBindRuntime trappedBindRuntime;

        private Coroutine stateAnimationCoroutine;
        private Coroutine tickDamageCoroutine;
        private Coroutine bindDurationCoroutine;
        private Coroutine deathRoutineCoroutine;

        private float trapHpBuff = 0f;
        private bool setupCompleted = false;
        private bool killStarted = false;

        public bool IsActive => currentState == TrapState.Active;
        public bool IsDormant => currentState == TrapState.Dormant;

        protected override void Awake()
        {
            base.Awake();

            if (trapSpriteRenderer == null)
            {
                trapSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }
        }

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint incomingBlueprint, float hpBuff = 0)
        {
            trapBlueprint = incomingBlueprint as TrapMonsterBlueprint;

            if (trapBlueprint == null)
            {
                Debug.LogError("[TrapMonster] TrapMonsterBlueprint가 연결되지 않았습니다.", this);
                return;
            }

            this.monsterIndex = monsterIndex;
            monsterBlueprint = trapBlueprint;

            trapHpBuff = hpBuff;
            currentHealth = trapBlueprint.activeHealth + trapHpBuff;
            alive = true;
            killStarted = false;
            setupCompleted = true;

            if (!entityManager.LivingMonsters.Contains(this))
            {
                entityManager.LivingMonsters.Add(this);
            }

            if (trapSpriteRenderer == null)
            {
                trapSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (triggerCollider == null)
            {
                triggerCollider = GetComponent<Collider2D>();
            }

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            Vector2 spawnPosition = position;

            if (trapBlueprint.spawnNearPlayerForTest)
            {
                Character player = FindPlayerCharacter();

                if (player != null)
                {
                    spawnPosition = GetSpawnPositionNearPlayer(player.transform.position);
                }
            }

            transform.position = spawnPosition;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            if (triggerCollider != null)
            {
                triggerCollider.enabled = true;
                triggerCollider.isTrigger = true;
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;

                if (trapSpriteRenderer != null && trapSpriteRenderer.sprite != null)
                {
                    monsterHitbox.size = trapSpriteRenderer.bounds.size;
                    monsterHitbox.offset = Vector2.zero;
                }
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = false;
            }

            if (centerTransform == null)
            {
                centerTransform = new GameObject("Center Transform").transform;
                centerTransform.SetParent(transform);
            }

            centerTransform.position = transform.position;

            StopTrapCoroutines();
            ReleaseTrappedPlayer();

            ChangeState(TrapState.Dormant);

            if (debugLog)
            {
                Debug.Log($"[TrapMonster] 스폰 완료 | 위치 {transform.position} | 휴면 상태", this);
            }
        }

        protected override void Update()
        {
            // 부모 Monster.Update()가 이동/애니메이션 처리를 할 수 있으므로 호출하지 않는다.
            // 함정 몬스터는 자체 상태머신으로만 동작한다.
        }

        protected new void FixedUpdate()
        {
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        public override void Knockback(Vector2 knockback)
        {
            // 함정 몬스터는 바닥 고정형이라 넉백을 받지 않는다.
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }
        }

        public override void TakeDamage(
    float damage,
    Vector2 direction = default(Vector2),
    bool isCritical = false)
        {
            if (!setupCompleted || trapBlueprint == null)
            {
                return;
            }

            if (currentState != TrapState.Active)
            {
                if (debugLog)
                {
                    Debug.Log("[TrapMonster] 휴면 상태라서 데미지를 받지 않음", this);
                }

                return;
            }

            currentHealth -= damage;

            if (debugLog)
            {
                Debug.Log($"[TrapMonster] 피격 | 받은 피해 {damage:0.##} | 남은 체력 {currentHealth:0.##}", this);
            }

            if (currentHealth <= 0f && !killStarted)
            {
                StartCoroutine(Killed(true));
            }
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            if (killStarted)
            {
                yield break;
            }

            killStarted = true;
            alive = false;

            ReleaseTrappedPlayer();

            ChangeState(TrapState.Dying);

            if (debugLog)
            {
                Debug.Log("[TrapMonster] 사망 시작 - 플레이어 구속 해제", this);
            }

            float deathPlayTime = GetAnimationTotalTime(
                trapBlueprint != null ? trapBlueprint.deathSprites : null,
                trapBlueprint != null ? trapBlueprint.animationFrameTime : 0.15f
            );

            if (deathPlayTime > 0f)
            {
                yield return new WaitForSeconds(deathPlayTime);
            }

            if (trapBlueprint != null && trapBlueprint.deathDespawnDelay > 0f)
            {
                yield return new WaitForSeconds(trapBlueprint.deathDespawnDelay);
            }

            currentState = TrapState.Dead;

            if (debugLog)
            {
                Debug.Log("[TrapMonster] 사망 완료 - 오브젝트 비활성화", this);
            }

            yield return base.Killed(killedByPlayer);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (!setupCompleted || currentState != TrapState.Dormant)
            {
                return;
            }

            Character playerCharacter = other.GetComponentInParent<Character>();

            if (playerCharacter == null)
            {
                return;
            }

            ActivateTrap(playerCharacter);
        }

        private void ActivateTrap(Character playerCharacter)
        {
            if (playerCharacter == null || currentState != TrapState.Dormant)
            {
                return;
            }

            trappedCharacter = playerCharacter;
            trappedDamageable = playerCharacter as IDamageable;

            if (trappedDamageable == null)
            {
                trappedDamageable = playerCharacter.GetComponent<IDamageable>();
            }

            trappedBindRuntime = PlayerTrapBindRuntime.GetOrCreate(playerCharacter);

            bool bindSucceeded = true;

            if (trapBlueprint.bindPlayerOnActivate && trappedBindRuntime != null)
            {
                bindSucceeded = trappedBindRuntime.TryBind(this, playerCharacter.transform.position);
            }

            if (!bindSucceeded)
            {
                if (debugLog)
                {
                    Debug.Log("[TrapMonster] 플레이어 바인드 실패", this);
                }

                return;
            }

            currentHealth = trapBlueprint.activeHealth + trapHpBuff;

            ChangeState(TrapState.Active);

            if (tickDamageCoroutine != null)
            {
                StopCoroutine(tickDamageCoroutine);
            }

            tickDamageCoroutine = StartCoroutine(TickDamageRoutine());

            if (bindDurationCoroutine != null)
            {
                StopCoroutine(bindDurationCoroutine);
            }

            if (trapBlueprint.bindDuration > 0f)
            {
                bindDurationCoroutine = StartCoroutine(BindDurationRoutine());
            }

            if (debugLog)
            {
                Debug.Log($"[TrapMonster] 활성화됨 | HP {currentHealth:0.##} | 플레이어 구속", this);
            }
        }

        private IEnumerator TickDamageRoutine()
        {
            float interval = Mathf.Max(0.05f, trapBlueprint.tickInterval);
            WaitForSeconds wait = new WaitForSeconds(interval);

            while (currentState == TrapState.Active)
            {
                if (trappedCharacter == null || trappedDamageable == null)
                {
                    yield break;
                }

                trappedDamageable.TakeDamage(trapBlueprint.tickDamage, Vector2.zero, false);

                if (debugLog)
                {
                    Debug.Log($"[TrapMonster] 플레이어에게 틱 데미지 {trapBlueprint.tickDamage:0.##}", this);
                }

                yield return wait;
            }
        }

        private IEnumerator BindDurationRoutine()
        {
            yield return new WaitForSeconds(trapBlueprint.bindDuration);

            if (currentState != TrapState.Active)
            {
                yield break;
            }

            if (debugLog)
            {
                Debug.Log("[TrapMonster] 바인드 시간이 종료되어 플레이어를 해제", this);
            }

            ReleaseTrappedPlayer();

            // 제한 시간이 끝났는데 함정이 죽지 않았다면 다시 휴면 상태로 돌아간다.
            // 나중에 원하면 여기서 바로 사망하거나, 일정 쿨타임 후 재활성 가능하도록 바꿀 수 있다.
            currentHealth = trapBlueprint.activeHealth + trapHpBuff;
            ChangeState(TrapState.Dormant);
        }

        private void ReleaseTrappedPlayer()
        {
            if (trappedBindRuntime != null)
            {
                trappedBindRuntime.Release(this);
            }

            trappedCharacter = null;
            trappedDamageable = null;
            trappedBindRuntime = null;

            if (tickDamageCoroutine != null)
            {
                StopCoroutine(tickDamageCoroutine);
                tickDamageCoroutine = null;
            }

            if (bindDurationCoroutine != null)
            {
                StopCoroutine(bindDurationCoroutine);
                bindDurationCoroutine = null;
            }
        }

        private void StopTrapCoroutines()
        {
            if (stateAnimationCoroutine != null)
            {
                StopCoroutine(stateAnimationCoroutine);
                stateAnimationCoroutine = null;
            }

            if (tickDamageCoroutine != null)
            {
                StopCoroutine(tickDamageCoroutine);
                tickDamageCoroutine = null;
            }

            if (bindDurationCoroutine != null)
            {
                StopCoroutine(bindDurationCoroutine);
                bindDurationCoroutine = null;
            }

            if (deathRoutineCoroutine != null)
            {
                StopCoroutine(deathRoutineCoroutine);
                deathRoutineCoroutine = null;
            }
        }

        private void ChangeState(TrapState nextState)
        {
            currentState = nextState;

            if (stateAnimationCoroutine != null)
            {
                StopCoroutine(stateAnimationCoroutine);
                stateAnimationCoroutine = null;
            }

            if (trapBlueprint == null)
            {
                return;
            }

            switch (currentState)
            {
                case TrapState.Dormant:
                    stateAnimationCoroutine = StartCoroutine(PlayLoopAnimation(trapBlueprint.dormantSprites));
                    break;

                case TrapState.Active:
                    stateAnimationCoroutine = StartCoroutine(PlayLoopAnimation(trapBlueprint.activeSprites));
                    break;

                case TrapState.Dying:
                    stateAnimationCoroutine = StartCoroutine(PlayOneShotAnimation(trapBlueprint.deathSprites));
                    break;
            }
        }

        private IEnumerator PlayLoopAnimation(Sprite[] frames)
        {
            if (trapSpriteRenderer == null)
            {
                yield break;
            }

            if (frames == null || frames.Length == 0)
            {
                yield break;
            }

            if (frames.Length == 1)
            {
                trapSpriteRenderer.sprite = frames[0];

                while (true)
                {
                    yield return null;
                }
            }

            int index = 0;
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, trapBlueprint.animationFrameTime));

            while (true)
            {
                trapSpriteRenderer.sprite = frames[index];
                index = (index + 1) % frames.Length;
                yield return wait;
            }
        }

        private IEnumerator PlayOneShotAnimation(Sprite[] frames)
        {
            if (trapSpriteRenderer == null)
            {
                yield break;
            }

            if (frames == null || frames.Length == 0)
            {
                yield break;
            }

            if (frames.Length == 1)
            {
                trapSpriteRenderer.sprite = frames[0];
                yield break;
            }

            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, trapBlueprint.animationFrameTime));

            for (int i = 0; i < frames.Length; i++)
            {
                trapSpriteRenderer.sprite = frames[i];
                yield return wait;
            }
        }

        private float GetAnimationTotalTime(Sprite[] frames, float frameTime)
        {
            if (frames == null || frames.Length == 0)
            {
                return 0f;
            }

            return Mathf.Max(0.01f, frameTime) * frames.Length;
        }

        private Character FindPlayerCharacter()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                Character playerByTag = playerObject.GetComponent<Character>();

                if (playerByTag != null)
                {
                    return playerByTag;
                }

                return playerObject.GetComponentInParent<Character>();
            }

            return FindObjectOfType<Character>();
        }

        private Vector2 GetSpawnPositionNearPlayer(Vector2 playerPosition)
        {
            float minDistance = Mathf.Max(0.5f, trapBlueprint.spawnMinDistanceFromPlayer);
            float maxDistance = Mathf.Max(minDistance, trapBlueprint.spawnMaxDistanceFromPlayer);

            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            if (randomDirection == Vector2.zero)
            {
                randomDirection = Vector2.right;
            }

            float distance = Random.Range(minDistance, maxDistance);
            return playerPosition + randomDirection * distance;
        }

        private void OnDisable()
        {
            ReleaseTrappedPlayer();
            StopTrapCoroutines();
        }
    }
}
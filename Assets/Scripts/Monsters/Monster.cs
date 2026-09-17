using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Vampire
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Monster : IDamageable, ISpatialHashGridClient
    {
        [SerializeField] protected Material defaultMaterial, whiteMaterial, dissolveMaterial;
        [SerializeField] protected ParticleSystem deathParticles;
        [SerializeField] protected GameObject shadow;

        protected BoxCollider2D monsterHitbox;
        protected CircleCollider2D monsterLegsCollider;

        protected int monsterIndex;
        protected MonsterBlueprint monsterBlueprint;
        protected SpriteAnimator monsterSpriteAnimator;
        protected SpriteRenderer monsterSpriteRenderer;
        protected ZPositioner zPositioner;

        protected float currentHealth;
        protected EntityManager entityManager;
        protected Character playerCharacter;
        protected Rigidbody2D rb;

        protected int currWalkSequenceFrame = 0;
        protected bool knockedBack = false;
        protected Coroutine hitAnimationCoroutine = null;
        protected bool alive = true;
        protected Transform centerTransform;
        //상현추가
        protected float currentAcceleration;
        protected float runtimeMoveSpeed;
        // ============================================================
        // Mini Stage Runtime Ownership / Suspend
        // ============================================================

        // true면 미니 스테이지 방에서 생성된 몬스터.
        // false면 기존 메인 필드 몬스터.
        private bool miniStageOwned = false;

        // 현재 메인 필드 런타임 정지 상태인지 여부.
        private bool fieldRuntimeSuspended = false;

        // 미니 스테이지 진입 전에 Rigidbody2D.simulated가 어떤 값이었는지 보존한다.
        // 특수 몬스터가 원래 simulated=false 상태였을 가능성도 고려한다.
        private bool cachedRigidbodySimulated = true;
        private bool hasCachedRigidbodySimulated = false;


        private Vector3 originalLocalScale = Vector3.one;

        public bool IsMiniStageOwned => miniStageOwned;
        public bool IsFieldRuntimeSuspended => fieldRuntimeSuspended;
        public Transform CenterTransform { get => centerTransform; }

        // 다른 코드에서 OnKilled.AddListener(OnEliteKilled(Monster)) 식으로 쓰고 있으므로 Monster 인자를 넘긴다.
        public UnityEvent<Monster> OnKilled { get; } = new UnityEvent<Monster>();

        public float HP => currentHealth;

        // 결과 화면 및 공격 출처 추적용
        public MonsterBlueprint Blueprint => monsterBlueprint;

        public Vector2 Position => transform.position;
        public Vector2 Size => monsterLegsCollider != null ? monsterLegsCollider.bounds.size : Vector2.one;

        public Dictionary<int, int> ListIndexByCellIndex { get; set; }
        public int QueryID { get; set; } = -1;
        //상현추가
        public float moveSpeed
        {
            get => runtimeMoveSpeed;
            set
            {
                // 속도가 0 이하로 내려가서 무한대 드래그가 걸리는 것을 방지
                runtimeMoveSpeed = Mathf.Max(0.05f, value);
                if (rb != null)
                {
                    //  속도가 변하면 가속도와 비례하여 물리 마찰력(drag)을 실시간으로 재계산합니다!
                    rb.drag = currentAcceleration / (runtimeMoveSpeed * runtimeMoveSpeed);
                }
            }
        }

        protected virtual void Awake()
        {
            originalLocalScale = transform.localScale;

            rb = GetComponent<Rigidbody2D>();
            monsterLegsCollider = GetComponent<CircleCollider2D>();
            monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>(true);
            monsterSpriteRenderer = FindMainSpriteRenderer();

            zPositioner = GetComponent<ZPositioner>();

            if (zPositioner == null)
            {
                zPositioner = gameObject.AddComponent<ZPositioner>();
            }

            SetupHitboxReference();
        }

        private SpriteRenderer FindMainSpriteRenderer()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

            if (renderers == null || renderers.Length == 0)
            {
                return null;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];

                if (renderer == null)
                {
                    continue;
                }

                string objectName = renderer.gameObject.name.ToLower();

                // 그림자용 SpriteRenderer를 메인 몬스터 이미지로 잡지 않게 방지
                if (objectName.Contains("shadow"))
                {
                    continue;
                }

                return renderer;
            }

            return renderers[0];
        }

        private void SetupHitboxReference()
        {
            if (monsterSpriteRenderer == null)
            {
                monsterHitbox = null;
                return;
            }

            monsterHitbox = monsterSpriteRenderer.gameObject.GetComponent<BoxCollider2D>();

            if (monsterHitbox == null)
            {
                monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>();
            }

            monsterHitbox.isTrigger = true;
        }

        public virtual void Init(
            EntityManager entityManager,
            Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            if (zPositioner != null && playerCharacter != null)
            {
                zPositioner.Init(playerCharacter.transform);
            }
        }
        /// <summary>
        /// EntityManager가 Pool에서 몬스터를 꺼낸 직후 Setup()보다 먼저 호출합니다.
        ///
        /// allowDuringMiniStage=true로 생성된 몬스터는
        /// 미니 스테이지 전용 몬스터로 취급합니다.
        ///
        /// Pool에서 이전 사용 상태가 남아 있더라도
        /// Rigidbody / Suspend 상태를 안전하게 초기화합니다.
        /// </summary>
        public void PrepareForSpawnRuntime(bool isMiniStageOwned)
        {
            miniStageOwned = isMiniStageOwned;

            fieldRuntimeSuspended = false;
            hasCachedRigidbodySimulated = false;

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (rb != null)
            {
                rb.simulated = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        /// <summary>
        /// 메인 필드 몬스터만 일시 정지/재개합니다.
        ///
        /// 미니 스테이지 소유 몬스터는 이 호출을 무시하므로
        /// MiniStageSniper / ExplodingRush 같은 방 몬스터는 정상 동작합니다.
        /// </summary>
        public void SetFieldRuntimeSuspended(bool suspended)
        {
            // 미니 스테이지에서 생성한 몬스터는
            // 필드 정지 대상이 아니다.
            if (miniStageOwned)
            {
                return;
            }

            if (fieldRuntimeSuspended == suspended)
            {
                return;
            }

            fieldRuntimeSuspended = suspended;

            if (suspended)
            {
                if (rb != null)
                {
                    cachedRigidbodySimulated = rb.simulated;
                    hasCachedRigidbodySimulated = true;

                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;

                    // Physics2D Solver 자체에서 제외한다.
                    // 따라서 다른 몬스터에게 밀리거나 이동하는 것도 막힌다.
                    rb.simulated = false;
                }

                OnFieldRuntimeSuspended();
                return;
            }

            // Resume
            if (rb != null)
            {
                if (hasCachedRigidbodySimulated)
                {
                    rb.simulated = cachedRigidbodySimulated;
                }

                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            hasCachedRigidbodySimulated = false;

            OnFieldRuntimeResumed();
        }

        /// <summary>
        /// 특수 몬스터가 MiniStage 진입 시
        /// 자체 Coroutine / Warning / UI 등을 정리하고 싶을 때 Override.
        /// </summary>
        protected virtual void OnFieldRuntimeSuspended()
        {
        }

        /// <summary>
        /// 특수 몬스터가 MiniStage 종료 후
        /// 자체 행동을 다시 시작하고 싶을 때 Override.
        /// </summary>
        protected virtual void OnFieldRuntimeResumed()
        {
        }
        public virtual void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            this.monsterIndex = monsterIndex;
            this.monsterBlueprint = monsterBlueprint;

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (monsterLegsCollider == null)
            {
                monsterLegsCollider = GetComponent<CircleCollider2D>();
            }

            if (monsterSpriteAnimator == null)
            {
                monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>(true);
            }

            if (monsterSpriteRenderer == null)
            {
                monsterSpriteRenderer = FindMainSpriteRenderer();
            }

            if (monsterHitbox == null)
            {
                SetupHitboxReference();
            }

            EliteMonsterBlueprint eliteBlueprint = monsterBlueprint as EliteMonsterBlueprint;

            float scaleMultiplier = 1f;

            if (eliteBlueprint != null)
            {
                scaleMultiplier = Mathf.Max(0.1f, eliteBlueprint.scaleMultiplier);
            }

            transform.localScale = originalLocalScale * scaleMultiplier;

            if (rb != null)
            {
                rb.position = position;
                rb.velocity = Vector2.zero;
            }

            transform.position = position;

            float baseHp = monsterBlueprint != null ? monsterBlueprint.hp : 1f;

            if (eliteBlueprint != null)
            {
                baseHp = eliteBlueprint.GetEffectiveBaseHP();
            }

            float finalHp = baseHp + hpBuff;

            if (eliteBlueprint != null)
            {
                finalHp *= Mathf.Max(0.01f, eliteBlueprint.hpMultiplier);
            }

            currentHealth = finalHp;
            alive = true;

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Add(this);
            }

            Sprite[] walkSpriteSequence = monsterBlueprint != null ? monsterBlueprint.walkSpriteSequence : null;
            float walkFrameTime = monsterBlueprint != null ? monsterBlueprint.walkFrameTime : 0.1f;

            if (eliteBlueprint != null)
            {
                walkSpriteSequence = eliteBlueprint.GetEffectiveWalkSpriteSequence();
                walkFrameTime = eliteBlueprint.GetEffectiveWalkFrameTime();
            }

            if (monsterSpriteAnimator != null &&
                walkSpriteSequence != null &&
                walkSpriteSequence.Length > 0)
            {
                monsterSpriteAnimator.Init(
                    walkSpriteSequence,
                    walkFrameTime,
                    true
                );

                monsterSpriteAnimator.StartAnimating(true);
            }
            else if (monsterSpriteRenderer != null &&
                     walkSpriteSequence != null &&
                     walkSpriteSequence.Length > 0)
            {
                monsterSpriteRenderer.sprite = walkSpriteSequence[0];
            }

            if (monsterHitbox != null && monsterSpriteRenderer != null)
            {
                monsterHitbox.enabled = true;
                monsterHitbox.size = monsterSpriteRenderer.bounds.size;
                monsterHitbox.offset = Vector2.up * monsterHitbox.size.y / 2f;
            }

            if (monsterLegsCollider != null && monsterHitbox != null)
            {
                monsterLegsCollider.radius = Mathf.Max(0.05f, monsterHitbox.size.x / 2.5f);
            }

            if (centerTransform == null)
            {
                centerTransform = new GameObject("Center Transform").transform;
                centerTransform.SetParent(transform);
            }

            if (monsterHitbox != null)
            {
                centerTransform.position =
                    transform.position + (Vector3)monsterHitbox.offset;
            }
            else
            {
                centerTransform.position = transform.position;
            }

            // 상현수정
            float baseMoveSpeed = monsterBlueprint != null ? monsterBlueprint.movespeed : 1f;
            currentAcceleration = monsterBlueprint != null ? monsterBlueprint.acceleration : 1f;

            if (eliteBlueprint != null)
            {
                baseMoveSpeed = eliteBlueprint.GetEffectiveMoveSpeed();
                currentAcceleration = eliteBlueprint.GetEffectiveAcceleration();
            }

            float spd = Random.Range(
                baseMoveSpeed - 0.1f,
                baseMoveSpeed + 0.1f
            );

            // 중요: 새로 만든 프로퍼티에 대입하여 기본 속도를 세팅합니다.
            // 프로퍼티 내부의 set 구문이 작동하면서 rb.drag(마찰력)도 자동으로 계산되어 들어갑니다!
            this.moveSpeed = spd;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            StopAllCoroutines();
            if (eliteBlueprint != null)
            {
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.EliteSpawn
                );
            }

            if (eliteBlueprint != null && eliteBlueprint.debugLog)
            {
                Debug.Log(
                    $"[EliteMonster] Spawned | name={monsterBlueprint.name} | " +
                    $"scale={scaleMultiplier} | hp={currentHealth:0.##}"
                );
            }
        }

        protected virtual void Update()
        {
            if (fieldRuntimeSuspended)
            {
                return;
            }

            if (playerCharacter == null || monsterSpriteRenderer == null || rb == null)
            {
                return;
            }

            monsterSpriteRenderer.flipX =
                ((playerCharacter.transform.position.x - rb.position.x) < 0);
        }

        protected virtual void FixedUpdate()
        {
        }

        public override void Knockback(Vector2 knockback)
        {
            if (fieldRuntimeSuspended || rb == null)
            {
                return;
            }

            rb.velocity += knockback * Mathf.Sqrt(Mathf.Max(0.01f, rb.drag));
        }

        public override void TakeDamage(
    float damage,
    Vector2 knockback = default(Vector2),
    bool isCritical = false)
        {
            if (!alive || fieldRuntimeSuspended)

            {
                return;
            }
            MonsterCombatBuffRuntime combatBuffRuntime = GetComponent<MonsterCombatBuffRuntime>();

            if (combatBuffRuntime != null)
            {
                damage = combatBuffRuntime.ModifyIncomingDamage(damage);
            }
            if (entityManager != null && monsterHitbox != null)
            {
                entityManager.SpawnDamageText(monsterHitbox.transform.position, damage, isCritical);
            }

            currentHealth -= damage;

            if (damage > 0f)
            {
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.MonsterHit
                );
            }

            // 치명타 판정이면서 실제 피해가 0보다 클 때만
            // 치명타 효과음을 1회 재생합니다.
            if (isCritical && damage > 0f)
            {
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.CriticalHit
                );
            }


            if (hitAnimationCoroutine != null)
            {
                StopCoroutine(hitAnimationCoroutine);
            }

            if (knockback != default(Vector2) && rb != null)
            {
                rb.velocity += knockback * Mathf.Sqrt(Mathf.Max(0.01f, rb.drag));
                knockedBack = true;
            }

            if (currentHealth > 0)
            {
                hitAnimationCoroutine = StartCoroutine(HitAnimation());
            }
            else
            {
                StartCoroutine(Killed());
            }
        }

        protected IEnumerator HitAnimation()
        {
            if (monsterSpriteRenderer != null && whiteMaterial != null)
            {
                monsterSpriteRenderer.sharedMaterial = whiteMaterial;
            }

            yield return new WaitForSeconds(0.15f);

            if (monsterSpriteRenderer != null && defaultMaterial != null)
            {
                monsterSpriteRenderer.sharedMaterial = defaultMaterial;
            }

            knockedBack = false;
        }

        public virtual IEnumerator Killed(bool killedByPlayer = true)
        {
            alive = false;

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = false;
            }

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            if (killedByPlayer)
            {
                if (playerCharacter != null && playerCharacter.HealOnKill > 0)
                {
                    playerCharacter.GainHealth(playerCharacter.HealOnKill);
                }

                RewardSilver();
                DropLoot();
            }

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            yield return HitAnimation();

            if (deathParticles != null)
            {
                if (monsterSpriteRenderer != null)
                {
                    monsterSpriteRenderer.enabled = false;
                }

                if (shadow != null)
                {
                    shadow.SetActive(false);
                }

                yield return new WaitForSeconds(Mathf.Max(0f, deathParticles.main.duration - 0.15f));

                if (monsterSpriteRenderer != null)
                {
                    monsterSpriteRenderer.enabled = true;
                }

                if (shadow != null)
                {
                    shadow.SetActive(true);
                }
            }

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            if (entityManager != null)
            {
                entityManager.DespawnMonster(
                    monsterIndex,
                    this,
                    killedByPlayer
                );
            }
        }

        protected virtual void RewardSilver()
        {
            EliteMonsterBlueprint eliteBlueprint = monsterBlueprint as EliteMonsterBlueprint;

            int rewardCalls = 1;

            if (eliteBlueprint != null)
            {
                rewardCalls = Mathf.Max(1, eliteBlueprint.silverRewardCalls);
            }

            for (int i = 0; i < rewardCalls; i++)
            {
                SilverRunRewarder.RewardMonsterKill(monsterBlueprint);
            }

            if (eliteBlueprint != null && eliteBlueprint.debugLog)
            {
                Debug.Log(
                    $"[EliteMonster] Silver reward calls={rewardCalls} | name={monsterBlueprint.name}"
                );
            }
        }

        protected virtual void DropLoot()
        {
            MonsterBlueprint lootBlueprint = monsterBlueprint;

            EliteMonsterBlueprint eliteBlueprint = monsterBlueprint as EliteMonsterBlueprint;

            if (eliteBlueprint != null &&
                eliteBlueprint.useSourceLootTables &&
                eliteBlueprint.sourceNormalBlueprint != null)
            {
                lootBlueprint = eliteBlueprint.sourceNormalBlueprint;
            }

            if (lootBlueprint != null &&
                lootBlueprint.gemLootTable != null &&
                lootBlueprint.gemLootTable.TryDropLoot(out GemType gemType))
            {
                entityManager.SpawnExpGem((Vector2)transform.position, gemType);
            }

            TryDropCoinsWithStageEventModifier();
            TryDropEliteExtraCoins();
        }

        private void TryDropCoinsWithStageEventModifier()
        {
            bool isEliteMonster = monsterBlueprint is EliteMonsterBlueprint;

            if (StageEventRuntimeModifiers.ShouldForceGoldRushCoinDrop(isEliteMonster))
            {
                int forcedCoinCount = Mathf.Max(1, StageEventRuntimeModifiers.ForcedGoldRushCoinCount);

                for (int i = 0; i < forcedCoinCount; i++)
                {
                    entityManager.SpawnCoin(
                        (Vector2)transform.position,
                        StageEventRuntimeModifiers.ForcedGoldRushCoinType
                    );
                }

                if (StageEventRuntimeModifiers.DebugGoldRush)
                {
                    Debug.Log(
                        $"[GoldRush] Forced coin dropped | " +
                        $"type={StageEventRuntimeModifiers.ForcedGoldRushCoinType} | " +
                        $"count={forcedCoinCount} | " +
                        $"monster={monsterBlueprint.name}"
                    );
                }

                if (StageEventRuntimeModifiers.SuppressOriginalCoinDropsDuringGoldRush)
                {
                    return;
                }
            }

            int dropAttempts = StageEventRuntimeModifiers.GetCoinDropAttemptCount();
            int droppedCoinCount = 0;

            for (int i = 0; i < dropAttempts; i++)
            {
                if (monsterBlueprint.coinLootTable != null &&
                    monsterBlueprint.coinLootTable.TryDropLoot(out CoinType coinType))
                {
                    entityManager.SpawnCoin((Vector2)transform.position, coinType);
                    droppedCoinCount++;
                }
            }

            if (StageEventRuntimeModifiers.ShouldDropAdditionalCoin())
            {
                for (int i = 0; i < StageEventRuntimeModifiers.AdditionalCoinDropCount; i++)
                {
                    entityManager.SpawnCoin(
                        (Vector2)transform.position,
                        StageEventRuntimeModifiers.AdditionalCoinType
                    );

                    droppedCoinCount++;
                }
            }

            if (StageEventRuntimeModifiers.DebugGoldRush && droppedCoinCount > 0)
            {
                Debug.Log($"[GoldRush] Coin dropped | count={droppedCoinCount}");
            }
        }
        private void TryDropEliteExtraCoins()
        {
            EliteMonsterBlueprint eliteBlueprint = monsterBlueprint as EliteMonsterBlueprint;

            if (eliteBlueprint == null)
            {
                return;
            }

            int extraCoinCount = Mathf.Max(0, eliteBlueprint.guaranteedExtraCoinCount);

            if (extraCoinCount <= 0)
            {
                return;
            }

            float scatterRadius = Mathf.Max(0f, eliteBlueprint.extraCoinScatterRadius);

            for (int i = 0; i < extraCoinCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * scatterRadius;
                Vector2 spawnPosition = (Vector2)transform.position + randomOffset;

                entityManager.SpawnCoin(
                    spawnPosition,
                    eliteBlueprint.guaranteedExtraCoinType
                );
            }

            if (eliteBlueprint.debugLog)
            {
                Debug.Log(
                    $"[EliteMonster] Extra coins dropped | count={extraCoinCount} | " +
                    $"type={eliteBlueprint.guaranteedExtraCoinType}"
                );
            }
        }
    }
}
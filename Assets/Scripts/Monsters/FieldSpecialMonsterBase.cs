using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 필드에 독립적으로 생성되는 특수 몬스터 공통 베이스입니다.
    ///
    /// 중요:
    /// 기존 투사체/증강/상태이상 판정은 Monster 컴포넌트를 기준으로 동작합니다.
    /// 따라서 이 베이스도 IDamageable을 직접 상속하지 않고 Monster를 상속합니다.
    ///
    /// 이 구조로 바꾸면 영양 도둑균, 추격형 보물 몬스터도 기존 몬스터와 동일하게
    /// SyringeProjectile의 Monster 판정, 피격 플래시, 데미지 텍스트, 상태이상 흐름에 들어갑니다.
    /// </summary>
    [DisallowMultipleComponent]
    public abstract class FieldSpecialMonsterBase : Monster
    {
        [Header("Field Special Monster")]
        [Tooltip("필드 특수 몬스터 최대 체력입니다.")]
        [SerializeField] protected float maxHealth = 35f;

        [Tooltip("사망 후 오브젝트를 제거하기까지 기다리는 시간입니다.")]
        [SerializeField] protected float destroyDelayAfterDeath = 0.25f;

        [Tooltip("EntityManager.LivingMonsters에 등록할지 여부입니다. 켜두면 기존 몬스터 탐색/일부 전설증강 판정에 더 잘 잡힙니다.")]
        [SerializeField] private bool registerToLivingMonsters = true;

        [Header("Existing Monster Layer Rules")]
        [Tooltip("기존 몬스터의 몸통 피격 박스에 맞출 레이어 이름입니다. 보통 Monster Full을 사용합니다.")]
        [SerializeField] private string hitboxLayerName = "Monster Full";

        [Tooltip("기존 몬스터의 다리/루트 콜라이더에 맞출 레이어 이름입니다. 보통 Monster Legs를 사용합니다.")]
        [SerializeField] private string legsLayerName = "Monster Legs";

        [Tooltip("기존 몬스터처럼 루트 CircleCollider2D를 Trigger로 유지할지 여부입니다.")]
        [SerializeField] private bool rootLegsColliderIsTrigger = true;

        [Header("Field Special Debug")]
        [Tooltip("필드 특수 몬스터 전용 디버그 로그를 출력합니다.")]
        [SerializeField] private bool fieldSpecialDebugLog = false;

        protected FieldSpecialMonsterSpawner ownerSpawner;

        private bool removalNotified;
        private bool registeredToLivingMonsters;

        public bool IsAlive => alive;
        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;

        // 기존 NutritionThiefBacteriaMonster / TreasureRunnerMonster에서 debugLog를 쓰고 있으므로
        // SerializeField 중복 오류를 피하기 위해 필드가 아니라 읽기 전용 프로퍼티로 제공합니다.
        protected bool debugLog => fieldSpecialDebugLog;

        protected override void Awake()
        {
            // Monster.Awake()가 기존 몬스터 규격의 핵심입니다.
            // 여기서 Rigidbody2D, CircleCollider2D, SpriteRenderer, BoxCollider2D hitbox,
            // ZPositioner 등을 기존 방식으로 세팅합니다.
            base.Awake();

            ConfigureFieldSpecialPhysics();
            ApplyExistingMonsterLayers();
            ResetRuntimeState();
        }

        protected virtual void OnEnable()
        {
            ResetRuntimeState();
        }

        protected override void Update()
        {
            if (!alive)
            {
                return;
            }

            OnAliveUpdate();
            UpdateSpriteDirectionByVelocity();
        }

        protected override void FixedUpdate()
        {
            if (!alive)
            {
                return;
            }

            OnAliveFixedUpdate();
        }

        public void SetupRuntime(
            FieldSpecialMonsterSpawner spawner,
            EntityManager manager,
            Character player)
        {
            ownerSpawner = spawner;

            if (manager != null)
            {
                entityManager = manager;
            }

            if (player != null)
            {
                playerCharacter = player;
            }

            ConfigureFieldSpecialPhysics();
            ApplyExistingMonsterLayers();
            ResetRuntimeState();
            RegisterToLivingMonstersIfNeeded();
        }

        public void DespawnWithoutReward()
        {
            if (!alive)
            {
                return;
            }

            StartCoroutine(Killed(false));
        }

        protected virtual void OnAliveUpdate()
        {
        }

        protected virtual void OnAliveFixedUpdate()
        {
        }

        protected virtual void OnKilledByPlayer()
        {
        }

        protected virtual void OnExpiredOrRemoved()
        {
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            if (!alive && removalNotified)
            {
                yield break;
            }

            alive = false;

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = false;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = false;
            }

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            RemoveFromLivingMonstersIfNeeded();

            if (killedByPlayer)
            {
                OnKilledByPlayer();
            }
            else
            {
                OnExpiredOrRemoved();
            }

            NotifyRemovedOnce();

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            // 기존 Monster.cs의 피격 플래시 규격을 그대로 사용합니다.
            yield return HitAnimation();

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = false;
            }

            if (shadow != null)
            {
                shadow.SetActive(false);
            }

            float waitTime = Mathf.Max(0f, destroyDelayAfterDeath);

            if (deathParticles != null)
            {
                waitTime = Mathf.Max(
                    waitTime,
                    Mathf.Max(0f, deathParticles.main.duration - 0.15f));
            }

            if (waitTime > 0f)
            {
                yield return new WaitForSeconds(waitTime);
            }

            Destroy(gameObject);
        }

        protected virtual void ResetRuntimeState()
        {
            alive = true;
            currentHealth = Mathf.Max(1f, maxHealth);
            removalNotified = false;

            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (monsterLegsCollider == null)
            {
                monsterLegsCollider = GetComponent<CircleCollider2D>();
            }

            if (monsterSpriteRenderer == null)
            {
                monsterSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            }

            if (monsterHitbox == null && monsterSpriteRenderer != null)
            {
                monsterHitbox = monsterSpriteRenderer.GetComponent<BoxCollider2D>();

                if (monsterHitbox == null)
                {
                    monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>();
                }
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;
                monsterHitbox.isTrigger = true;

                if (monsterSpriteRenderer != null)
                {
                    monsterHitbox.size = monsterSpriteRenderer.bounds.size;
                    monsterHitbox.offset = Vector2.up * monsterHitbox.size.y / 2f;
                }
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = true;
                monsterLegsCollider.isTrigger = rootLegsColliderIsTrigger;

                if (monsterHitbox != null)
                {
                    monsterLegsCollider.radius = Mathf.Max(0.05f, monsterHitbox.size.x / 2.5f);
                }
            }

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = true;

                if (defaultMaterial != null)
                {
                    monsterSpriteRenderer.sharedMaterial = defaultMaterial;
                }
            }

            if (shadow != null)
            {
                shadow.SetActive(true);
            }

            EnsureCenterTransform();
            ConfigureFieldSpecialPhysics();
            ApplyExistingMonsterLayers();
        }

        protected void DropExpValueAroundSelf(int totalExp)
        {
            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager == null)
            {
                return;
            }

            int remaining = Mathf.Max(0, totalExp);

            remaining = SpawnExpByUnit(remaining, GemType.Red50, 50);
            remaining = SpawnExpByUnit(remaining, GemType.Green10, 10);
            remaining = SpawnExpByUnit(remaining, GemType.Blue2, 2);
            remaining = SpawnExpByUnit(remaining, GemType.White1, 1);
        }

        protected void DropCoinValueAroundSelf(int totalCoin)
        {
            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager == null)
            {
                return;
            }

            int remaining = Mathf.Max(0, totalCoin);

            remaining = SpawnCoinByUnit(remaining, CoinType.Bag50, 50);
            remaining = SpawnCoinByUnit(remaining, CoinType.Pouch30, 30);
            remaining = SpawnCoinByUnit(remaining, CoinType.Gold5, 5);
            remaining = SpawnCoinByUnit(remaining, CoinType.Silver2, 2);
            remaining = SpawnCoinByUnit(remaining, CoinType.Bronze1, 1);
        }

        protected Vector2 GetRandomDropPosition()
        {
            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.01f)
            {
                randomDirection = Vector2.right;
            }

            Vector2 offset = randomDirection.normalized * Random.Range(0.15f, 0.85f);
            return (Vector2)transform.position + offset;
        }

        private int SpawnExpByUnit(int remaining, GemType gemType, int unitValue)
        {
            while (remaining >= unitValue)
            {
                entityManager.SpawnExpGem(GetRandomDropPosition(), gemType, true);
                remaining -= unitValue;
            }

            return remaining;
        }

        private int SpawnCoinByUnit(int remaining, CoinType coinType, int unitValue)
        {
            while (remaining >= unitValue)
            {
                entityManager.SpawnCoin(GetRandomDropPosition(), coinType, true);
                remaining -= unitValue;
            }

            return remaining;
        }

        private void ConfigureFieldSpecialPhysics()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.isTrigger = true;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.isTrigger = rootLegsColliderIsTrigger;
            }
        }

        private void ApplyExistingMonsterLayers()
        {
            int hitboxLayer = GetLayerIndexWithFallback(hitboxLayerName, legsLayerName);
            int legsLayer = GetLayerIndexWithFallback(legsLayerName, hitboxLayerName);

            if (hitboxLayer >= 0 && monsterHitbox != null)
            {
                monsterHitbox.gameObject.layer = hitboxLayer;
            }

            if (legsLayer >= 0)
            {
                gameObject.layer = legsLayer;

                if (monsterLegsCollider != null)
                {
                    monsterLegsCollider.gameObject.layer = legsLayer;
                }
            }
        }

        private int GetLayerIndexWithFallback(string primaryName, string fallbackName)
        {
            int primary = LayerMask.NameToLayer(primaryName);

            if (primary >= 0)
            {
                return primary;
            }

            int fallback = LayerMask.NameToLayer(fallbackName);

            if (fallback >= 0)
            {
                return fallback;
            }

            if (fieldSpecialDebugLog)
            {
                Debug.LogWarning(
                    $"[{GetType().Name}] 레이어를 찾지 못했습니다. primary={primaryName}, fallback={fallbackName}",
                    this);
            }

            return -1;
        }

        private void EnsureCenterTransform()
        {
            if (centerTransform == null)
            {
                centerTransform = new GameObject("Center Transform").transform;
                centerTransform.SetParent(transform);
            }

            if (monsterHitbox != null)
            {
                centerTransform.position = transform.position + (Vector3)monsterHitbox.offset;
            }
            else
            {
                centerTransform.position = transform.position;
            }
        }

        private void RegisterToLivingMonstersIfNeeded()
        {
            if (!registerToLivingMonsters)
            {
                return;
            }

            if (registeredToLivingMonsters)
            {
                return;
            }

            if (entityManager == null)
            {
                return;
            }

            if (!entityManager.LivingMonsters.Contains(this))
            {
                entityManager.LivingMonsters.Add(this);
            }

            registeredToLivingMonsters = true;
        }

        private void RemoveFromLivingMonstersIfNeeded()
        {
            if (!registeredToLivingMonsters)
            {
                return;
            }

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            registeredToLivingMonsters = false;
        }

        private void NotifyRemovedOnce()
        {
            if (removalNotified)
            {
                return;
            }

            removalNotified = true;
            ownerSpawner?.NotifySpecialMonsterRemoved(this);
        }

        private void UpdateSpriteDirectionByVelocity()
        {
            if (monsterSpriteRenderer == null || rb == null)
            {
                return;
            }

            if (Mathf.Abs(rb.velocity.x) > 0.02f)
            {
                monsterSpriteRenderer.flipX = rb.velocity.x < 0f;
            }
        }

        protected virtual void OnDisable()
        {
            RemoveFromLivingMonstersIfNeeded();
            NotifyRemovedOnce();
        }

        protected virtual void OnDestroy()
        {
            RemoveFromLivingMonstersIfNeeded();
            NotifyRemovedOnce();
        }
    }
}
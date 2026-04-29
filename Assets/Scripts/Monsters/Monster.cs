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

        public Transform CenterTransform { get => centerTransform; }

        public UnityEvent<Monster> OnKilled { get; } = new UnityEvent<Monster>();

        public float HP => currentHealth;

        public Vector2 Position => transform.position;
        public Vector2 Size => monsterLegsCollider.bounds.size;
        public Dictionary<int, int> ListIndexByCellIndex { get; set; }
        public int QueryID { get; set; } = -1;

        protected virtual void Awake()
        {
            rb = GetComponent<Rigidbody2D>();
            monsterLegsCollider = GetComponent<CircleCollider2D>();
            monsterSpriteAnimator = GetComponentInChildren<SpriteAnimator>();
            monsterSpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            zPositioner = gameObject.AddComponent<ZPositioner>();

            if (monsterSpriteRenderer != null)
            {
                monsterHitbox = monsterSpriteRenderer.gameObject.AddComponent<BoxCollider2D>();
                monsterHitbox.isTrigger = true;
            }
        }

        public virtual void Init(EntityManager entityManager, Character playerCharacter)
        {
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;
            zPositioner.Init(playerCharacter.transform);
        }

        public virtual void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            this.monsterIndex = monsterIndex;
            this.monsterBlueprint = monsterBlueprint;

            rb.position = position;
            transform.position = position;

            currentHealth = monsterBlueprint.hp + hpBuff;
            alive = true;

            entityManager.LivingMonsters.Add(this);

            monsterSpriteAnimator.Init(monsterBlueprint.walkSpriteSequence, monsterBlueprint.walkFrameTime, true);
            monsterSpriteAnimator.StartAnimating(true);

            monsterHitbox.enabled = true;
            monsterHitbox.size = monsterSpriteRenderer.bounds.size;
            monsterHitbox.offset = Vector2.up * monsterHitbox.size.y / 2f;

            monsterLegsCollider.radius = monsterHitbox.size.x / 2.5f;

            centerTransform = (new GameObject("Center Transform")).transform;
            centerTransform.SetParent(transform);
            centerTransform.position = transform.position + (Vector3)monsterHitbox.offset;

            float spd = Random.Range(monsterBlueprint.movespeed - 0.1f, monsterBlueprint.movespeed + 0.1f);
            rb.drag = monsterBlueprint.acceleration / (spd * spd);

            rb.velocity = Vector2.zero;

            StopAllCoroutines();
        }

        protected virtual void Update()
        {
            if (playerCharacter == null || monsterSpriteRenderer == null)
            {
                return;
            }

            monsterSpriteRenderer.flipX = ((playerCharacter.transform.position.x - rb.position.x) < 0);
        }

        protected virtual void FixedUpdate()
        {
        }

        public override void Knockback(Vector2 knockback)
        {
            rb.velocity += knockback * Mathf.Sqrt(rb.drag);
        }

        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
        {
            if (!alive)
            {
                return;
            }

            entityManager.SpawnDamageText(monsterHitbox.transform.position, damage);

            currentHealth -= damage;

            if (hitAnimationCoroutine != null)
            {
                StopCoroutine(hitAnimationCoroutine);
            }

            if (knockback != default(Vector2))
            {
                rb.velocity += knockback * Mathf.Sqrt(rb.drag);
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
            monsterSpriteRenderer.sharedMaterial = whiteMaterial;

            yield return new WaitForSeconds(0.15f);

            monsterSpriteRenderer.sharedMaterial = defaultMaterial;
            knockedBack = false;
        }

        public virtual IEnumerator Killed(bool killedByPlayer = true)
        {
            alive = false;
            monsterHitbox.enabled = false;

            entityManager.LivingMonsters.Remove(this);

            if (killedByPlayer)
            {
                DropLoot();
            }

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            yield return HitAnimation();

            if (deathParticles != null)
            {
                monsterSpriteRenderer.enabled = false;
                shadow.SetActive(false);

                yield return new WaitForSeconds(deathParticles.main.duration - 0.15f);

                monsterSpriteRenderer.enabled = true;
                shadow.SetActive(true);
            }

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            entityManager.DespawnMonster(monsterIndex, this, true);
        }

        protected virtual void DropLoot()
        {
            if (monsterBlueprint.gemLootTable.TryDropLoot(out GemType gemType))
            {
                entityManager.SpawnExpGem((Vector2)transform.position, gemType);
            }

            TryDropCoinsWithStageEventModifier();
        }

        private void TryDropCoinsWithStageEventModifier()
        {
            int dropAttempts = StageEventRuntimeModifiers.GetCoinDropAttemptCount();
            int droppedCoinCount = 0;

            for (int i = 0; i < dropAttempts; i++)
            {
                if (monsterBlueprint.coinLootTable.TryDropLoot(out CoinType coinType))
                {
                    entityManager.SpawnCoin((Vector2)transform.position, coinType);
                    droppedCoinCount++;
                }
            }

            if (StageEventRuntimeModifiers.ShouldDropAdditionalCoin())
            {
                for (int i = 0; i < StageEventRuntimeModifiers.AdditionalCoinDropCount; i++)
                {
                    entityManager.SpawnCoin((Vector2)transform.position, StageEventRuntimeModifiers.AdditionalCoinType);
                    droppedCoinCount++;
                }
            }

            if (StageEventRuntimeModifiers.DebugGoldRush && droppedCoinCount > 0)
            {
                Debug.Log($"[GoldRush] Coin dropped | count={droppedCoinCount}");
            }
        }
    }
}
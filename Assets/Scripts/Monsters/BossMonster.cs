using System.Collections;
using System.Linq;
using UnityEngine;

namespace Vampire
{
    public class BossMonster : Monster
    {
        protected new BossMonsterBlueprint monsterBlueprint;
        protected BossAbility[] abilities;

        protected Coroutine act = null;

        [Header("Boss Controller Link")]
        [Tooltip("보스 페이즈와 패턴을 관리하는 BossController입니다. 비워두면 같은 오브젝트에서 자동으로 찾습니다.")]
        [SerializeField] private BossController bossController;

        [Header("Boss Death Animation")]
        [Tooltip("체크하면 보스 사망 시 Animator Trigger를 호출합니다. 아직 애니메이션이 없으면 꺼두세요.")]
        [SerializeField] private bool useDeathAnimatorTrigger = false;

        [Tooltip("보스 사망 애니메이션을 재생할 Animator입니다. 비워두면 자식 오브젝트에서 자동으로 찾습니다.")]
        [SerializeField] private Animator deathAnimator;

        [Tooltip("보스 사망 애니메이션 Trigger 이름입니다. Animator에 같은 이름의 Trigger 파라미터가 있어야 합니다.")]
        [SerializeField] private string deathTriggerName = "Die";

        [Tooltip("보스 사망 후 보상 생성 전까지 기다리는 시간입니다. 사망 애니메이션 길이에 맞춰 조절하세요.")]
        [SerializeField] private float deathAnimationWaitTime = 1.5f;

        [Tooltip("체크하면 사망 연출 대기 후 보스 스프라이트와 그림자를 숨깁니다.")]
        [SerializeField] private bool hideBossVisualAfterDeath = true;

        [Header("Boss Clear Reward")]
        [Tooltip("체크하면 보스가 죽어도 바로 클리어 처리하지 않고, 보스 클리어 보상을 먹었을 때 클리어 처리합니다.")]
        [SerializeField] private bool delayClearUntilRewardCollected = true;

        [Tooltip("보스 사망 후 생성할 클리어 보상 프리팹입니다. 이 프리팹에는 Collider2D Is Trigger와 BossClearRewardPickup이 있으면 좋습니다.")]
        [SerializeField] private GameObject bossClearRewardPrefab;

        [Tooltip("보스 위치 기준으로 클리어 보상이 생성될 위치 보정값입니다.")]
        [SerializeField] private Vector3 bossClearRewardSpawnOffset = Vector3.zero;

        [Tooltip("보상 프리팹이 없을 때 즉시 클리어 처리할지 여부입니다. 체크를 끄면 보상 프리팹이 없을 때 클리어가 막힐 수 있습니다.")]
        [SerializeField] private bool clearImmediatelyIfRewardPrefabMissing = true;

        [Header("Default Loot")]
        [Tooltip("체크하면 기존 Monster/BossMonster의 기본 드랍도 같이 실행합니다. 기존 보스 상자 드랍을 유지하고 싶으면 체크하세요.")]
        [SerializeField] private bool dropDefaultLootOnDeath = false;

        [Header("Debug")]
        [Tooltip("체크하면 보스 사망/보상/클리어 처리 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugBossDeath = true;

        private float bossMaxHealth = 1f;
        private bool clearCompleted = false;

        public Rigidbody2D Rigidbody
        {
            get => rb;
        }

        public SpriteAnimator Animator
        {
            get => monsterSpriteAnimator;
        }

        protected float timeSinceLastMeleeAttack;

        protected override void Awake()
        {
            base.Awake();

            ResolveBossController();

            if (deathAnimator == null)
            {
                deathAnimator = GetComponentInChildren<Animator>();
            }
        }

        public override void Setup(int monsterIndex, Vector2 position, MonsterBlueprint monsterBlueprint, float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            this.monsterBlueprint = (BossMonsterBlueprint)monsterBlueprint;

            bossMaxHealth = currentHealth;
            clearCompleted = false;

            if (monsterSpriteRenderer != null)
            {
                monsterSpriteRenderer.enabled = true;
            }

            if (shadow != null)
            {
                shadow.SetActive(true);
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = true;
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = true;
            }

            ResolveBossController();

            if (bossController != null)
            {
                bossController.NotifyBossHealthInitialized(currentHealth, bossMaxHealth);

                if (playerCharacter != null)
                {
                    bossController.SetPlayerCharacter(playerCharacter);
                }
            }

            abilities = new BossAbility[this.monsterBlueprint.abilityPrefabs.Length];

            for (int i = 0; i < abilities.Length; i++)
            {
                abilities[i] = Instantiate(this.monsterBlueprint.abilityPrefabs[i], transform).GetComponent<BossAbility>();
                abilities[i].Init(this, entityManager, playerCharacter);
            }

            act = StartCoroutine(Act());
        }

        protected override void Update()
        {
            base.Update();
            timeSinceLastMeleeAttack += Time.deltaTime;
        }

        public void Move(Vector2 direction, float deltaTime)
        {
            rb.velocity += direction * monsterBlueprint.acceleration * deltaTime;
        }

        public void Freeze()
        {
            rb.velocity = Vector2.zero;
        }

        public override void TakeDamage(float damage, Vector2 knockback = default(Vector2))
        {
            ResolveBossController();

            if (bossController != null && bossController.IsInvincibleToDamage)
            {
                if (debugBossDeath)
                {
                    Debug.Log("[BossMonster] 페이즈 전환/무적 상태라 데미지를 무시했습니다.");
                }

                return;
            }

            base.TakeDamage(damage, knockback);

            if (bossController != null)
            {
                bossController.NotifyBossHealthChanged(currentHealth, bossMaxHealth);
            }
        }

        private IEnumerator Act()
        {
            while (true)
            {
                if (abilities == null || abilities.Length == 0)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                float[] abilityScores = abilities.Select(a => a.Score()).ToArray();
                float totalScore = abilityScores.Sum();

                if (totalScore <= 0f)
                {
                    yield return new WaitForSeconds(1f);
                    continue;
                }

                float rand = Random.Range(0f, totalScore);
                float cumulative = 0f;
                int abilityIndex = -1;

                for (int i = 0; i < abilities.Length; i++)
                {
                    abilities[i].Deactivate();

                    cumulative += abilityScores[i];

                    if (abilityIndex == -1 && rand < cumulative)
                    {
                        abilityIndex = i;
                    }
                }

                if (abilityIndex == -1)
                {
                    yield return new WaitForSeconds(1f);
                }
                else
                {
                    yield return abilities[abilityIndex].Activate();
                }
            }
        }

        protected override void DropLoot()
        {
            base.DropLoot();

            if (monsterBlueprint.chestBlueprint != null)
            {
                entityManager.SpawnChest(monsterBlueprint.chestBlueprint, transform.position);
            }
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            alive = false;

            ResolveBossController();

            if (bossController != null)
            {
                bossController.NotifyBossDeathStarted();
            }

            if (abilities != null)
            {
                foreach (BossAbility ability in abilities)
                {
                    if (ability != null)
                    {
                        Destroy(ability.gameObject);
                    }
                }
            }

            if (act != null)
            {
                StopCoroutine(act);
                act = null;
            }

            if (monsterHitbox != null)
            {
                monsterHitbox.enabled = false;
            }

            if (monsterLegsCollider != null)
            {
                monsterLegsCollider.enabled = false;
            }

            if (entityManager != null)
            {
                entityManager.LivingMonsters.Remove(this);
            }

            if (killedByPlayer && dropDefaultLootOnDeath)
            {
                DropLoot();
            }

            if (deathParticles != null)
            {
                deathParticles.Play();
            }

            if (useDeathAnimatorTrigger && deathAnimator != null && !string.IsNullOrWhiteSpace(deathTriggerName))
            {
                deathAnimator.SetTrigger(deathTriggerName);
            }

            if (debugBossDeath)
            {
                Debug.Log("[BossMonster] Boss death animation wait started.");
            }

            yield return new WaitForSeconds(Mathf.Max(0f, deathAnimationWaitTime));

            if (hideBossVisualAfterDeath)
            {
                if (monsterSpriteRenderer != null)
                {
                    monsterSpriteRenderer.enabled = false;
                }

                if (shadow != null)
                {
                    shadow.SetActive(false);
                }
            }

            if (delayClearUntilRewardCollected)
            {
                SpawnBossClearRewardOrFallback();
            }
            else
            {
                CompleteBossClear();
            }
        }

        private void SpawnBossClearRewardOrFallback()
        {
            if (bossClearRewardPrefab == null)
            {
                Debug.LogWarning("[BossMonster] Boss Clear Reward Prefab이 비어 있습니다.");

                if (clearImmediatelyIfRewardPrefabMissing)
                {
                    CompleteBossClear();
                }

                return;
            }

            Vector3 spawnPosition = transform.position + bossClearRewardSpawnOffset;
            GameObject rewardObject = Instantiate(bossClearRewardPrefab, spawnPosition, Quaternion.identity);

            BossClearRewardPickup rewardPickup = rewardObject.GetComponent<BossClearRewardPickup>();

            if (rewardPickup == null)
            {
                rewardPickup = rewardObject.AddComponent<BossClearRewardPickup>();
            }

            rewardPickup.Init(this);

            if (debugBossDeath)
            {
                Debug.Log("[BossMonster] Boss clear reward spawned. Level clear waits until reward pickup.");
            }
        }

        public void NotifyBossClearRewardCollected()
        {
            if (clearCompleted)
            {
                return;
            }

            if (debugBossDeath)
            {
                Debug.Log("[BossMonster] Boss clear reward collected. Level clear now.");
            }

            CompleteBossClear();
        }

        private void CompleteBossClear()
        {
            if (clearCompleted)
            {
                return;
            }

            clearCompleted = true;

            OnKilled.Invoke(this);
            OnKilled.RemoveAllListeners();

            if (entityManager != null)
            {
                entityManager.DespawnMonster(monsterIndex, this, true);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ResolveBossController()
        {
            if (bossController == null)
            {
                bossController = GetComponent<BossController>();
            }
        }

        private void OnCollisionEnter2D(Collision2D col)
        {
            if (((monsterBlueprint.meleeLayer & (1 << col.collider.gameObject.layer)) != 0))
            {
                IDamageable damageable = col.collider.GetComponentInParent<IDamageable>();

                if (damageable == null)
                {
                    return;
                }

                Vector2 knockbackDirection = (damageable.transform.position - transform.position).normalized;

                if (timeSinceLastMeleeAttack > monsterBlueprint.meleeAttackDelay)
                {
                    damageable.TakeDamage(monsterBlueprint.meleeDamage, monsterBlueprint.meleeKnockback * knockbackDirection);
                    timeSinceLastMeleeAttack = 0f;
                }
                else
                {
                    damageable.TakeDamage(0f, monsterBlueprint.meleeKnockback * knockbackDirection);
                }
            }

            if (col.gameObject.TryGetComponent(out Chest chest))
            {
                chest.OpenChest(false);
            }
        }
    }
}
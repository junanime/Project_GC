using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 추격형 보물 몬스터.
    ///
    /// 기존 Monster 규격을 그대로 사용합니다.
    /// - MonsterBlueprint로 풀 생성
    /// - LevelBlueprint.monsters에 등록
    /// - TimedSpecialMonsterSpawner에서 MonsterBlueprint로 스폰
    ///
    /// 기능:
    /// - 플레이어에게서 도망감
    /// - 제한 시간 내 처치하면 큰 보상
    /// - 시간 초과 시 보상 없이 사라짐
    /// </summary>
    public class TreasureRunnerMonster : Monster
    {
        [Header("Treasure Runner")]
        [Tooltip("보물 몬스터가 도망치는 시간입니다. 이 시간이 지나면 보상 없이 사라집니다.")]
        [SerializeField] private float lifetime = 18f;

        [Tooltip("기본 도망 이동속도입니다.")]
        [SerializeField] private float fleeMoveSpeed = 2.35f;

        [Tooltip("플레이어가 이 거리 안에 있으면 더 강하게 도망갑니다.")]
        [SerializeField] private float panicDistance = 5f;

        [Tooltip("패닉 상태일 때 이동속도 배율입니다.")]
        [SerializeField] private float panicSpeedMultiplier = 1.35f;

        [Tooltip("도망 방향이 너무 단조롭지 않도록 추가되는 흔들림 세기입니다.")]
        [SerializeField] private float sideNoiseStrength = 0.35f;

        [Header("Reward")]
        [Tooltip("처치 시 드랍할 경험치 총량입니다.")]
        [SerializeField] private int rewardExpValue = 30;

        [Tooltip("처치 시 드랍할 골드 총량입니다.")]
        [SerializeField] private int rewardCoinValue = 30;

        [Tooltip("제한 시간의 절반 안에 빠르게 처치하면 추가 골드를 지급합니다.")]
        [SerializeField] private bool useQuickKillBonus = true;

        [Tooltip("빠르게 처치했을 때 추가 골드량입니다.")]
        [SerializeField] private int quickKillBonusCoinValue = 15;

        [Header("Debug")]
        [Tooltip("추격형 보물 몬스터 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        private float spawnTime;
        private bool deathHandled;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            spawnTime = Time.time;
            deathHandled = false;
        }

        protected override void Update()
        {
            base.Update();

            if (!alive)
            {
                return;
            }

            if (Time.time - spawnTime >= lifetime)
            {
                if (debugLog)
                {
                    Debug.Log("[추격형 보물 몬스터] 시간 초과로 도망쳤습니다.", this);
                }

                StartCoroutine(Killed(false));
            }
        }

        protected override void FixedUpdate()
        {
            if (!alive || rb == null)
            {
                return;
            }

            if (playerCharacter == null)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            Vector2 awayFromPlayer =
                (Vector2)transform.position - (Vector2)playerCharacter.transform.position;

            if (awayFromPlayer.sqrMagnitude < 0.01f)
            {
                awayFromPlayer = Random.insideUnitCircle;
            }

            awayFromPlayer.Normalize();

            Vector2 sideNoise = new Vector2(-awayFromPlayer.y, awayFromPlayer.x)
                * Mathf.Sin(Time.time * 3.5f)
                * sideNoiseStrength;

            float distance = Vector2.Distance(transform.position, playerCharacter.transform.position);
            float speed = fleeMoveSpeed;

            if (distance <= panicDistance)
            {
                speed *= panicSpeedMultiplier;
            }

            rb.velocity = (awayFromPlayer + sideNoise).normalized * speed;
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            if (deathHandled)
            {
                yield break;
            }

            deathHandled = true;

            if (killedByPlayer)
            {
                DropTreasureReward();
            }

            // 중요:
            // base.Killed(true)를 호출하면 MonsterBlueprint 기본 DropLoot도 같이 떨어집니다.
            // 보물 몬스터는 전용 보상만 드랍하게 하고, 풀 반환은 기존 Monster.Killed(false)를 사용합니다.
            yield return base.Killed(false);
        }

        private void DropTreasureReward()
        {
            int finalCoin = rewardCoinValue;

            if (useQuickKillBonus && Time.time - spawnTime <= lifetime * 0.5f)
            {
                finalCoin += quickKillBonusCoinValue;
            }

            DropExpValueAroundSelf(rewardExpValue);
            DropCoinValueAroundSelf(finalCoin);

            if (debugLog)
            {
                Debug.Log(
                    $"[추격형 보물 몬스터] 처치 보상 드랍 | Exp={rewardExpValue}, Coin={finalCoin}",
                    this);
            }
        }

        private void DropExpValueAroundSelf(int totalExp)
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

        private void DropCoinValueAroundSelf(int totalCoin)
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

        private Vector2 GetRandomDropPosition()
        {
            Vector2 randomDirection = Random.insideUnitCircle;

            if (randomDirection.sqrMagnitude < 0.01f)
            {
                randomDirection = Vector2.right;
            }

            Vector2 offset = randomDirection.normalized * Random.Range(0.15f, 0.85f);
            return (Vector2)transform.position + offset;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, panicDistance);
        }
    }
}
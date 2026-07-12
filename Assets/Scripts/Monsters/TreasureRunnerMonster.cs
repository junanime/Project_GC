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
    /// 변경된 동작:
    /// - 스폰 직후에는 플레이어 쪽으로 접근합니다.
    /// - 플레이어와의 거리가 fleeStartDistance 이하가 되면 도망 상태로 전환됩니다.
    /// - 한 번 도망 상태가 되면 다시 접근하지 않고 계속 도망갑니다.
    /// - 제한 시간 안에 처치하면 큰 보상을 드랍합니다.
    /// - 제한 시간이 지나면 보상 없이 사라집니다.
    /// </summary>
    public class TreasureRunnerMonster : Monster
    {
        [Header("Approach Then Flee")]
        [Tooltip("스폰 직후 플레이어에게 접근할 때의 이동속도입니다.")]
        [SerializeField] private float approachMoveSpeed = 1.6f;

        [Tooltip("플레이어와 이 거리 이하가 되면 도망 상태로 전환됩니다.")]
        [SerializeField] private float fleeStartDistance = 3f;

        [Tooltip("한 번 도망 상태가 되면 다시 접근 상태로 돌아가지 않게 합니다.")]
        [SerializeField] private bool keepFleeingAfterTriggered = true;

        [Tooltip("접근 중 이동이 너무 직선적이지 않도록 살짝 흔들리는 정도입니다.")]
        [SerializeField] private float approachSideNoiseStrength = 0.12f;

        [Header("Treasure Runner")]
        [Tooltip("보물 몬스터가 살아 있는 시간입니다. 이 시간이 지나면 보상 없이 사라집니다.")]
        [SerializeField] private float lifetime = 18f;

        [Tooltip("도망 상태일 때 기본 이동속도입니다.")]
        [SerializeField] private float fleeMoveSpeed = 2.35f;

        [Tooltip("도망 상태에서 플레이어가 이 거리 안에 있으면 더 강하게 도망갑니다.")]
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
        private bool hasStartedFleeing;

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);

            spawnTime = Time.time;
            deathHandled = false;
            hasStartedFleeing = false;

            if (debugLog)
            {
                Debug.Log(
                    $"[추격형 보물 몬스터] 스폰 완료. 접근 시작 | FleeStartDistance={fleeStartDistance}",
                    this);
            }
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

            Vector2 monsterPosition = rb.position;
            Vector2 playerPosition = playerCharacter.transform.position;
            Vector2 toPlayer = playerPosition - monsterPosition;

            float distanceToPlayer = toPlayer.magnitude;

            if (distanceToPlayer <= Mathf.Max(0.1f, fleeStartDistance))
            {
                if (!hasStartedFleeing && debugLog)
                {
                    Debug.Log(
                        $"[추격형 보물 몬스터] 플레이어 접근 감지. 도망 시작 | Distance={distanceToPlayer:F2}",
                        this);
                }

                hasStartedFleeing = true;
            }

            if (!hasStartedFleeing || (!keepFleeingAfterTriggered && distanceToPlayer > fleeStartDistance))
            {
                MoveTowardPlayer(toPlayer);
                return;
            }

            FleeFromPlayer(toPlayer, distanceToPlayer);
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

            // 보물 몬스터는 전용 보상만 드랍합니다.
            // base.Killed(true)를 호출하면 MonsterBlueprint 기본 DropLoot도 같이 떨어질 수 있으므로 false로 넘깁니다.
            yield return base.Killed(false);
        }

        private void MoveTowardPlayer(Vector2 toPlayer)
        {
            if (toPlayer.sqrMagnitude < 0.01f)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            Vector2 direction = toPlayer.normalized;

            Vector2 sideNoise = new Vector2(-direction.y, direction.x)
                * Mathf.Sin(Time.time * 2.5f)
                * approachSideNoiseStrength;

            rb.velocity = (direction + sideNoise).normalized * approachMoveSpeed;
        }

        private void FleeFromPlayer(Vector2 toPlayer, float distanceToPlayer)
        {
            Vector2 awayFromPlayer = -toPlayer;

            if (awayFromPlayer.sqrMagnitude < 0.01f)
            {
                awayFromPlayer = Random.insideUnitCircle;

                if (awayFromPlayer.sqrMagnitude < 0.01f)
                {
                    awayFromPlayer = Vector2.right;
                }
            }

            awayFromPlayer.Normalize();

            Vector2 sideNoise = new Vector2(-awayFromPlayer.y, awayFromPlayer.x)
                * Mathf.Sin(Time.time * 3.5f)
                * sideNoiseStrength;

            float speed = fleeMoveSpeed;

            if (distanceToPlayer <= panicDistance)
            {
                speed *= panicSpeedMultiplier;
            }

            rb.velocity = (awayFromPlayer + sideNoise).normalized * speed;
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
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, fleeStartDistance);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, panicDistance);
        }
    }
}
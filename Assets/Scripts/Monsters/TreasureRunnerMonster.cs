using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 추격형 보물 몬스터.
    ///
    /// 플레이어에게서 도망가는 보상형 특수 몬스터입니다.
    /// 제한 시간 안에 처치하면 큰 보상을 드랍하고,
    /// 제한 시간이 지나면 보상 없이 사라집니다.
    /// </summary>
    public class TreasureRunnerMonster : FieldSpecialMonsterBase
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

        private float spawnTime;

        protected override void ResetRuntimeState()
        {
            base.ResetRuntimeState();
            spawnTime = Time.time;
        }

        protected override void OnAliveUpdate()
        {
            if (Time.time - spawnTime >= lifetime)
            {
                if (debugLog)
                {
                    Debug.Log("[추격형 보물 몬스터] 시간 초과로 도망쳤습니다.", this);
                }

                DespawnWithoutReward();
            }
        }

        protected override void OnAliveFixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }

            if (playerCharacter == null)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            Vector2 awayFromPlayer = ((Vector2)transform.position - (Vector2)playerCharacter.transform.position);

            if (awayFromPlayer.sqrMagnitude < 0.01f)
            {
                awayFromPlayer = Random.insideUnitCircle.normalized;
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

        protected override void OnKilledByPlayer()
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
                    $"[추격형 보물 몬스터] 처치 보상 드랍. exp={rewardExpValue}, coin={finalCoin}",
                    this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, panicDistance);
        }
    }
}
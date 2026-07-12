using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 영양 도둑균.
    ///
    /// 필드에 떨어진 경험치 젬과 코인을 몰래 흡수해서 몸 안에 저장합니다.
    /// 방치하면 저장량에 따라 크기와 이동속도가 증가합니다.
    /// 처치하면 먹은 경험치/골드를 다시 뱉고, 일정량 이상 먹었으면 보너스 골드도 드랍합니다.
    /// </summary>
    public class NutritionThiefBacteriaMonster : FieldSpecialMonsterBase
    {
        [Header("Nutrition Thief Targeting")]
        [Tooltip("경험치/골드를 찾는 탐색 반경입니다.")]
        [SerializeField] private float collectableSearchRadius = 7f;

        [Tooltip("이 거리 안에 들어온 경험치/골드를 흡수합니다.")]
        [SerializeField] private float absorbRadius = 0.65f;

        [Tooltip("흡수 대상 재탐색 주기입니다.")]
        [SerializeField] private float retargetInterval = 0.25f;

        [Header("Movement")]
        [Tooltip("기본 이동속도입니다.")]
        [SerializeField] private float baseMoveSpeed = 1.05f;

        [Tooltip("저장한 보상 1개당 이동속도 증가량입니다.")]
        [SerializeField] private float speedIncreasePerStoredItem = 0.035f;

        [Tooltip("최대 이동속도 배율입니다.")]
        [SerializeField] private float maxSpeedMultiplier = 2.2f;

        [Tooltip("먹을 보상이 없을 때 배회 속도 배율입니다.")]
        [SerializeField] private float idleWanderSpeedMultiplier = 0.35f;

        [Tooltip("먹을 보상이 없을 때 배회 범위입니다.")]
        [SerializeField] private float idleWanderRadius = 1.5f;

        [Header("Growth")]
        [Tooltip("기본 스케일입니다.")]
        [SerializeField] private Vector3 baseScale = Vector3.one;

        [Tooltip("저장한 보상 1개당 스케일 증가량입니다.")]
        [SerializeField] private float scaleIncreasePerStoredItem = 0.035f;

        [Tooltip("최대 스케일 배율입니다.")]
        [SerializeField] private float maxScaleMultiplier = 2.0f;

        [Header("Stored Reward")]
        [Tooltip("이 저장량 이상 먹은 상태에서 처치하면 보너스 골드를 추가로 드랍합니다.")]
        [SerializeField] private int bonusRewardThreshold = 12;

        [Tooltip("보너스 조건을 만족했을 때 추가로 드랍할 골드량입니다.")]
        [SerializeField] private int bonusCoinValue = 15;

        [Tooltip("최소 보장 경험치입니다. 아무것도 못 먹은 상태로 죽어도 이만큼 드랍합니다.")]
        [SerializeField] private int minimumExpDrop = 2;

        [Tooltip("최소 보장 골드입니다. 아무것도 못 먹은 상태로 죽어도 이만큼 드랍합니다.")]
        [SerializeField] private int minimumCoinDrop = 1;

        private static FieldInfo expGemTypeField;

        private ExpGem targetGem;
        private Coin targetCoin;
        private float nextRetargetTime;
        private Vector2 idleAnchorPosition;

        private int storedExpValue;
        private int storedCoinValue;
        private int storedItemCount;

        protected override void Awake()
        {
            base.Awake();
            CacheExpGemField();
        }

        protected override void ResetRuntimeState()
        {
            base.ResetRuntimeState();

            targetGem = null;
            targetCoin = null;
            nextRetargetTime = 0f;
            idleAnchorPosition = transform.position;

            storedExpValue = 0;
            storedCoinValue = 0;
            storedItemCount = 0;

            transform.localScale = baseScale;
        }

        protected override void OnAliveUpdate()
        {
            if (Time.time >= nextRetargetTime)
            {
                FindNearestCollectableTarget();
                nextRetargetTime = Time.time + Mathf.Max(0.05f, retargetInterval);
            }

            TryAbsorbTarget();
            ApplyGrowthVisual();
        }

        protected override void OnAliveFixedUpdate()
        {
            if (rb == null)
            {
                return;
            }

            Vector2 velocity = Vector2.zero;
            Transform targetTransform = GetCurrentTargetTransform();

            if (targetTransform != null)
            {
                Vector2 toTarget = (Vector2)targetTransform.position - rb.position;

                if (toTarget.sqrMagnitude > 0.02f)
                {
                    velocity = toTarget.normalized * GetCurrentMoveSpeed();
                }
            }
            else
            {
                velocity = GetIdleWanderVelocity();
            }

            rb.velocity = velocity;
        }

        protected override void OnKilledByPlayer()
        {
            int finalExp = Mathf.Max(minimumExpDrop, storedExpValue);
            int finalCoin = Mathf.Max(minimumCoinDrop, storedCoinValue);

            if (storedItemCount >= bonusRewardThreshold)
            {
                finalCoin += bonusCoinValue;
            }

            DropExpValueAroundSelf(finalExp);
            DropCoinValueAroundSelf(finalCoin);

            if (debugLog)
            {
                Debug.Log(
                    $"[영양 도둑균] 처치 보상 반환. exp={finalExp}, coin={finalCoin}, storedItem={storedItemCount}",
                    this);
            }
        }

        private void FindNearestCollectableTarget()
        {
            targetGem = null;
            targetCoin = null;

            float nearestSqrDistance = collectableSearchRadius * collectableSearchRadius;
            Vector2 myPosition = transform.position;

            ExpGem[] gems = FindObjectsOfType<ExpGem>();

            for (int i = 0; i < gems.Length; i++)
            {
                ExpGem gem = gems[i];

                if (!IsValidGem(gem))
                {
                    continue;
                }

                float sqrDistance = ((Vector2)gem.transform.position - myPosition).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    targetGem = gem;
                    targetCoin = null;
                }
            }

            Coin[] coins = FindObjectsOfType<Coin>();

            for (int i = 0; i < coins.Length; i++)
            {
                Coin coin = coins[i];

                if (!IsValidCoin(coin))
                {
                    continue;
                }

                float sqrDistance = ((Vector2)coin.transform.position - myPosition).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    targetGem = null;
                    targetCoin = coin;
                }
            }
        }

        private void TryAbsorbTarget()
        {
            if (targetGem != null && IsValidGem(targetGem))
            {
                float distance = Vector2.Distance(transform.position, targetGem.transform.position);

                if (distance <= absorbRadius)
                {
                    AbsorbGem(targetGem);
                    targetGem = null;
                    return;
                }
            }

            if (targetCoin != null && IsValidCoin(targetCoin))
            {
                float distance = Vector2.Distance(transform.position, targetCoin.transform.position);

                if (distance <= absorbRadius)
                {
                    AbsorbCoin(targetCoin);
                    targetCoin = null;
                }
            }
        }

        private void AbsorbGem(ExpGem gem)
        {
            if (gem == null)
            {
                return;
            }

            GemType gemType = GetGemType(gem);
            int expValue = (int)gemType;

            storedExpValue += Mathf.Max(1, expValue);
            storedItemCount++;

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager != null)
            {
                entityManager.DespawnGem(gem);
            }
            else
            {
                gem.gameObject.SetActive(false);
            }

            if (debugLog)
            {
                Debug.Log($"[영양 도둑균] 경험치 흡수. +{expValue}, totalExp={storedExpValue}", this);
            }
        }

        private void AbsorbCoin(Coin coin)
        {
            if (coin == null)
            {
                return;
            }

            int coinValue = Mathf.Max(1, (int)coin.CoinType);

            storedCoinValue += coinValue;
            storedItemCount++;

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager != null)
            {
                entityManager.DespawnCoin(coin, pickedUpByPlayer: false);
            }
            else
            {
                coin.gameObject.SetActive(false);
            }

            if (debugLog)
            {
                Debug.Log($"[영양 도둑균] 골드 흡수. +{coinValue}, totalCoin={storedCoinValue}", this);
            }
        }

        private Transform GetCurrentTargetTransform()
        {
            if (targetGem != null && IsValidGem(targetGem))
            {
                return targetGem.transform;
            }

            if (targetCoin != null && IsValidCoin(targetCoin))
            {
                return targetCoin.transform;
            }

            return null;
        }

        private bool IsValidGem(ExpGem gem)
        {
            return gem != null && gem.gameObject.activeInHierarchy;
        }

        private bool IsValidCoin(Coin coin)
        {
            return coin != null && coin.gameObject.activeInHierarchy;
        }

        private float GetCurrentMoveSpeed()
        {
            float multiplier = 1f + storedItemCount * speedIncreasePerStoredItem;
            multiplier = Mathf.Clamp(multiplier, 1f, maxSpeedMultiplier);

            return baseMoveSpeed * multiplier;
        }

        private void ApplyGrowthVisual()
        {
            float multiplier = 1f + storedItemCount * scaleIncreasePerStoredItem;
            multiplier = Mathf.Clamp(multiplier, 1f, maxScaleMultiplier);

            transform.localScale = baseScale * multiplier;
        }

        private Vector2 GetIdleWanderVelocity()
        {
            Vector2 offset = new Vector2(
                Mathf.Sin(Time.time * 0.8f),
                Mathf.Cos(Time.time * 0.6f)
            ) * idleWanderRadius;

            Vector2 targetPosition = idleAnchorPosition + offset;
            Vector2 toIdleTarget = targetPosition - (Vector2)transform.position;

            if (toIdleTarget.sqrMagnitude < 0.02f)
            {
                return Vector2.zero;
            }

            return toIdleTarget.normalized * baseMoveSpeed * idleWanderSpeedMultiplier;
        }

        private static void CacheExpGemField()
        {
            if (expGemTypeField != null)
            {
                return;
            }

            expGemTypeField = typeof(ExpGem).GetField(
                "gemType",
                BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private GemType GetGemType(ExpGem gem)
        {
            if (gem == null || expGemTypeField == null)
            {
                return GemType.White1;
            }

            object value = expGemTypeField.GetValue(gem);

            if (value is GemType gemType)
            {
                return gemType;
            }

            return GemType.White1;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, collectableSearchRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, absorbRadius);
        }
    }
}
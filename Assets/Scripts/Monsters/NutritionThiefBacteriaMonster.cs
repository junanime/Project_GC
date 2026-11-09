using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 영양 도둑균.
    ///
    /// 기존 Monster 규격을 그대로 사용합니다.
    /// - MonsterBlueprint로 풀 생성
    /// - LevelBlueprint.monsters에 등록
    /// - TimedSpecialMonsterSpawner에서 MonsterBlueprint로 스폰
    ///
    /// 기능:
    /// - 필드의 경험치/골드를 찾아 흡수
    /// - 흡수할수록 크기/이동속도 증가
    /// - 처치 시 저장한 경험치/골드 반환
    /// </summary>
    [DefaultExecutionOrder(730)]
    public class NutritionThiefBacteriaMonster : Monster
    {
        [Header("Nutrition Thief Targeting")]
        [Tooltip("경험치/골드를 찾는 탐색 반경입니다.")]
        [SerializeField] private float collectableSearchRadius = 7f;

        [Tooltip("이 거리 안에 들어온 경험치/골드를 흡수합니다.")]
        [SerializeField] private float absorbRadius = 0.65f;

        [Tooltip("흡수 대상 재탐색 주기입니다.")]
        [SerializeField] private float retargetInterval = 0.25f;

        [Header("Movement")]
        [Tooltip("영양 도둑균 기본 이동속도입니다.")]
        [SerializeField] private float thiefMoveSpeed = 1.05f;

        [Tooltip("저장한 보상 1개당 이동속도 증가량입니다.")]
        [SerializeField] private float speedIncreasePerStoredItem = 0.035f;

        [Tooltip("최대 이동속도 배율입니다.")]
        [SerializeField] private float maxSpeedMultiplier = 2.2f;

        [Tooltip("먹을 보상이 없을 때 배회 속도 배율입니다.")]
        [SerializeField] private float idleWanderSpeedMultiplier = 0.35f;

        [Tooltip("먹을 보상이 없을 때 배회 범위입니다.")]
        [SerializeField] private float idleWanderRadius = 1.5f;

        [Header("Growth")]
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

        [Header("Debug")]
        [Tooltip("영양 도둑균 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = false;

        [Header("Popcorn Pomeranian Animation")]
        [SerializeField] private Sprite[] backRunSprites;
        [SerializeField] private Sprite[] eatSprites;
        [SerializeField, Min(0.01f)] private float runFrameTime = 0.09f;
        [SerializeField, Min(0.01f)] private float eatFrameTime = 0.08f;
        public bool IsMole=>ColonMonsterRoster.InColon&&!IsMiniStageOwned;
        public bool Underground=>IsMole&&burrowRemaining>0;
        float burrowRemaining,burrowTotal=2.2f,moleClock;Vector2 burrowFrom,burrowTo;
        public float EatingRemaining=>eatingRemaining;
        private float eatingRemaining;
        private float motionElapsed;
        private int motionState = -1;

        private static FieldInfo expGemTypeField;

        private ExpGem targetGem;
        private Coin targetCoin;
        private float nextRetargetTime;
        private Vector2 idleAnchorPosition;

        private int storedExpValue;
        private int storedCoinValue;
        private int storedItemCount;

        private Vector3 runtimeBaseScale;
        private bool deathHandled;

        protected override void Awake()
        {
            base.Awake();
            CacheExpGemField();
        }

        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint monsterBlueprint,
            float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, monsterBlueprint, hpBuff);
            ResetNutritionRuntime();
        }

        protected override void Update()
        {
            base.Update();

            // MiniStage 진행 중 기존 필드 몬스터는 완전히 정지합니다.
            // 부모 Monster.Update()가 return하더라도
            // 자식 Update()는 계속 실행되므로 여기서 한 번 더 차단해야 합니다.
            if (!alive || IsFieldRuntimeSuspended)
            {
                return;
            }

            if(IsMole){moleClock+=Time.deltaTime;if(burrowRemaining>0){TickBurrow();return;}}
            if (eatingRemaining > 0f)
            {
                eatingRemaining = Mathf.Max(0f, eatingRemaining - Time.deltaTime);
                if(IsMole&&eatingRemaining<=0)StartBurrow();
                return;
            }

            if (Time.time >= nextRetargetTime)
            {
                FindNearestCollectableTarget();
                nextRetargetTime = Time.time + Mathf.Max(0.05f, retargetInterval);
            }

            TryAbsorbTarget();
            ApplyGrowthVisual();
        }

        protected override void FixedUpdate()
        {
            if (!alive || rb == null || IsFieldRuntimeSuspended)
            {
                return;
            }

            if (eatingRemaining > 0f || Underground)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            Vector2 velocity = Vector2.zero;
            Transform targetTransform = GetCurrentTargetTransform();

            if (targetTransform != null)
            {
                Vector2 toTarget = (Vector2)targetTransform.position - rb.position;

                if (toTarget.sqrMagnitude > 0.02f)
                {
                    velocity = ColonNavigation.Toward(rb.position, targetTransform.position) * GetCurrentMoveSpeed();
                }
            }
            else
            {
                velocity = GetIdleWanderVelocity();
            }

            rb.velocity = velocity * IceMoveMultiplier;
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
                DropStoredReward();
            }

            // Preserve kill attribution and quest/score events. DropLoot/RewardSilver
            // are overridden below because stored pickups have already been returned.
            yield return base.Killed(killedByPlayer);
        }

        protected override void DropLoot() { } // Stored pickups are returned once in Killed.
        protected override void RewardSilver() { }

        private void ResetNutritionRuntime()
        {
            burrowRemaining=0;moleClock=0;if(monsterHitbox!=null)monsterHitbox.enabled=true;
            targetGem = null;
            targetCoin = null;
            nextRetargetTime = 0f;
            idleAnchorPosition = transform.position;

            storedExpValue = 0;
            storedCoinValue = 0;
            storedItemCount = 0;

            eatingRemaining = 0f;
            motionElapsed = 0f;
            motionState = -1;
            if (HasDirectionalAnimation && monsterSpriteAnimator != null)
                monsterSpriteAnimator.StopAnimating();
            deathHandled = false;
            runtimeBaseScale = transform.localScale;
        }

        private bool HasDirectionalAnimation => backRunSprites != null && backRunSprites.Length > 0;

        private void BeginEatingAnimation()
        {
            if(IsMole){eatingRemaining=2;motionElapsed=0;if(rb!=null)rb.velocity=Vector2.zero;return;}
            if (eatSprites == null || eatSprites.Length == 0) return;
            eatingRemaining = eatSprites.Length * Mathf.Max(0.01f, eatFrameTime);
            motionElapsed = 0f;
            motionState = 2;
            if (rb != null) rb.velocity = Vector2.zero;
        }

        private void LateUpdate()
        {
            if(IsMole){MoleVisual();return;}
            if (!alive || deathHandled || IsFieldRuntimeSuspended || !HasDirectionalAnimation ||
                monsterSpriteRenderer == null) return;

            if (monsterSpriteAnimator != null) monsterSpriteAnimator.StopAnimating();
            // The requested sign convention: Y >= 0 faces front; Y < 0 faces back.
            int state = eatingRemaining > 0f ? 2 : (rb != null && rb.velocity.y < 0f ? 1 : 0);
            if (motionState != state)
            {
                motionState = state;
                motionElapsed = 0f;
            }
            Sprite[] frames = state == 2 ? eatSprites :
                state == 1 ? backRunSprites : monsterBlueprint.walkSpriteSequence;
            if (frames == null || frames.Length == 0) return;
            float frameTime = Mathf.Max(0.01f, state == 2 ? eatFrameTime : runFrameTime);
            int index = state == 2
                ? Mathf.Min(Mathf.FloorToInt(motionElapsed / frameTime), frames.Length - 1)
                : Mathf.FloorToInt(motionElapsed / frameTime) % frames.Length;
            monsterSpriteRenderer.flipX = false;
            monsterSpriteRenderer.sprite = frames[index];
            motionElapsed += Time.deltaTime;
        }

        protected override void OnFieldRuntimeSuspended()
        {
            if (monsterSpriteAnimator != null) monsterSpriteAnimator.StopAnimating();
        }

        protected override void OnFieldRuntimeResumed()
        {
            if (alive && !HasDirectionalAnimation && monsterSpriteAnimator != null)
                monsterSpriteAnimator.StartAnimating();
        }

        private void DropStoredReward()
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
                    $"[영양 도둑균] 처치 보상 반환 | Exp={finalExp}, Coin={finalCoin}, StoredItem={storedItemCount}",
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

                if (distance <= absorbRadius && (!IsMole||ColonAttackWall.Clear(transform.position,GetCurrentTargetTransform().position)))
                {
                    AbsorbGem(targetGem);
                    targetGem = null;
                    return;
                }
            }

            if (targetCoin != null && IsValidCoin(targetCoin))
            {
                float distance = Vector2.Distance(transform.position, targetCoin.transform.position);

                if (distance <= absorbRadius && (!IsMole||ColonAttackWall.Clear(transform.position,GetCurrentTargetTransform().position)))
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
            int expValue = Mathf.Max(1, (int)gemType);

            storedExpValue += expValue;
            storedItemCount++;
            BeginEatingAnimation();

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager != null)
            {
                RemoveFromMagneticCollectablesIfNeeded(gem);
                entityManager.DespawnGem(gem);
            }
            else
            {
                gem.gameObject.SetActive(false);
            }
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.NutritionThiefSteal
);
            if (debugLog)
            {
                Debug.Log($"[영양 도둑균] 경험치 흡수 | +{expValue}, TotalExp={storedExpValue}", this);
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
            BeginEatingAnimation();

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager != null)
            {
                RemoveFromMagneticCollectablesIfNeeded(coin);
                entityManager.DespawnCoin(coin, pickedUpByPlayer: false);
            }
            else
            {
                coin.gameObject.SetActive(false);
            }
            GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.NutritionThiefSteal
);
            if (debugLog)
            {
                Debug.Log($"[영양 도둑균] 골드 흡수 | +{coinValue}, TotalCoin={storedCoinValue}", this);
            }
        }

        private void RemoveFromMagneticCollectablesIfNeeded(Collectable collectable)
        {
            if (collectable == null)
            {
                return;
            }

            if (entityManager == null || entityManager.MagneticCollectables == null)
            {
                return;
            }

            if (entityManager.MagneticCollectables.Contains(collectable))
            {
                entityManager.MagneticCollectables.Remove(collectable);
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
            if(IsMole)return moveSpeed;
            float multiplier = 1f + storedItemCount * speedIncreasePerStoredItem;
            multiplier = Mathf.Clamp(multiplier, 1f, maxSpeedMultiplier);

            return thiefMoveSpeed * multiplier;
        }

        private void ApplyGrowthVisual()
        {
            if(IsMole)return;
            float multiplier = 1f + storedItemCount * scaleIncreasePerStoredItem;
            multiplier = Mathf.Clamp(multiplier, 1f, maxScaleMultiplier);

            transform.localScale = runtimeBaseScale * multiplier;
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

            return toIdleTarget.normalized * thiefMoveSpeed * idleWanderSpeedMultiplier;
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
            return FiniteStomachMap.SafeSpawn((Vector2)transform.position + offset,.2f,IsMiniStageOwned);
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


        void StartBurrow(){burrowRemaining=burrowTotal;burrowFrom=transform.position;FindNearestCollectableTarget();var target=GetCurrentTargetTransform();burrowTo=FiniteStomachMap.SafeSpawn(target!=null?(Vector2)target.position:burrowFrom+Random.insideUnitCircle*2,.8f);burrowTo=ColonAttackWall.Stop(burrowFrom,burrowTo,.5f);if(monsterHitbox!=null)monsterHitbox.enabled=false;ColonSpecialEffects.Puff(burrowFrom,new Color(.66f,.36f,.36f),.4f);}
        void TickBurrow(){burrowRemaining=Mathf.Max(0,burrowRemaining-Time.deltaTime);float elapsed=burrowTotal-burrowRemaining;float t=Mathf.Clamp01((elapsed-.35f)/1.5f);var at=Vector2.Lerp(burrowFrom,burrowTo,t);transform.position=at;if(rb!=null)rb.position=at;if(burrowRemaining<=0){if(monsterHitbox!=null)monsterHitbox.enabled=true;ColonSpecialEffects.Puff(at,new Color(.66f,.36f,.36f),.4f);}entityManager?.Grid?.UpdateClient(this);}
        void MoleVisual(){if(!enabled||!alive||monsterSpriteRenderer==null||IsFieldRuntimeSuspended)return;var r=monsterSpriteRenderer;float scale=1;float elapsed=burrowTotal-burrowRemaining;if(Underground)scale=elapsed<.35f?1-elapsed/.35f:burrowRemaining<.35f?1-burrowRemaining/.35f:0;r.enabled=scale>.01f;
            var s=r.transform.localScale;s.y*=Mathf.Max(.01f,scale)*(eatingRemaining>0?1+Mathf.Sin(moleClock*16)*.035f:1);r.transform.localScale=s;if(eatingRemaining>0)r.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(moleClock*12)*6);}

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, collectableSearchRadius);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, absorbRadius);
        }
    }
}
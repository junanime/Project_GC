using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 특수 몬스터: 위산 거머리
    ///
    /// 동작 구조:
    /// 1. 스폰 후 일정 시간 동안 플레이어 주변을 곡선 형태로 이동한다.
    /// 2. 이동 중 코드로 생성한 피 흔적을 남긴다.
    /// 3. 이동 시간이 끝나면 제자리에서 위벽에 주둥이를 박고 피를 빠는 상태로 전환한다.
    /// 4. 흡혈 시간이 끝나면 소화효소 난이도 상승과 같은 랜덤 난이도 상승을 1회 발생시킨다.
    /// 5. 이후 다시 이동 → 흡혈 루프를 반복한다.
    ///
    /// 주의:
    /// - 기존 Monster.moveSpeed와 이름이 겹치지 않도록 전용 이동속도는 leechMoveSpeed를 사용한다.
    /// - DigestiveEnzymeDifficultyManager의 외부 난이도 상승 메서드가 아직 없어도 컴파일되도록 Reflection으로 처리한다.
    /// - 플레이어를 직접 공격하지 않도록 기본 접촉 공격 처리는 사용하지 않는다.
    /// </summary>
    public class AcidLeechMonster : Monster
    {
        private enum LeechState
        {
            Moving,
            Feeding,
            Dead
        }

        [Header("Acid Leech - Animation")]
        [Tooltip("위산 거머리가 이동할 때 사용할 워크 프레임입니다. 일반 몬스터처럼 4장 정도를 추천합니다.")]
        [SerializeField] private Sprite[] moveSprites;

        [Tooltip("위산 거머리가 위벽에 주둥이를 박고 피를 빠는 동안 사용할 프레임입니다.")]
        [SerializeField] private Sprite[] feedingSprites;

        [Tooltip("위산 거머리 애니메이션 프레임 전환 시간입니다.")]
        [SerializeField] private float animationFrameTime = 0.15f;

        [Header("Acid Leech - Phase")]
        [Tooltip("이동 상태가 지속되는 시간입니다. 기본값은 요청 기획 기준 15초입니다.")]
        [SerializeField] private float moveDuration = 15f;

        [Tooltip("피를 빠는 상태가 지속되는 시간입니다. 기본값은 요청 기획 기준 30초입니다.")]
        [SerializeField] private float feedingDuration = 30f;

        [Tooltip("피를 다 빨아먹은 뒤 난이도 상승을 적용할지 여부입니다.")]
        [SerializeField] private bool increaseDifficultyAfterFeeding = true;

        [Header("Acid Leech - Movement")]
        [Tooltip("위산 거머리 전용 곡선 이동 속도입니다. 기존 Monster.moveSpeed와 이름이 겹치지 않도록 별도 값으로 사용합니다.")]
        [SerializeField] private float leechMoveSpeed = 0.75f;

        [Tooltip("이동 페이즈 시작 시 목표 지점을 플레이어 기준 이 거리 안에서 다시 고릅니다.")]
        [SerializeField] private float moveTargetRadiusAroundPlayer = 6f;

        [Tooltip("곡선 이동의 휘어지는 정도입니다. 값이 높을수록 크게 휘어 이동합니다.")]
        [SerializeField] private float curveAmplitude = 1.2f;

        [Tooltip("이동 목표 위치를 고를 때 현재 위치와 최소한 이 거리 이상 떨어진 지점을 고릅니다.")]
        [SerializeField] private float minTargetDistance = 2f;

        [Header("Acid Leech - Blood Trail")]
        [Tooltip("이동 중 피 흔적을 남길지 여부입니다.")]
        [SerializeField] private bool leaveBloodTrail = true;

        [Tooltip("피 흔적 생성 간격입니다.")]
        [SerializeField] private float bloodTrailInterval = 0.35f;

        [Tooltip("피 흔적이 사라지기까지 걸리는 시간입니다.")]
        [SerializeField] private float bloodTrailLifetime = 5f;

        [Tooltip("코드로 생성되는 피 흔적 색상입니다. 이미지 없이 SpriteRenderer 색상으로 처리합니다.")]
        [SerializeField] private Color bloodTrailColor = new Color(0.45f, 0.02f, 0.02f, 0.65f);

        [Tooltip("피 흔적의 최소 크기입니다.")]
        [SerializeField] private float bloodTrailMinSize = 0.18f;

        [Tooltip("피 흔적의 최대 크기입니다.")]
        [SerializeField] private float bloodTrailMaxSize = 0.38f;

        [Tooltip("피 흔적의 Sorting Layer 이름입니다. 바닥 위에 보이되 몬스터보다 뒤에 두는 값을 추천합니다.")]
        [SerializeField] private string bloodTrailSortingLayerName = "Default";

        [Tooltip("피 흔적의 Order in Layer입니다. 값이 높을수록 앞에 보입니다.")]
        [SerializeField] private int bloodTrailSortingOrder = -5;

        [Header("Acid Leech - Silver Reward")]
        [Tooltip("처치 시 추가 지급할 실버 최소값입니다.")]
        [SerializeField] private int silverRewardMin = 8;

        [Tooltip("처치 시 추가 지급할 실버 최대값입니다.")]
        [SerializeField] private int silverRewardMax = 15;

        [Header("Acid Leech - Combat")]
        [Tooltip("위산 거머리가 플레이어에게 직접 접촉 피해를 주지 않도록 접촉 공격을 막습니다.")]
        [SerializeField] private bool disableContactAttack = true;

        [Tooltip("넉백을 받을지 여부입니다. 추격해서 잡는 재미를 위해 true를 추천합니다.")]
        [SerializeField] private bool allowKnockback = true;

        [Header("Debug")]
        [Tooltip("위산 거머리 상태 전환, 난이도 상승, 실버 지급 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private LeechState currentState = LeechState.Moving;

        private Coroutine stateRoutine;
        private Coroutine animationRoutine;

        private Vector2 moveStartPosition;
        private Vector2 moveTargetPosition;
        private Vector2 curveNormal;

        private float moveElapsed;
        private float nextBloodTrailTime;

        private bool silverRewardPaid;

        private static Sprite whiteSprite;

        protected override void Awake()
        {
            base.Awake();

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }
        }

        /// <summary>
        /// EntityManager / TimedSpecialMonsterSpawner가 몬스터 풀에서 꺼낼 때 호출한다.
        /// 부모 Monster.Setup으로 기본 HP, 블루프린트, 풀 등록을 처리한 뒤
        /// 위산 거머리 전용 상태머신을 시작한다.
        /// </summary>
        public override void Setup(
            int monsterIndex,
            Vector2 position,
            MonsterBlueprint incomingBlueprint,
            float hpBuff = 0)
        {
            base.Setup(monsterIndex, position, incomingBlueprint, hpBuff);

            currentState = LeechState.Moving;
            silverRewardPaid = false;
            moveElapsed = 0f;
            nextBloodTrailTime = 0f;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
            }

            StopRuntimeCoroutines();
            StartMovingPhase();

            if (debugLog)
            {
                Debug.Log($"[AcidLeechMonster] 스폰 완료 | 위치={transform.position}", this);
            }
        }

        /// <summary>
        /// 부모 Monster.Update는 기본 몬스터 이동/애니메이션 흐름과 엮일 수 있으므로 호출하지 않는다.
        /// 위산 거머리는 자체 상태머신으로만 동작한다.
        /// </summary>
        protected override void Update()
        {
            if (!alive)
            {
                return;
            }

            UpdateSpriteDirection();
        }

        /// <summary>
        /// 부모 Monster.FixedUpdate는 플레이어 추적 이동을 수행할 수 있으므로 호출하지 않는다.
        /// 위산 거머리는 이동 페이즈에서만 자체 곡선 이동을 수행한다.
        /// </summary>
        protected override void FixedUpdate()
        {
            if (!alive || rb == null)
            {
                return;
            }

            if (currentState != LeechState.Moving)
            {
                rb.velocity = Vector2.zero;
                return;
            }

            UpdateCurveMovement();
        }

        private void StartMovingPhase()
        {
            if (!alive)
            {
                return;
            }

            currentState = LeechState.Moving;
            moveElapsed = 0f;
            nextBloodTrailTime = 0f;

            PrepareCurvePath();
            PlayLoopAnimation(moveSprites);

            if (stateRoutine != null)
            {
                StopCoroutine(stateRoutine);
            }

            stateRoutine = StartCoroutine(MovePhaseRoutine());

            if (debugLog)
            {
                Debug.Log("[AcidLeechMonster] 이동 페이즈 시작", this);
            }
        }

        private IEnumerator MovePhaseRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, moveDuration));

            stateRoutine = null;
            StartFeedingPhase();
        }

        private void StartFeedingPhase()
        {
            if (!alive)
            {
                return;
            }

            currentState = LeechState.Feeding;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
            }

            PlayLoopAnimation(feedingSprites);

            if (stateRoutine != null)
            {
                StopCoroutine(stateRoutine);
            }

            stateRoutine = StartCoroutine(FeedingPhaseRoutine());

            if (debugLog)
            {
                Debug.Log("[AcidLeechMonster] 흡혈 페이즈 시작", this);
            }
        }

        private IEnumerator FeedingPhaseRoutine()
        {
            yield return new WaitForSeconds(Mathf.Max(0.1f, feedingDuration));

            stateRoutine = null;

            if (increaseDifficultyAfterFeeding)
            {
                TryApplyDigestiveDifficultyIncrease();

                if (debugLog)
                {
                    Debug.Log("[AcidLeechMonster] 흡혈 완료 - 랜덤 난이도 상승 시도", this);
                }
            }

            StartMovingPhase();
        }

        private void PrepareCurvePath()
        {
            moveStartPosition = transform.position;
            moveTargetPosition = GetNextMoveTargetPosition();

            Vector2 pathDirection = moveTargetPosition - moveStartPosition;

            if (pathDirection.sqrMagnitude <= 0.001f)
            {
                pathDirection = Random.insideUnitCircle.normalized;
            }

            if (pathDirection.sqrMagnitude <= 0.001f)
            {
                pathDirection = Vector2.right;
            }

            pathDirection.Normalize();

            curveNormal = new Vector2(-pathDirection.y, pathDirection.x);

            if (Random.value < 0.5f)
            {
                curveNormal *= -1f;
            }
        }

        private Vector2 GetNextMoveTargetPosition()
        {
            Vector2 origin = transform.position;

            if (playerCharacter != null)
            {
                origin = playerCharacter.transform.position;
            }

            for (int i = 0; i < 12; i++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;

                if (direction.sqrMagnitude <= 0.001f)
                {
                    direction = Vector2.right;
                }

                float distance = Random.Range(
                    Mathf.Max(0.5f, minTargetDistance),
                    Mathf.Max(minTargetDistance + 0.1f, moveTargetRadiusAroundPlayer));

                Vector2 candidate = origin + direction * distance;

                if (Vector2.Distance(candidate, transform.position) >= minTargetDistance)
                {
                    return candidate;
                }
            }

            Vector2 fallbackDirection = Random.insideUnitCircle.normalized;

            if (fallbackDirection.sqrMagnitude <= 0.001f)
            {
                fallbackDirection = Vector2.right;
            }

            return origin + fallbackDirection * Mathf.Max(1f, minTargetDistance);
        }

        private void UpdateCurveMovement()
        {
            float safeMoveDuration = Mathf.Max(0.1f, moveDuration);
            moveElapsed += Time.fixedDeltaTime;

            float t = Mathf.Clamp01(moveElapsed / safeMoveDuration);

            Vector2 linearPosition = Vector2.Lerp(moveStartPosition, moveTargetPosition, t);
            Vector2 curveOffset = curveNormal * Mathf.Sin(t * Mathf.PI) * curveAmplitude;
            Vector2 targetPosition = linearPosition + curveOffset;

            Vector2 currentPosition = rb.position;
            float maxStep = Mathf.Max(0.05f, leechMoveSpeed) * Time.fixedDeltaTime;
            Vector2 nextPosition = Vector2.MoveTowards(currentPosition, targetPosition, maxStep);

            rb.MovePosition(nextPosition);

            if (leaveBloodTrail && Time.time >= nextBloodTrailTime)
            {
                nextBloodTrailTime = Time.time + Mathf.Max(0.05f, bloodTrailInterval);
                SpawnBloodTrail(nextPosition);
            }
        }

        private void SpawnBloodTrail(Vector2 position)
        {
            Sprite sprite = GetWhiteSprite();

            if (sprite == null)
            {
                return;
            }

            GameObject trailObject = new GameObject("AcidLeech_BloodTrail");
            trailObject.transform.position = position;

            float minSize = Mathf.Max(0.01f, bloodTrailMinSize);
            float maxSize = Mathf.Max(minSize, bloodTrailMaxSize);
            float randomSize = Random.Range(minSize, maxSize);

            trailObject.transform.localScale = new Vector3(
                randomSize * Random.Range(1.0f, 1.8f),
                randomSize * Random.Range(0.6f, 1.2f),
                1f);

            trailObject.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

            SpriteRenderer sr = trailObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = bloodTrailColor;
            sr.sortingLayerName = bloodTrailSortingLayerName;
            sr.sortingOrder = bloodTrailSortingOrder;

            StartCoroutine(FadeAndDestroyBloodTrail(sr, trailObject, bloodTrailLifetime));
        }

        private IEnumerator FadeAndDestroyBloodTrail(
            SpriteRenderer sr,
            GameObject trailObject,
            float lifetime)
        {
            float safeLifetime = Mathf.Max(0.1f, lifetime);
            float elapsed = 0f;
            Color startColor = sr != null ? sr.color : bloodTrailColor;

            while (elapsed < safeLifetime)
            {
                elapsed += Time.deltaTime;

                if (sr != null)
                {
                    float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / safeLifetime);
                    sr.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                }

                yield return null;
            }

            if (trailObject != null)
            {
                Destroy(trailObject);
            }
        }

        private void PlayLoopAnimation(Sprite[] frames)
        {
            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            if (frames == null || frames.Length == 0)
            {
                return;
            }

            animationRoutine = StartCoroutine(LoopAnimationRoutine(frames));
        }

        private IEnumerator LoopAnimationRoutine(Sprite[] frames)
        {
            if (monsterSpriteRenderer == null)
            {
                yield break;
            }

            int frameIndex = 0;
            WaitForSeconds wait = new WaitForSeconds(Mathf.Max(0.01f, animationFrameTime));

            while (alive)
            {
                if (frames != null && frames.Length > 0)
                {
                    monsterSpriteRenderer.sprite = frames[frameIndex];
                    frameIndex = (frameIndex + 1) % frames.Length;
                }

                yield return wait;
            }
        }

        private void UpdateSpriteDirection()
        {
            if (monsterSpriteRenderer == null || rb == null)
            {
                return;
            }

            if (Mathf.Abs(rb.velocity.x) > 0.05f)
            {
                monsterSpriteRenderer.flipX = rb.velocity.x < 0f;
            }
        }

        public override void Knockback(Vector2 knockback)
        {
            if (!allowKnockback || rb == null)
            {
                return;
            }

            rb.velocity += knockback;
        }

        /// <summary>
        /// 위산 거머리는 플레이어에게 직접 접촉 피해를 주지 않는다.
        /// 기존 Monster의 접촉 공격 흐름이 있더라도 이 클래스에서는 비워둔다.
        /// </summary>
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (disableContactAttack)
            {
                return;
            }
        }

        /// <summary>
        /// 트리거 접촉으로도 플레이어에게 직접 피해를 주지 않는다.
        /// </summary>
        private void OnTriggerStay2D(Collider2D other)
        {
            if (disableContactAttack)
            {
                return;
            }
        }

        public override IEnumerator Killed(bool killedByPlayer = true)
        {
            if (currentState == LeechState.Dead)
            {
                yield break;
            }

            currentState = LeechState.Dead;
            StopRuntimeCoroutines();

            if (killedByPlayer && !silverRewardPaid)
            {
                silverRewardPaid = true;

                int min = Mathf.Max(0, silverRewardMin);
                int max = Mathf.Max(min, silverRewardMax);
                int reward = Random.Range(min, max + 1);

                if (reward > 0)
                {
                    SilverWallet.Add(reward);

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[AcidLeechMonster] 처치 보상 실버 +{reward} | Total={SilverWallet.Silver}",
                            this);
                    }
                }
            }

            yield return base.Killed(killedByPlayer);
        }

        private void StopRuntimeCoroutines()
        {
            if (stateRoutine != null)
            {
                StopCoroutine(stateRoutine);
                stateRoutine = null;
            }

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }
        }

        /// <summary>
        /// DigestiveEnzymeDifficultyManager에 외부 난이도 상승 메서드가 있으면 그 메서드를 호출한다.
        /// 아직 메서드가 없으면 기존 private ApplyRandomDifficultyIncrease()를 Reflection으로 호출한다.
        ///
        /// 이렇게 처리하면 DigestiveEnzymeDifficultyManager 수정이 아직 완전히 반영되지 않아도
        /// AcidLeechMonster.cs 자체는 컴파일된다.
        /// </summary>
        private void TryApplyDigestiveDifficultyIncrease()
        {
            DigestiveEnzymeDifficultyManager manager = DigestiveEnzymeDifficultyManager.Instance;

            if (manager == null)
            {
                Debug.LogWarning(
                    "[AcidLeechMonster] DigestiveEnzymeDifficultyManager가 씬에 없어 난이도 상승을 적용하지 못했습니다.",
                    this);
                return;
            }

            System.Type managerType = typeof(DigestiveEnzymeDifficultyManager);

            MethodInfo externalNotifyMethod = managerType.GetMethod(
                "NotifyDifficultyIncreaseFromExternalSource",
                BindingFlags.Public | BindingFlags.Static);

            if (externalNotifyMethod != null)
            {
                externalNotifyMethod.Invoke(null, new object[] { "Acid Leech Feeding" });
                return;
            }

            MethodInfo applyRandomDifficultyMethod = managerType.GetMethod(
                "ApplyRandomDifficultyIncrease",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (applyRandomDifficultyMethod == null)
            {
                Debug.LogWarning(
                    "[AcidLeechMonster] DigestiveEnzymeDifficultyManager에서 ApplyRandomDifficultyIncrease 메서드를 찾지 못했습니다.",
                    this);
                return;
            }

            applyRandomDifficultyMethod.Invoke(manager, null);
        }

        private static Sprite GetWhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            whiteSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f),
                1f);

            return whiteSprite;
        }

        private void OnDisable()
        {
            StopRuntimeCoroutines();
        }
    }
}
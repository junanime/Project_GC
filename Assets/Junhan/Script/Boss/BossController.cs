using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BossController : MonoBehaviour
    {
        [Header("Boss References")]
        [SerializeField] private Character playerCharacter;
        [SerializeField] private Rigidbody2D rb;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private List<BossPatternBase> patterns = new List<BossPatternBase>();
        [SerializeField] private bool autoCollectPatternsFromChildren = true;

        [Header("Boss Stats")]
        [SerializeField] private float maxHp = 500f;
        [SerializeField] private float currentHp = 500f;
        [SerializeField] private float contactDamage = 10f;
        [SerializeField] private float contactDamageCooldown = 0.6f;

        [Header("Basic Attack")]
        [SerializeField] private bool enableBasicAttack = true;
        [SerializeField] private GameObject basicAttackBulletPrefab;

        [Tooltip("기본 공격 발사 간격입니다. 너무 낮으면 피하기 어렵습니다.")]
        [SerializeField] private float basicAttackCooldown = 1.6f;

        [SerializeField] private float basicAttackDamage = 5f;

        [Tooltip("기본 탄환 속도입니다. 너무 빠르면 무조건 맞는 느낌이 납니다.")]
        [SerializeField] private float basicAttackBulletSpeed = 4.5f;

        [Tooltip("보스 중심에서 얼마나 앞쪽으로 탄환을 생성할지 결정합니다.")]
        [SerializeField] private float basicAttackMuzzleOffset = 1.2f;

        [Tooltip("패턴 사용 중에도 기본 공격을 쏠지 여부입니다.")]
        [SerializeField] private bool basicAttackWhileUsingPattern = false;

        [Header("Basic Attack Aim")]
        [Tooltip("체크하면 플레이어 현재 위치가 아니라 이동 방향 앞쪽을 조준합니다.")]
        [SerializeField] private bool usePredictiveBasicAim = true;

        [Tooltip("플레이어가 이 시간 뒤에 있을 것으로 예상되는 위치를 조준합니다.")]
        [SerializeField] private float basicAimLeadTime = 0.45f;

        [Tooltip("조준에 약간의 오차를 줍니다. 값이 클수록 피하기 쉬워집니다.")]
        [SerializeField] private float basicAimInaccuracyAngle = 5f;

        [Tooltip("플레이어 이동속도가 이 값보다 낮으면 예측 조준을 약하게 적용합니다.")]
        [SerializeField] private float minPlayerVelocityForPrediction = 0.1f;

        [Header("Basic Attack Visual Sorting")]
        [SerializeField] private bool forceBasicBulletSortingOrder = true;

        [Tooltip("보스 본체 Order보다 낮게 두세요. 예: 보스 700, 탄환 500")]
        [SerializeField] private int basicBulletSortingOrder = 500;

        [Header("Movement")]
        [SerializeField] private bool enableMovement = true;
        [SerializeField] private float moveSpeedPhase1 = 0.8f;
        [SerializeField] private float moveSpeedPhase2 = 1.2f;
        [SerializeField] private float stopDistanceFromPlayer = 3f;
        [SerializeField] private bool moveWhileUsingPattern = true;
        [SerializeField] private float patternMoveSpeedMultiplier = 0.35f;
        [SerializeField] private float movementSmoothTime = 0.18f;
        [SerializeField] private bool flipSpriteToPlayer = true;

        [Header("Special Pattern Timing")]
        [SerializeField] private float firstPatternDelay = 3f;
        [SerializeField] private float patternIntervalMin = 4f;
        [SerializeField] private float patternIntervalMax = 7f;
        [SerializeField] private float thinkInterval = 0.3f;
        [SerializeField] private float patternGap = 1.2f;

        [Header("Distance Thresholds")]
        [SerializeField] private float nearDistanceThreshold = 4f;
        [SerializeField] private float midDistanceThreshold = 8f;

        [Header("Phase Settings")]
        [SerializeField] private float phase2HpRatio = 0.5f;
        [SerializeField] private bool phase2Active = false;

        [Header("Reward")]
        [SerializeField] private bool rewardOnDeath = true;

        [Header("Debug")]
        [SerializeField] private bool debugMovement = false;
        [SerializeField] private bool debugPattern = false;
        [SerializeField] private bool debugBasicAttack = false;

        private bool isDead = false;
        private bool isUsingPattern = false;
        private float lastContactDamageTime = -999f;
        private Vector2 smoothMoveVelocity;

        private bool externalMovementLock = false;
        private bool suppressContactDamage = false;

        private float basicAttackTimer = 0f;
        private float nextPatternTime = 0f;

        private Vector2 lastPlayerPosition;
        private Vector2 estimatedPlayerVelocity;

        public float NearDistanceThreshold => nearDistanceThreshold;
        public float MidDistanceThreshold => midDistanceThreshold;
        public int CurrentPhase => phase2Active ? 2 : 1;
        public Character PlayerCharacter => playerCharacter;
        public Vector3 BossCenterPosition => transform.position;
        public Rigidbody2D Rigidbody => rb;
        public bool IsDead => isDead;

        public void SetPlayerCharacter(Character character)
        {
            playerCharacter = character;

            if (playerCharacter != null)
            {
                lastPlayerPosition = playerCharacter.transform.position;
            }
        }

        public void SetExternalMovementLock(bool value)
        {
            externalMovementLock = value;

            if (value)
            {
                StopMovementVelocity();
            }
        }

        public void SetSuppressContactDamage(bool value)
        {
            suppressContactDamage = value;
        }

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody2D>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.freezeRotation = true;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void Start()
        {
            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }

            if (playerCharacter != null)
            {
                lastPlayerPosition = playerCharacter.transform.position;
            }

            if (autoCollectPatternsFromChildren || patterns.Count == 0)
            {
                patterns.Clear();

                BossPatternBase[] foundPatterns = GetComponentsInChildren<BossPatternBase>(true);
                foreach (BossPatternBase pattern in foundPatterns)
                {
                    if (pattern != null)
                    {
                        patterns.Add(pattern);
                    }
                }
            }

            currentHp = maxHp;

            foreach (BossPatternBase pattern in patterns)
            {
                if (pattern != null)
                {
                    pattern.Init(this);
                }
            }

            basicAttackTimer = basicAttackCooldown;
            ScheduleNextPattern(firstPatternDelay);

            if (debugPattern)
            {
                Debug.Log($"[BossController] Pattern Count = {patterns.Count}");
            }

            StartCoroutine(PatternLoop());
        }

        private void Update()
        {
            UpdatePlayerVelocityEstimate();
            UpdateSpriteFlip();
            UpdateBasicAttack();
        }

        private void FixedUpdate()
        {
            UpdateMovement();
        }

        private void UpdatePlayerVelocityEstimate()
        {
            if (playerCharacter == null)
            {
                return;
            }

            Vector2 currentPlayerPosition = playerCharacter.transform.position;

            if (Time.deltaTime > 0f)
            {
                estimatedPlayerVelocity = (currentPlayerPosition - lastPlayerPosition) / Time.deltaTime;
            }

            lastPlayerPosition = currentPlayerPosition;
        }

        private void UpdateBasicAttack()
        {
            if (!enableBasicAttack || isDead || playerCharacter == null)
            {
                return;
            }

            if (isUsingPattern && !basicAttackWhileUsingPattern)
            {
                return;
            }

            if (basicAttackBulletPrefab == null)
            {
                return;
            }

            basicAttackTimer += Time.deltaTime;

            if (basicAttackTimer >= basicAttackCooldown)
            {
                basicAttackTimer = 0f;
                FireBasicAttack();
            }
        }

        private void FireBasicAttack()
        {
            Vector2 origin = BossCenterPosition;
            Vector2 aimPosition = GetBasicAttackAimPosition();
            Vector2 direction = (aimPosition - origin).normalized;

            if (direction == Vector2.zero)
            {
                direction = Vector2.right;
            }

            if (basicAimInaccuracyAngle > 0f)
            {
                float randomAngle = Random.Range(-basicAimInaccuracyAngle, basicAimInaccuracyAngle);
                direction = RotateVector(direction, randomAngle);
            }

            Vector3 spawnPosition = (Vector3)origin + (Vector3)(direction * basicAttackMuzzleOffset);
            GameObject bullet = Instantiate(basicAttackBulletPrefab, spawnPosition, Quaternion.identity);

            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            bullet.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            ApplyBulletSortingOrder(bullet, basicBulletSortingOrder);

            BossSimpleBullet simpleBullet = bullet.GetComponent<BossSimpleBullet>();

            if (simpleBullet == null)
            {
                simpleBullet = bullet.GetComponentInChildren<BossSimpleBullet>();
            }

            if (simpleBullet == null)
            {
                simpleBullet = bullet.AddComponent<BossSimpleBullet>();
                Debug.LogWarning("[BossController] Basic Attack Bullet에 BossSimpleBullet이 없어 자동 추가했습니다.");
            }

            simpleBullet.Init(direction, basicAttackBulletSpeed, basicAttackDamage);

            if (debugBasicAttack)
            {
                Debug.Log($"[BossController] Basic Attack Fired | aimPosition={aimPosition} | velocity={estimatedPlayerVelocity}");
            }
        }

        private Vector2 GetBasicAttackAimPosition()
        {
            Vector2 currentPlayerPosition = playerCharacter.transform.position;

            if (!usePredictiveBasicAim)
            {
                return currentPlayerPosition;
            }

            if (estimatedPlayerVelocity.magnitude < minPlayerVelocityForPrediction)
            {
                return currentPlayerPosition;
            }

            return currentPlayerPosition + estimatedPlayerVelocity * basicAimLeadTime;
        }

        private void ApplyBulletSortingOrder(GameObject bullet, int sortingOrder)
        {
            if (!forceBasicBulletSortingOrder || bullet == null)
            {
                return;
            }

            SpriteRenderer[] renderers = bullet.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in renderers)
            {
                renderer.sortingOrder = sortingOrder;
            }
        }

        private void UpdateMovement()
        {
            if (!CanMove())
            {
                StopMovementVelocity();
                return;
            }

            Vector2 bossPosition = rb != null ? rb.position : (Vector2)transform.position;
            Vector2 playerPosition = playerCharacter.transform.position;

            Vector2 toPlayer = playerPosition - bossPosition;
            float distance = toPlayer.magnitude;

            if (distance <= stopDistanceFromPlayer)
            {
                StopMovementVelocity();
                return;
            }

            Vector2 direction = toPlayer.normalized;
            Vector2 targetPosition = playerPosition - direction * stopDistanceFromPlayer;

            float baseSpeed = CurrentPhase == 2 ? moveSpeedPhase2 : moveSpeedPhase1;
            float finalSpeed = baseSpeed;

            if (isUsingPattern)
            {
                finalSpeed *= patternMoveSpeedMultiplier;
            }

            Vector2 nextPosition = Vector2.SmoothDamp(
                bossPosition,
                targetPosition,
                ref smoothMoveVelocity,
                movementSmoothTime,
                finalSpeed,
                Time.fixedDeltaTime
            );

            float maxStep = finalSpeed * Time.fixedDeltaTime;

            if (Vector2.Distance(bossPosition, nextPosition) > maxStep * 1.5f)
            {
                nextPosition = Vector2.MoveTowards(bossPosition, nextPosition, maxStep);
            }

            if (rb != null)
            {
                rb.MovePosition(nextPosition);
            }
            else
            {
                transform.position = nextPosition;
            }

            if (debugMovement)
            {
                Debug.Log($"[BossController] Moving | distance={distance:F2} | speed={finalSpeed:F2}");
            }
        }

        private bool CanMove()
        {
            if (!enableMovement)
            {
                return false;
            }

            if (isDead)
            {
                return false;
            }

            if (playerCharacter == null)
            {
                return false;
            }

            if (externalMovementLock)
            {
                return false;
            }

            if (isUsingPattern && !moveWhileUsingPattern)
            {
                return false;
            }

            return true;
        }

        private void StopMovementVelocity()
        {
            smoothMoveVelocity = Vector2.zero;

            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }

        private void UpdateSpriteFlip()
        {
            if (!flipSpriteToPlayer || spriteRenderer == null || playerCharacter == null)
            {
                return;
            }

            float directionX = playerCharacter.transform.position.x - transform.position.x;
            spriteRenderer.flipX = directionX < 0f;
        }

        private IEnumerator PatternLoop()
        {
            while (!isDead)
            {
                if (!isUsingPattern && playerCharacter != null && Time.time >= nextPatternTime)
                {
                    BossPatternBase selectedPattern = SelectPatternByDistance();

                    if (selectedPattern != null)
                    {
                        yield return StartCoroutine(UsePattern(selectedPattern));
                    }

                    ScheduleNextPattern();
                }

                yield return new WaitForSeconds(thinkInterval);
            }
        }

        private IEnumerator UsePattern(BossPatternBase pattern)
        {
            isUsingPattern = true;

            if (debugPattern)
            {
                Debug.Log($"[BossController] Use Pattern: {pattern.PatternName}");
            }

            yield return StartCoroutine(pattern.Execute());
            yield return new WaitForSeconds(patternGap);

            isUsingPattern = false;
        }

        private void ScheduleNextPattern(float overrideDelay = -1f)
        {
            float delay = overrideDelay >= 0f
                ? overrideDelay
                : Random.Range(patternIntervalMin, patternIntervalMax);

            nextPatternTime = Time.time + delay;

            if (debugPattern)
            {
                Debug.Log($"[BossController] Next Pattern In {delay:F1}s");
            }
        }

        private BossPatternBase SelectPatternByDistance()
        {
            float distance = GetDistanceToPlayer();

            List<BossPatternBase> validPatterns = new List<BossPatternBase>();
            List<int> weights = new List<int>();
            int totalWeight = 0;

            foreach (BossPatternBase pattern in patterns)
            {
                if (pattern == null || !pattern.CanUse())
                {
                    continue;
                }

                int weight = pattern.GetWeight(distance, CurrentPhase);

                if (weight <= 0)
                {
                    continue;
                }

                validPatterns.Add(pattern);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (validPatterns.Count == 0 || totalWeight <= 0)
            {
                return null;
            }

            int roll = Random.Range(0, totalWeight);
            int cumulative = 0;

            for (int i = 0; i < validPatterns.Count; i++)
            {
                cumulative += weights[i];

                if (roll < cumulative)
                {
                    return validPatterns[i];
                }
            }

            return validPatterns[0];
        }

        private float GetDistanceToPlayer()
        {
            if (playerCharacter == null)
            {
                return 999f;
            }

            return Vector2.Distance(transform.position, playerCharacter.transform.position);
        }

        public void TakeDamage(float damage)
        {
            if (isDead)
            {
                return;
            }

            currentHp -= damage;

            if (!phase2Active && currentHp <= maxHp * phase2HpRatio)
            {
                EnterPhase2();
            }

            if (currentHp <= 0f)
            {
                Die();
            }
        }

        private void EnterPhase2()
        {
            phase2Active = true;
            Debug.Log("[Boss] Phase 2 Start");
        }

        private void Die()
        {
            if (isDead)
            {
                return;
            }

            isDead = true;

            Debug.Log("[Boss] Boss Died");

            if (rewardOnDeath)
            {
                GiveBossReward();
            }

            Destroy(gameObject);
        }

        private void GiveBossReward()
        {
            Debug.Log("[Boss] Reward Triggered");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryDealContactDamage(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            TryDealContactDamage(other);
        }

        private void TryDealContactDamage(Collider2D other)
        {
            if (isDead || playerCharacter == null)
            {
                return;
            }

            if (suppressContactDamage)
            {
                return;
            }

            Character character = other.GetComponentInParent<Character>();

            if (character == null || character != playerCharacter)
            {
                return;
            }

            if (Time.time < lastContactDamageTime + contactDamageCooldown)
            {
                return;
            }

            lastContactDamageTime = Time.time;
            playerCharacter.TakeDamage(contactDamage);
        }

        private Vector2 RotateVector(Vector2 vector, float angleDegrees)
        {
            float rad = angleDegrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);

            return new Vector2(
                vector.x * cos - vector.y * sin,
                vector.x * sin + vector.y * cos
            ).normalized;
        }
    }
}
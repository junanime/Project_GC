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

        [Header("Movement")]
        [SerializeField] private bool enableMovement = true;
        [SerializeField] private float moveSpeedPhase1 = 0.8f;
        [SerializeField] private float moveSpeedPhase2 = 1.2f;
        [SerializeField] private float stopDistanceFromPlayer = 3f;
        [SerializeField] private bool moveWhileUsingPattern = true;
        [SerializeField] private float patternMoveSpeedMultiplier = 0.35f;
        [SerializeField] private float movementSmoothTime = 0.18f;
        [SerializeField] private bool flipSpriteToPlayer = true;

        [Header("Pattern Timing")]
        [SerializeField] private float thinkInterval = 0.5f;
        [SerializeField] private float patternGap = 1.5f;

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

        private bool isDead = false;
        private bool isUsingPattern = false;
        private float lastContactDamageTime = -999f;
        private Vector2 smoothMoveVelocity;

        public float NearDistanceThreshold => nearDistanceThreshold;
        public float MidDistanceThreshold => midDistanceThreshold;
        public int CurrentPhase => phase2Active ? 2 : 1;
        public Character PlayerCharacter => playerCharacter;
        public Vector3 BossCenterPosition => transform.position;

        public void SetPlayerCharacter(Character character)
        {
            playerCharacter = character;
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

            if (debugPattern)
            {
                Debug.Log($"[BossController] Pattern Count = {patterns.Count}");
            }

            StartCoroutine(PatternLoop());
        }

        private void Update()
        {
            UpdateSpriteFlip();
        }

        private void FixedUpdate()
        {
            UpdateMovement();
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

            // 플레이어에게 완전히 겹치지 않고, 정지 거리만큼 떨어진 지점을 목표로 이동
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

            // SmoothDamp가 튀는 상황을 막기 위한 최대 이동량 제한
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
                if (!isUsingPattern && playerCharacter != null)
                {
                    BossPatternBase selectedPattern = SelectPatternByDistance();

                    if (selectedPattern != null)
                    {
                        yield return StartCoroutine(UsePattern(selectedPattern));
                    }
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
    }
}
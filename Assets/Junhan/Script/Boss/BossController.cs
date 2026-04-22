using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class BossController : MonoBehaviour
    {
        [Header("Boss References")]
        [SerializeField] private Character playerCharacter;
        [SerializeField] private List<BossPatternBase> patterns = new List<BossPatternBase>();

        [Header("Boss Stats")]
        [SerializeField] private float maxHp = 500f;
        [SerializeField] private float currentHp = 500f;
        [SerializeField] private float contactDamage = 10f;

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

        private bool isDead = false;
        private bool isUsingPattern = false;

        public float NearDistanceThreshold => nearDistanceThreshold;
        public float MidDistanceThreshold => midDistanceThreshold;
        public int CurrentPhase => phase2Active ? 2 : 1;
        public Character PlayerCharacter => playerCharacter;

        public void SetPlayerCharacter(Character character)
        {
            playerCharacter = character;
        }

        private void Start()
        {
            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }

            currentHp = maxHp;

            foreach (BossPatternBase pattern in patterns)
            {
                if (pattern != null)
                {
                    pattern.Init(this);
                }
            }

            StartCoroutine(PatternLoop());
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
                    continue;

                int weight = pattern.GetWeight(distance, CurrentPhase);
                if (weight <= 0)
                    continue;

                validPatterns.Add(pattern);
                weights.Add(weight);
                totalWeight += weight;
            }

            if (validPatterns.Count == 0 || totalWeight <= 0)
                return null;

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
                return 999f;

            return Vector2.Distance(transform.position, playerCharacter.transform.position);
        }

        public void TakeDamage(float damage)
        {
            if (isDead)
                return;

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
                return;

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
            // 나중에 기존 증강 선택 UI와 연결
            Debug.Log("[Boss] Reward Triggered");
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (isDead || playerCharacter == null)
                return;

            if (other.TryGetComponent<IDamageable>(out IDamageable damageable))
            {
                if (damageable == playerCharacter)
                {
                    playerCharacter.TakeDamage(contactDamage);
                }
            }
        }
    }
}
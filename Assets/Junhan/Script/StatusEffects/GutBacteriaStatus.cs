using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 장내균침 상태.
    /// 침에 맞을 때마다 장내균 스택을 쌓고,
    /// 일정 중첩 이상인 상태에서 몬스터가 죽으면 추가 경험치 구슬을 생성합니다.
    /// </summary>
    public class GutBacteriaStatus : MonoBehaviour
    {
        private readonly List<float> stackExpireTimes = new List<float>();

        private Monster ownerMonster;
        private EntityManager entityManager;

        private float stackDuration = 6f;
        private int requiredStacks = 3;
        private int maxStacks = 5;
        private int bonusGemCount = 1;
        private GemType bonusGemType = GemType.White1;
        private float bonusGemSpawnRadius = 0.35f;
        private bool debugLog = false;

        private bool initialized = false;
        private bool rewardGiven = false;

        public int CurrentStacks
        {
            get
            {
                RemoveExpiredStacks();
                return stackExpireTimes.Count;
            }
        }

        public void Apply(
            float stackDuration,
            int requiredStacks,
            int maxStacks,
            int bonusGemCount,
            GemType bonusGemType,
            float bonusGemSpawnRadius,
            bool debugLog)
        {
            this.stackDuration = Mathf.Max(0.1f, stackDuration);
            this.requiredStacks = Mathf.Max(1, requiredStacks);
            this.maxStacks = Mathf.Max(this.requiredStacks, maxStacks);
            this.bonusGemCount = Mathf.Max(0, bonusGemCount);
            this.bonusGemType = bonusGemType;
            this.bonusGemSpawnRadius = Mathf.Max(0f, bonusGemSpawnRadius);
            this.debugLog = debugLog;

            if (!initialized)
            {
                Initialize();
            }

            RemoveExpiredStacks();

            stackExpireTimes.Add(Time.time + this.stackDuration);
            stackExpireTimes.Sort();

            while (stackExpireTimes.Count > this.maxStacks)
            {
                stackExpireTimes.RemoveAt(0);
            }

            if (this.debugLog)
            {
                Debug.Log(
                    $"[장내균침] 스택 증가 | Target={gameObject.name} | " +
                    $"Stacks={stackExpireTimes.Count}/{this.requiredStacks}"
                );
            }
        }

        private void Initialize()
        {
            ownerMonster = GetComponent<Monster>() ?? GetComponentInParent<Monster>();

            if (ownerMonster == null)
            {
                Destroy(this);
                return;
            }

            entityManager = FindObjectOfType<EntityManager>();

            ownerMonster.OnKilled.AddListener(OnOwnerKilled);
            initialized = true;
        }

        private void Update()
        {
            RemoveExpiredStacks();

            if (stackExpireTimes.Count <= 0)
            {
                Destroy(this);
            }
        }

        private void OnOwnerKilled(Monster killedMonster)
        {
            if (rewardGiven)
            {
                return;
            }

            RemoveExpiredStacks();

            if (stackExpireTimes.Count < requiredStacks)
            {
                return;
            }

            rewardGiven = true;

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (entityManager == null)
            {
                Debug.LogWarning("[장내균침] EntityManager를 찾지 못해 추가 경험치 구슬을 생성하지 못했습니다.");
                return;
            }

            Vector2 center = killedMonster != null
                ? (Vector2)killedMonster.transform.position
                : (Vector2)transform.position;

            for (int i = 0; i < bonusGemCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * bonusGemSpawnRadius;
                entityManager.SpawnExpGem(center + offset, bonusGemType, true);
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[장내균침] 추가 경험치 생성 | Target={gameObject.name} | " +
                    $"Stacks={stackExpireTimes.Count} | Gems={bonusGemCount} | Type={bonusGemType}"
                );
            }
        }

        private void RemoveExpiredStacks()
        {
            float now = Time.time;

            for (int i = stackExpireTimes.Count - 1; i >= 0; i--)
            {
                if (stackExpireTimes[i] <= now)
                {
                    stackExpireTimes.RemoveAt(i);
                }
            }
        }

        private void OnDestroy()
        {
            if (ownerMonster != null)
            {
                ownerMonster.OnKilled.RemoveListener(OnOwnerKilled);
            }
        }
    }
}
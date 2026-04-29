using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class StageEventDirector : MonoBehaviour
    {
        [System.Serializable]
        public class MonsterSurgeEvent
        {
            [Header("Event Info")]
            public string eventName = "감염 증식";
            public bool enabled = true;

            [Header("Timing")]
            public float startTime = 60f;
            public float duration = 15f;

            [Header("Spawn Reference")]
            [Tooltip("기본 스폰률이 너무 낮은 구간에서도 이벤트 체감이 나도록 하는 최소 기준 스폰률")]
            public float minimumReferenceSpawnRate = 1f;

            [Header("Low Tier Monsters")]
            public List<int> lowTierMonsterIndices = new List<int>();
            public float lowTierSpawnMultiplier = 4f;
            public float lowTierBaseShare = 1f;
            public float lowTierHpMultiplier = 1f;

            [Header("Mid Tier Monsters")]
            public List<int> midTierMonsterIndices = new List<int>();
            public float midTierSpawnMultiplier = 1.5f;
            public float midTierBaseShare = 0.5f;
            public float midTierHpMultiplier = 1f;

            [Header("High Tier Monsters")]
            public List<int> highTierMonsterIndices = new List<int>();
            public float highTierSpawnMultiplier = 1f;
            public float highTierBaseShare = 0.2f;
            public float highTierHpMultiplier = 1f;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public float lowAccumulator;
            [HideInInspector] public float midAccumulator;
            [HideInInspector] public float highAccumulator;

            public float EndTime => startTime + duration;
        }

        [System.Serializable]
        public class GoldRushEvent
        {
            [Header("Event Info")]
            public string eventName = "골드 러시";
            public bool enabled = true;

            [Header("Timing")]
            public float startTime = 30f;
            public float duration = 15f;

            [Header("Gold Modifiers")]
            public float coinDropAttemptMultiplier = 3f;
            public float coinValueMultiplier = 3f;
            public int maxCoinDropAttempts = 5;

            [Header("Guaranteed Bonus Coin")]
            [Range(0f, 1f)]
            public float additionalCoinDropChance = 1f;
            public int additionalCoinDropCount = 1;
            public CoinType additionalCoinType = CoinType.Bronze1;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;

            public float EndTime => startTime + duration;
        }

        [System.Serializable]
        public class AcidSecretionEvent
        {
            [Header("Event Info")]
            public string eventName = "위산분비";
            public bool enabled = true;

            [Header("Timing")]
            public float startTime = 45f;
            public float duration = 15f;

            [Header("Acid Puddle")]
            public GameObject acidPuddlePrefab;
            public float puddleSpawnInterval = 1f;
            public int puddlesPerWave = 3;
            public int maxActivePuddles = 12;
            public float minSpawnDistanceFromPlayer = 1.2f;
            public float maxSpawnDistanceFromPlayer = 6f;
            public float puddleLifeTime = 6f;
            public float puddleDamagePerTick = 3f;
            public float puddleTickInterval = 0.5f;
            public float puddleScale = 1.6f;
            public float puddleWarningDuration = 0.4f;
            public bool damageImmediatelyOnEnter = false;

            [Header("Acid Slime Spawn")]
            [Tooltip("위산 슬라임으로 사용할 몬스터 flatIndex 목록. 아직 위산 슬라임이 없다면 테스트용으로 저등급 몬스터 index 0을 넣어도 됩니다.")]
            public List<int> acidSlimeMonsterIndices = new List<int>();
            public float acidSlimeExtraSpawnMultiplier = 3f;
            public float acidSlimeMinimumReferenceSpawnRate = 1f;
            public float acidSlimeHpMultiplier = 1f;

            [Header("Reward")]
            public bool rewardOnEnd = true;
            public bool spawnChestAsTemporaryItemReward = true;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public bool rewarded;
            [HideInInspector] public float puddleSpawnTimer;
            [HideInInspector] public float acidSlimeAccumulator;
            [HideInInspector] public List<GameObject> activePuddles = new List<GameObject>();

            public float EndTime => startTime + duration;
        }

        [Header("References")]
        [SerializeField] private LevelManager levelManager;

        [Header("1. Monster Surge Events")]
        [SerializeField] private List<MonsterSurgeEvent> monsterSurgeEvents = new List<MonsterSurgeEvent>();

        [Header("2. Gold Rush Events")]
        [SerializeField] private List<GoldRushEvent> goldRushEvents = new List<GoldRushEvent>();

        [Header("3. Acid Secretion Events")]
        [SerializeField] private List<AcidSecretionEvent> acidSecretionEvents = new List<AcidSecretionEvent>();

        [Header("Debug")]
        [SerializeField] private bool logEventState = true;
        [SerializeField] private bool logMonsterIndexTableOnStart = false;
        [SerializeField] private bool logExtraSpawn = false;
        [SerializeField] private bool logGoldRushModifier = false;
        [SerializeField] private bool logAcidEvent = false;

        private void Start()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (levelManager != null && logMonsterIndexTableOnStart)
            {
                levelManager.LogMonsterIndexTable();
            }
        }

        private void Update()
        {
            if (levelManager == null)
            {
                return;
            }

            float currentTime = levelManager.CurrentLevelTime;

            StageEventRuntimeModifiers.ResetCoinModifiers();

            foreach (MonsterSurgeEvent surgeEvent in monsterSurgeEvents)
            {
                UpdateMonsterSurgeEvent(surgeEvent, currentTime);
            }

            foreach (GoldRushEvent goldRushEvent in goldRushEvents)
            {
                UpdateGoldRushEvent(goldRushEvent, currentTime);
            }

            foreach (AcidSecretionEvent acidEvent in acidSecretionEvents)
            {
                UpdateAcidSecretionEvent(acidEvent, currentTime);
            }
        }

        private void UpdateMonsterSurgeEvent(MonsterSurgeEvent surgeEvent, float currentTime)
        {
            if (surgeEvent == null || !surgeEvent.enabled || surgeEvent.finished)
            {
                return;
            }

            if (currentTime < surgeEvent.startTime)
            {
                return;
            }

            if (!surgeEvent.started)
            {
                surgeEvent.started = true;
                ResetMonsterSurgeRuntimeValues(surgeEvent);

                if (logEventState)
                {
                    Debug.Log($"[StageEvent] Start: {surgeEvent.eventName} | time={currentTime:F1}s | duration={surgeEvent.duration:F1}s");
                }
            }

            if (currentTime >= surgeEvent.EndTime)
            {
                surgeEvent.finished = true;

                if (logEventState)
                {
                    Debug.Log($"[StageEvent] End: {surgeEvent.eventName} | time={currentTime:F1}s");
                }

                return;
            }

            float baseSpawnRate = levelManager.GetCurrentBaseMonsterSpawnRate();
            float referenceSpawnRate = Mathf.Max(baseSpawnRate, surgeEvent.minimumReferenceSpawnRate);

            TickTierSpawn(
                "Low",
                surgeEvent.lowTierMonsterIndices,
                surgeEvent.lowTierSpawnMultiplier,
                surgeEvent.lowTierBaseShare,
                surgeEvent.lowTierHpMultiplier,
                referenceSpawnRate,
                ref surgeEvent.lowAccumulator
            );

            TickTierSpawn(
                "Mid",
                surgeEvent.midTierMonsterIndices,
                surgeEvent.midTierSpawnMultiplier,
                surgeEvent.midTierBaseShare,
                surgeEvent.midTierHpMultiplier,
                referenceSpawnRate,
                ref surgeEvent.midAccumulator
            );

            TickTierSpawn(
                "High",
                surgeEvent.highTierMonsterIndices,
                surgeEvent.highTierSpawnMultiplier,
                surgeEvent.highTierBaseShare,
                surgeEvent.highTierHpMultiplier,
                referenceSpawnRate,
                ref surgeEvent.highAccumulator
            );
        }

        private void UpdateGoldRushEvent(GoldRushEvent goldRushEvent, float currentTime)
        {
            if (goldRushEvent == null || !goldRushEvent.enabled || goldRushEvent.finished)
            {
                return;
            }

            if (currentTime < goldRushEvent.startTime)
            {
                return;
            }

            if (!goldRushEvent.started)
            {
                goldRushEvent.started = true;

                if (logEventState)
                {
                    Debug.Log($"[StageEvent] Start: {goldRushEvent.eventName} | time={currentTime:F1}s | duration={goldRushEvent.duration:F1}s");
                }
            }

            if (currentTime >= goldRushEvent.EndTime)
            {
                goldRushEvent.finished = true;

                if (logEventState)
                {
                    Debug.Log($"[StageEvent] End: {goldRushEvent.eventName} | time={currentTime:F1}s");
                }

                return;
            }

            StageEventRuntimeModifiers.ApplyGoldRushModifier(
                goldRushEvent.coinDropAttemptMultiplier,
                goldRushEvent.coinValueMultiplier,
                goldRushEvent.maxCoinDropAttempts,
                goldRushEvent.additionalCoinDropChance,
                goldRushEvent.additionalCoinDropCount,
                goldRushEvent.additionalCoinType,
                logGoldRushModifier
            );

            if (logGoldRushModifier)
            {
                Debug.Log(
                    $"[StageEvent] GoldRush Active | " +
                    $"attemptMultiplier={StageEventRuntimeModifiers.CoinDropAttemptMultiplier:F1} | " +
                    $"valueMultiplier={StageEventRuntimeModifiers.CoinValueMultiplier:F1} | " +
                    $"bonusChance={StageEventRuntimeModifiers.AdditionalCoinDropChance:F1} | " +
                    $"bonusCount={StageEventRuntimeModifiers.AdditionalCoinDropCount}"
                );
            }
        }

        private void UpdateAcidSecretionEvent(AcidSecretionEvent acidEvent, float currentTime)
        {
            if (acidEvent == null || !acidEvent.enabled || acidEvent.finished)
            {
                return;
            }

            if (currentTime < acidEvent.startTime)
            {
                return;
            }

            if (!acidEvent.started)
            {
                acidEvent.started = true;
                acidEvent.puddleSpawnTimer = 0f;
                acidEvent.acidSlimeAccumulator = 0f;

                if (logEventState)
                {
                    Debug.Log($"[StageEvent] Start: {acidEvent.eventName} | time={currentTime:F1}s | duration={acidEvent.duration:F1}s");
                }
            }

            if (currentTime >= acidEvent.EndTime)
            {
                acidEvent.finished = true;

                if (logEventState)
                {
                    Debug.Log($"[StageEvent] End: {acidEvent.eventName} | time={currentTime:F1}s");
                }

                if (acidEvent.rewardOnEnd && !acidEvent.rewarded)
                {
                    acidEvent.rewarded = true;
                    GiveAcidSecretionReward(acidEvent);
                }

                return;
            }

            TickAcidPuddleSpawn(acidEvent);
            TickAcidSlimeSpawn(acidEvent);
        }

        private void TickAcidPuddleSpawn(AcidSecretionEvent acidEvent)
        {
            if (acidEvent.acidPuddlePrefab == null)
            {
                return;
            }

            CleanNullPuddles(acidEvent);

            acidEvent.puddleSpawnTimer += Time.deltaTime;

            if (acidEvent.puddleSpawnTimer < acidEvent.puddleSpawnInterval)
            {
                return;
            }

            acidEvent.puddleSpawnTimer = 0f;

            for (int i = 0; i < acidEvent.puddlesPerWave; i++)
            {
                CleanNullPuddles(acidEvent);

                if (acidEvent.activePuddles.Count >= acidEvent.maxActivePuddles)
                {
                    return;
                }

                SpawnAcidPuddle(acidEvent);
            }
        }

        private void SpawnAcidPuddle(AcidSecretionEvent acidEvent)
        {
            Vector3 spawnPosition = GetRandomPositionAroundPlayer(
                acidEvent.minSpawnDistanceFromPlayer,
                acidEvent.maxSpawnDistanceFromPlayer
            );

            GameObject puddleObject = Instantiate(acidEvent.acidPuddlePrefab, spawnPosition, Quaternion.identity);
            acidEvent.activePuddles.Add(puddleObject);

            AcidPuddle acidPuddle = puddleObject.GetComponent<AcidPuddle>();
            if (acidPuddle != null)
            {
                acidPuddle.Init(
                    acidEvent.puddleLifeTime,
                    acidEvent.puddleDamagePerTick,
                    acidEvent.puddleTickInterval,
                    acidEvent.puddleScale,
                    acidEvent.damageImmediatelyOnEnter,
                    acidEvent.puddleWarningDuration
                );
            }

            if (logAcidEvent)
            {
                Debug.Log($"[StageEvent] Acid puddle spawned at {spawnPosition}");
            }
        }

        private void TickAcidSlimeSpawn(AcidSecretionEvent acidEvent)
        {
            if (acidEvent.acidSlimeMonsterIndices == null || acidEvent.acidSlimeMonsterIndices.Count == 0)
            {
                return;
            }

            float baseSpawnRate = levelManager.GetCurrentBaseMonsterSpawnRate();
            float referenceSpawnRate = Mathf.Max(baseSpawnRate, acidEvent.acidSlimeMinimumReferenceSpawnRate);

            float extraSpawnRate = referenceSpawnRate * Mathf.Max(0f, acidEvent.acidSlimeExtraSpawnMultiplier);

            if (extraSpawnRate <= 0f)
            {
                return;
            }

            acidEvent.acidSlimeAccumulator += Time.deltaTime * extraSpawnRate;

            while (acidEvent.acidSlimeAccumulator >= 1f)
            {
                levelManager.SpawnRandomMonsterFromFlatIndexList(
                    acidEvent.acidSlimeMonsterIndices,
                    acidEvent.acidSlimeHpMultiplier
                );

                acidEvent.acidSlimeAccumulator -= 1f;

                if (logAcidEvent)
                {
                    Debug.Log($"[StageEvent] Acid slime extra spawn | rate={extraSpawnRate:F2}");
                }
            }
        }

        private Vector3 GetRandomPositionAroundPlayer(float minDistance, float maxDistance)
        {
            Character playerCharacter = levelManager.PlayerCharacter;

            if (playerCharacter == null)
            {
                return transform.position;
            }

            Vector2 randomDirection = Random.insideUnitCircle.normalized;

            if (randomDirection == Vector2.zero)
            {
                randomDirection = Vector2.up;
            }

            float distance = Random.Range(minDistance, maxDistance);
            return playerCharacter.transform.position + (Vector3)(randomDirection * distance);
        }

        private void GiveAcidSecretionReward(AcidSecretionEvent acidEvent)
        {
            if (!acidEvent.spawnChestAsTemporaryItemReward)
            {
                return;
            }

            if (levelManager == null || levelManager.EntityManager == null || levelManager.CurrentLevelBlueprint == null)
            {
                Debug.LogWarning("[StageEvent] 위산분비 보상 지급 실패: LevelManager 정보가 비어 있습니다.");
                return;
            }

            levelManager.EntityManager.SpawnChest(levelManager.CurrentLevelBlueprint.chestBlueprint);

            if (logEventState)
            {
                Debug.Log("[StageEvent] 위산분비 보상: 임시 아이템 보상용 Chest 생성");
            }
        }

        private void CleanNullPuddles(AcidSecretionEvent acidEvent)
        {
            for (int i = acidEvent.activePuddles.Count - 1; i >= 0; i--)
            {
                if (acidEvent.activePuddles[i] == null)
                {
                    acidEvent.activePuddles.RemoveAt(i);
                }
            }
        }

        private void ResetMonsterSurgeRuntimeValues(MonsterSurgeEvent surgeEvent)
        {
            surgeEvent.lowAccumulator = 0f;
            surgeEvent.midAccumulator = 0f;
            surgeEvent.highAccumulator = 0f;
        }

        private void TickTierSpawn(
            string tierName,
            List<int> monsterIndices,
            float spawnMultiplier,
            float baseShare,
            float hpMultiplier,
            float referenceSpawnRate,
            ref float accumulator)
        {
            if (monsterIndices == null || monsterIndices.Count == 0)
            {
                return;
            }

            float extraMultiplier = Mathf.Max(0f, spawnMultiplier - 1f);

            if (extraMultiplier <= 0f)
            {
                return;
            }

            float extraSpawnRate = referenceSpawnRate * baseShare * extraMultiplier;

            if (extraSpawnRate <= 0f)
            {
                return;
            }

            accumulator += Time.deltaTime * extraSpawnRate;

            while (accumulator >= 1f)
            {
                levelManager.SpawnRandomMonsterFromFlatIndexList(monsterIndices, hpMultiplier);
                accumulator -= 1f;

                if (logExtraSpawn)
                {
                    Debug.Log($"[StageEvent] Extra Spawn | tier={tierName} | rate={extraSpawnRate:F2}");
                }
            }
        }

        [ContextMenu("Reset Stage Events Runtime")]
        private void ResetStageEventsRuntime()
        {
            foreach (MonsterSurgeEvent surgeEvent in monsterSurgeEvents)
            {
                if (surgeEvent == null)
                {
                    continue;
                }

                surgeEvent.started = false;
                surgeEvent.finished = false;
                ResetMonsterSurgeRuntimeValues(surgeEvent);
            }

            foreach (GoldRushEvent goldRushEvent in goldRushEvents)
            {
                if (goldRushEvent == null)
                {
                    continue;
                }

                goldRushEvent.started = false;
                goldRushEvent.finished = false;
            }

            foreach (AcidSecretionEvent acidEvent in acidSecretionEvents)
            {
                if (acidEvent == null)
                {
                    continue;
                }

                acidEvent.started = false;
                acidEvent.finished = false;
                acidEvent.rewarded = false;
                acidEvent.puddleSpawnTimer = 0f;
                acidEvent.acidSlimeAccumulator = 0f;

                foreach (GameObject puddle in acidEvent.activePuddles)
                {
                    if (puddle != null)
                    {
                        Destroy(puddle);
                    }
                }

                acidEvent.activePuddles.Clear();
            }

            StageEventRuntimeModifiers.ResetCoinModifiers();

            Debug.Log("[StageEvent] Runtime reset complete.");
        }
    }
}
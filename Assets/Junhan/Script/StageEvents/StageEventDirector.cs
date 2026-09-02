using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class StageEventDirector : MonoBehaviour
    {
        [System.Serializable]
        public class EventStartTimeRange
        {
            [Tooltip("랜덤 시작 최소 시간입니다. 단위는 분입니다. 2 = 2분")]
            public float minStartMinute = 2f;

            [Tooltip("랜덤 시작 최대 시간입니다. 단위는 분입니다. 4 = 4분")]
            public float maxStartMinute = 4f;

            public float GetRandomStartTimeSeconds()
            {
                float min = Mathf.Min(minStartMinute, maxStartMinute);
                float max = Mathf.Max(minStartMinute, maxStartMinute);

                return Random.Range(min, max) * 60f;
            }

            public bool IsValid()
            {
                float min = Mathf.Min(minStartMinute, maxStartMinute);
                float max = Mathf.Max(minStartMinute, maxStartMinute);

                return max > 0f && !Mathf.Approximately(min, max);
            }
        }

        [System.Serializable]
        public class MonsterSurgeEvent
        {
            [Header("Event Info")]
            [Tooltip("UI에 표시될 이벤트 이름입니다.")]
            public string eventName = "감염 증식";

            [Tooltip("체크되어 있으면 이벤트가 발동됩니다.")]
            public bool enabled = true;

            [Header("Timing")]
            [Tooltip("랜덤 시간을 쓰지 않을 때 사용하는 고정 시작 시간입니다. 단위는 초입니다. 60 = 1분")]
            public float startTime = 60f;

            [Tooltip("이벤트 지속 시간입니다. 단위는 초입니다.")]
            public float duration = 15f;

            [Tooltip("체크하면 아래 시간 범위 중 하나를 골라 랜덤 시작 시간을 정합니다.")]
            public bool useRandomStartTime = true;

            [Tooltip("랜덤 시작 시간 범위입니다. 단위는 분입니다. 예: 2~4분, 4~6분")]
            public List<EventStartTimeRange> startTimeRanges = new List<EventStartTimeRange>();

            [Header("Spawn Surge")]
            [Tooltip("현재 시간대의 Monster Spawn Table 비율을 그대로 따라가며 스폰량을 몇 배로 늘릴지 정합니다. 2 = 총 2배 느낌")]
            public float spawnMultiplier = 2f;

            [Tooltip("기본 스폰률이 너무 낮은 구간에서도 이벤트 체감이 나도록 하는 최소 기준 스폰률입니다.")]
            public float minimumReferenceSpawnRate = 1f;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public float resolvedStartTime;
            [HideInInspector] public float spawnAccumulator;

            public float EndTime => resolvedStartTime + duration;
        }

        [System.Serializable]
        public class GoldRushEvent
        {
            [Header("Event Info")]
            [Tooltip("UI에 표시될 이벤트 이름입니다.")]
            public string eventName = "골드 러시";

            [Tooltip("체크되어 있으면 이벤트가 발동됩니다.")]
            public bool enabled = true;

            [Header("Timing")]
            [Tooltip("랜덤 시간을 쓰지 않을 때 사용하는 고정 시작 시간입니다. 단위는 초입니다. 60 = 1분")]
            public float startTime = 30f;

            [Tooltip("이벤트 지속 시간입니다. 단위는 초입니다.")]
            public float duration = 15f;

            [Tooltip("체크하면 아래 시간 범위 중 하나를 골라 랜덤 시작 시간을 정합니다.")]
            public bool useRandomStartTime = true;

            [Tooltip("랜덤 시작 시간 범위입니다. 단위는 분입니다. 예: 2~4분, 4~6분")]
            public List<EventStartTimeRange> startTimeRanges = new List<EventStartTimeRange>();

            [Header("Guaranteed Gold Drop")]
            [Tooltip("골드러쉬 중 일반 몬스터가 무조건 드랍할 코인입니다. 5원 골드는 Gold5입니다.")]
            public CoinType guaranteedCoinType = CoinType.Gold5;

            [Tooltip("골드러쉬 중 몬스터 1마리당 강제 드랍할 코인 개수입니다.")]
            public int guaranteedCoinCount = 1;

            [Tooltip("체크하면 일반 몬스터에게만 강제 골드 드랍을 적용하고 엘리트 몬스터는 제외합니다.")]
            public bool applyOnlyToNormalMonsters = true;

            [Tooltip("체크하면 기존 coinLootTable 확률 드랍은 막고, 강제 Gold5만 드랍합니다.")]
            public bool suppressOriginalCoinDrops = true;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public float resolvedStartTime;

            public float EndTime => resolvedStartTime + duration;
        }

        [System.Serializable]
        public class AcidSecretionEvent
        {
            [Header("Event Info")]
            [Tooltip("UI에 표시될 이벤트 이름입니다.")]
            public string eventName = "위산분비";

            [Tooltip("체크되어 있으면 이벤트가 발동됩니다.")]
            public bool enabled = true;

            [Header("Timing")]
            [Tooltip("랜덤 시간을 쓰지 않을 때 사용하는 고정 시작 시간입니다. 단위는 초입니다. 60 = 1분")]
            public float startTime = 45f;

            [Tooltip("이벤트 지속 시간입니다. 단위는 초입니다.")]
            public float duration = 15f;

            [Tooltip("체크하면 아래 시간 범위 중 하나를 골라 랜덤 시작 시간을 정합니다.")]
            public bool useRandomStartTime = true;

            [Tooltip("랜덤 시작 시간 범위입니다. 단위는 분입니다. 예: 2~4분, 4~6분")]
            public List<EventStartTimeRange> startTimeRanges = new List<EventStartTimeRange>();

            [Header("Acid Puddle")]
            [Tooltip("위산 바닥 프리팹입니다.")]
            public GameObject acidPuddlePrefab;

            [Tooltip("위산 바닥 생성 간격입니다.")]
            public float puddleSpawnInterval = 1f;

            [Tooltip("한 번 생성될 때 몇 개의 위산 바닥을 만들지 정합니다.")]
            public int puddlesPerWave = 3;

            [Tooltip("동시에 존재할 수 있는 위산 바닥 최대 개수입니다.")]
            public int maxActivePuddles = 12;

            [Tooltip("플레이어 기준 최소 생성 거리입니다.")]
            public float minSpawnDistanceFromPlayer = 1.2f;

            [Tooltip("플레이어 기준 최대 생성 거리입니다.")]
            public float maxSpawnDistanceFromPlayer = 6f;

            [Tooltip("위산 바닥이 유지되는 시간입니다.")]
            public float puddleLifeTime = 6f;

            [Tooltip("위산 바닥이 틱마다 주는 데미지입니다.")]
            public float puddleDamagePerTick = 3f;

            [Tooltip("위산 바닥 데미지 틱 간격입니다.")]
            public float puddleTickInterval = 0.5f;

            [Tooltip("위산 바닥 크기입니다.")]
            public float puddleScale = 1.6f;

            [Tooltip("위산 바닥 생성 후 데미지가 활성화되기 전 경고 시간입니다.")]
            public float puddleWarningDuration = 0.4f;

            [Tooltip("체크하면 플레이어가 위산 바닥에 들어간 즉시 데미지를 받습니다.")]
            public bool damageImmediatelyOnEnter = false;

            [Header("Acid Slime Spawn")]
            [Tooltip("위산 슬라임으로 사용할 몬스터 flatIndex 목록입니다.")]
            public List<int> acidSlimeMonsterIndices = new List<int>();

            [Tooltip("위산 이벤트 중 추가 슬라임 스폰량 배수입니다.")]
            public float acidSlimeExtraSpawnMultiplier = 3f;

            [Tooltip("기본 스폰률이 너무 낮은 구간에서도 위산 슬라임이 나오도록 하는 최소 기준 스폰률입니다.")]
            public float acidSlimeMinimumReferenceSpawnRate = 1f;

            [Tooltip("위산 슬라임 HP 배율입니다.")]
            public float acidSlimeHpMultiplier = 1f;

            [Header("Reward")]
            [Tooltip("위산분비 이벤트 종료 시 보상을 지급할지 여부입니다.")]
            public bool rewardOnEnd = true;

            [Tooltip("위산분비 이벤트 종료 보상으로 임시 아이템 보상용 상자를 생성할지 여부입니다.")]
            public bool spawnChestAsTemporaryItemReward = true;

            [Header("Runtime")]
            [HideInInspector] public bool started;
            [HideInInspector] public bool finished;
            [HideInInspector] public bool rewarded;
            [HideInInspector] public float resolvedStartTime;
            [HideInInspector] public float puddleSpawnTimer;
            [HideInInspector] public float acidSlimeAccumulator;
            [HideInInspector] public List<GameObject> activePuddles = new List<GameObject>();

            public float EndTime => resolvedStartTime + duration;
        }

        [Header("References")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("이벤트 시작 알림 UI입니다.")]
        [SerializeField] private StageEventToastUI eventToastUI;

        [Tooltip("위산분비 이벤트 중 화면 전체에 녹색 위산비를 뿌리는 VFX 컨트롤러입니다. 비워두면 씬에서 자동으로 찾습니다.")]
        [SerializeField] private global::AcidRainScreenVFXController acidRainScreenVFX;

        [Header("UI Message")]
        [Tooltip("{0} 위치에 이벤트 이름이 들어갑니다.")]
        [SerializeField] private string eventStartMessageFormat = "{0} 이벤트가 시작됐습니다!";

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

        private int activeAcidRainEventCount;

        private void Start()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (eventToastUI == null)
            {
                eventToastUI = FindObjectOfType<StageEventToastUI>();
            }

            FindAcidRainVFXIfNeeded();

            PrepareAllEvents();

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

        private void PrepareAllEvents()
        {
            activeAcidRainEventCount = 0;

            if (acidRainScreenVFX != null)
            {
                acidRainScreenVFX.StopImmediately();
            }

            foreach (MonsterSurgeEvent surgeEvent in monsterSurgeEvents)
            {
                if (surgeEvent == null)
                {
                    continue;
                }

                surgeEvent.started = false;
                surgeEvent.finished = false;
                surgeEvent.spawnAccumulator = 0f;
                surgeEvent.resolvedStartTime = ResolveStartTime(
                    surgeEvent.eventName,
                    surgeEvent.startTime,
                    surgeEvent.useRandomStartTime,
                    surgeEvent.startTimeRanges
                );
            }

            foreach (GoldRushEvent goldRushEvent in goldRushEvents)
            {
                if (goldRushEvent == null)
                {
                    continue;
                }

                goldRushEvent.started = false;
                goldRushEvent.finished = false;
                goldRushEvent.resolvedStartTime = ResolveStartTime(
                    goldRushEvent.eventName,
                    goldRushEvent.startTime,
                    goldRushEvent.useRandomStartTime,
                    goldRushEvent.startTimeRanges
                );
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
                acidEvent.resolvedStartTime = ResolveStartTime(
                    acidEvent.eventName,
                    acidEvent.startTime,
                    acidEvent.useRandomStartTime,
                    acidEvent.startTimeRanges
                );

                if (acidEvent.activePuddles == null)
                {
                    acidEvent.activePuddles = new List<GameObject>();
                }
                else
                {
                    acidEvent.activePuddles.Clear();
                }
            }
        }

        private float ResolveStartTime(
            string eventName,
            float fallbackStartTime,
            bool useRandomStartTime,
            List<EventStartTimeRange> ranges)
        {
            if (!useRandomStartTime)
            {
                return Mathf.Max(0f, fallbackStartTime);
            }

            List<EventStartTimeRange> validRanges = new List<EventStartTimeRange>();

            if (ranges != null)
            {
                for (int i = 0; i < ranges.Count; i++)
                {
                    if (ranges[i] != null && ranges[i].IsValid())
                    {
                        validRanges.Add(ranges[i]);
                    }
                }
            }

            if (validRanges.Count == 0)
            {
                Debug.LogWarning(
                    $"[StageEvent] {eventName}: Use Random Start Time이 켜져 있지만 유효한 Start Time Range가 없습니다. " +
                    $"fallback Start Time {fallbackStartTime:F1}초를 사용합니다. 1분은 60초입니다."
                );

                return Mathf.Max(0f, fallbackStartTime);
            }

            EventStartTimeRange selectedRange = validRanges[Random.Range(0, validRanges.Count)];
            float resolvedTime = selectedRange.GetRandomStartTimeSeconds();

            if (logEventState)
            {
                Debug.Log(
                    $"[StageEvent] {eventName} random start resolved: {resolvedTime:F1}s " +
                    $"({resolvedTime / 60f:F2}min)"
                );
            }

            return Mathf.Max(0f, resolvedTime);
        }

        private void UpdateMonsterSurgeEvent(MonsterSurgeEvent surgeEvent, float currentTime)
        {
            if (surgeEvent == null || !surgeEvent.enabled || surgeEvent.finished)
            {
                return;
            }

            if (currentTime < surgeEvent.resolvedStartTime)
            {
                return;
            }

            if (!surgeEvent.started)
            {
                surgeEvent.started = true;
                surgeEvent.spawnAccumulator = 0f;

                ShowEventStartedUI(surgeEvent.eventName);

                GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.FieldEventStart
);

                if (logEventState)
                {
                    Debug.Log(
                        $"[StageEvent] Start: {surgeEvent.eventName} | " +
                        $"time={currentTime:F1}s | planned={surgeEvent.resolvedStartTime:F1}s | duration={surgeEvent.duration:F1}s"
                    );
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

            TickMonsterSurgeSpawn(surgeEvent);
        }

        private void TickMonsterSurgeSpawn(MonsterSurgeEvent surgeEvent)
        {
            float baseSpawnRate = levelManager.GetCurrentBaseMonsterSpawnRate();
            float referenceSpawnRate = Mathf.Max(baseSpawnRate, surgeEvent.minimumReferenceSpawnRate);
            float extraMultiplier = Mathf.Max(0f, surgeEvent.spawnMultiplier - 1f);
            float extraSpawnRate = referenceSpawnRate * extraMultiplier;

            if (extraSpawnRate <= 0f)
            {
                return;
            }

            surgeEvent.spawnAccumulator += Time.deltaTime * extraSpawnRate;

            while (surgeEvent.spawnAccumulator >= 1f)
            {
                levelManager.SpawnMonsterFromCurrentSpawnTable();
                surgeEvent.spawnAccumulator -= 1f;

                if (logExtraSpawn)
                {
                    Debug.Log(
                        $"[StageEvent] Monster Surge Extra Spawn | " +
                        $"baseRate={baseSpawnRate:F2} | multiplier={surgeEvent.spawnMultiplier:F2} | extraRate={extraSpawnRate:F2}"
                    );
                }
            }
        }

        private void UpdateGoldRushEvent(GoldRushEvent goldRushEvent, float currentTime)
        {
            if (goldRushEvent == null || !goldRushEvent.enabled || goldRushEvent.finished)
            {
                return;
            }

            if (currentTime < goldRushEvent.resolvedStartTime)
            {
                return;
            }

            if (!goldRushEvent.started)
            {
                goldRushEvent.started = true;

                ShowEventStartedUI(goldRushEvent.eventName);

                GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.FieldEventStart
);

                if (logEventState)
                {
                    Debug.Log(
                        $"[StageEvent] Start: {goldRushEvent.eventName} | " +
                        $"time={currentTime:F1}s | planned={goldRushEvent.resolvedStartTime:F1}s | duration={goldRushEvent.duration:F1}s"
                    );
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

            StageEventRuntimeModifiers.ApplyGoldRushForcedCoinDrop(
                goldRushEvent.guaranteedCoinType,
                goldRushEvent.guaranteedCoinCount,
                goldRushEvent.applyOnlyToNormalMonsters,
                goldRushEvent.suppressOriginalCoinDrops,
                logGoldRushModifier
            );
        }

        private void UpdateAcidSecretionEvent(AcidSecretionEvent acidEvent, float currentTime)
        {
            if (acidEvent == null || !acidEvent.enabled || acidEvent.finished)
            {
                return;
            }

            if (currentTime < acidEvent.resolvedStartTime)
            {
                return;
            }

            if (!acidEvent.started)
            {
                acidEvent.started = true;
                acidEvent.puddleSpawnTimer = 0f;
                acidEvent.acidSlimeAccumulator = 0f;

                PlayAcidRainVFX();

                ShowEventStartedUI(acidEvent.eventName);

                GameAudioManager.PlaySfx(
    GameAudioManager.GameSfxId.FieldEventStart
);

                if (logEventState)
                {
                    Debug.Log(
                        $"[StageEvent] Start: {acidEvent.eventName} | " +
                        $"time={currentTime:F1}s | planned={acidEvent.resolvedStartTime:F1}s | duration={acidEvent.duration:F1}s"
                    );
                }
            }

            if (currentTime >= acidEvent.EndTime)
            {
                acidEvent.finished = true;

                StopAcidRainVFX();

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

            GameObject puddleObject = Instantiate(
                acidEvent.acidPuddlePrefab,
                spawnPosition,
                Quaternion.identity
            );

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

            // 위산 웅덩이 GameObject가 실제 생성된 순간
            // 웅덩이 하나당 1회 재생합니다.
            if (puddleObject != null)
            {
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.AcidPuddleSpawn
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
            float referenceSpawnRate = Mathf.Max(
                baseSpawnRate,
                acidEvent.acidSlimeMinimumReferenceSpawnRate
            );

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

            float safeMinDistance = Mathf.Max(0f, minDistance);
            float safeMaxDistance = Mathf.Max(safeMinDistance, maxDistance);
            float distance = Random.Range(safeMinDistance, safeMaxDistance);

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
            if (acidEvent.activePuddles == null)
            {
                acidEvent.activePuddles = new List<GameObject>();
                return;
            }

            for (int i = acidEvent.activePuddles.Count - 1; i >= 0; i--)
            {
                if (acidEvent.activePuddles[i] == null)
                {
                    acidEvent.activePuddles.RemoveAt(i);
                }
            }
        }

        private void ShowEventStartedUI(string eventName)
        {
            string safeEventName =
                string.IsNullOrEmpty(eventName)
                    ? "스테이지"
                    : eventName;

            string message =
                string.Format(
                    eventStartMessageFormat,
                    safeEventName
                );

            // 기존 필드 이벤트도 모두 동일한 시작 효과음을 사용합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.FieldEventStart
            );

            if (eventToastUI != null)
            {
                eventToastUI.Show(message);
            }

            if (logEventState)
            {
                Debug.Log(
                    $"[StageEvent UI] {message}"
                );
            }
        }

        private void FindAcidRainVFXIfNeeded()
        {
            if (acidRainScreenVFX != null)
            {
                return;
            }

            acidRainScreenVFX = FindObjectOfType<global::AcidRainScreenVFXController>();
        }

        private void PlayAcidRainVFX()
        {
            activeAcidRainEventCount = Mathf.Max(0, activeAcidRainEventCount) + 1;

            FindAcidRainVFXIfNeeded();

            if (acidRainScreenVFX == null)
            {
                if (logAcidEvent)
                {
                    Debug.LogWarning("[StageEvent] AcidRainScreenVFXController를 찾지 못했습니다. 위산비 VFX는 재생되지 않습니다.");
                }

                return;
            }

            if (activeAcidRainEventCount == 1)
            {
                acidRainScreenVFX.PlayRain();
            }
        }

        private void StopAcidRainVFX()
        {
            activeAcidRainEventCount = Mathf.Max(0, activeAcidRainEventCount - 1);

            if (activeAcidRainEventCount > 0)
            {
                return;
            }

            FindAcidRainVFXIfNeeded();

            if (acidRainScreenVFX == null)
            {
                return;
            }

            acidRainScreenVFX.StopRain();
        }

        [ContextMenu("Reset Stage Events Runtime")]
        private void ResetStageEventsRuntime()
        {
            foreach (AcidSecretionEvent acidEvent in acidSecretionEvents)
            {
                if (acidEvent == null || acidEvent.activePuddles == null)
                {
                    continue;
                }

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

            activeAcidRainEventCount = 0;

            FindAcidRainVFXIfNeeded();

            if (acidRainScreenVFX != null)
            {
                acidRainScreenVFX.StopImmediately();
            }

            PrepareAllEvents();

            Debug.Log("[StageEvent] Runtime reset complete.");
        }
    }
}
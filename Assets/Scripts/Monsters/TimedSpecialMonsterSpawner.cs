using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 저격수 같은 시간표 기반 특수 몬스터와,
    /// 함정 몬스터처럼 필드에 일정 수를 유지하는 특수 몬스터를 한 곳에서 관리하는 통합 스포너.
    /// </summary>
    public class TimedSpecialMonsterSpawner : MonoBehaviour
    {
        public enum SpecialSpawnMode
        {
            TimedSpawn,
            TrapFieldMaintain
        }

        public enum SpawnTimeMode
        {
            ExactTime,
            RandomTimeWindow
        }

        public enum SpawnCountMode
        {
            FixedCount,
            RandomRange
        }

        [System.Serializable]
        public class TimedSpawnEntry
        {
            [Header("Entry Mode")]
            [Tooltip("Timed Spawn: 저격수, 미니보스처럼 특정 시간 또는 랜덤 시간대에 스폰합니다.\nTrap Field Maintain: 함정 몬스터처럼 필드에 일정 수를 유지합니다.")]
            public SpecialSpawnMode specialSpawnMode = SpecialSpawnMode.TimedSpawn;

            [Tooltip("디버그용 이름입니다. 예: 2~3분 저격수 랜덤 스폰 / 함정 몬스터 필드 유지")]
            public string memo = "Special Monster Spawn";

            [Header("Monster")]
            [Tooltip("true면 MonsterBlueprint가 들어있는 LevelBlueprint의 poolIndex를 자동으로 찾습니다.")]
            public bool autoResolvePoolIndex = true;

            [Tooltip("autoResolvePoolIndex가 꺼져 있을 때만 사용하는 수동 poolIndex입니다.")]
            public int monsterPoolIndex = 4;

            [Tooltip("스폰할 몬스터 블루프린트입니다.\n저격수면 Sniper Monster Blueprint, 함정이면 Trap Monster Blueprint를 넣어주세요.")]
            public MonsterBlueprint monsterBlueprint;

            [Tooltip("추가 HP 보정값입니다. 보통 0으로 둡니다.")]
            public float hpBuff = 0f;

            [Header("Timed Spawn - Time")]
            [Tooltip("Exact Time: 지정한 시간에 스폰합니다. Random Time Window: 시작 시간과 종료 시간 사이의 랜덤한 시간에 스폰합니다.")]
            public SpawnTimeMode spawnTimeMode = SpawnTimeMode.ExactTime;

            [Tooltip("Exact Time 모드일 때 사용합니다. 게임 시작 후 몇 초에 스폰할지 설정합니다.\n예: 150 = 2분 30초")]
            public float spawnTimeSeconds = 150f;

            [Tooltip("Random Time Window 모드일 때 사용합니다. 이 시간부터 랜덤 스폰 후보가 됩니다. 예: 120 = 2분")]
            public float randomStartTimeSeconds = 120f;

            [Tooltip("Random Time Window 모드일 때 사용합니다. 이 시간까지 랜덤 스폰 후보가 됩니다. 예: 180 = 3분")]
            public float randomEndTimeSeconds = 180f;

            [Tooltip("Random Time Window 모드일 때, 이 시간대 안에서 스폰 이벤트를 몇 번 발생시킬지 설정합니다.")]
            public int randomEventCount = 1;

            [Header("Timed Spawn - Count")]
            [Tooltip("Fixed Count: 고정 마릿수 스폰.\nRandom Range: 최소~최대 사이 랜덤 마릿수 스폰.")]
            public SpawnCountMode spawnCountMode = SpawnCountMode.FixedCount;

            [Tooltip("Fixed Count 모드일 때 사용합니다. 한 번 스폰 이벤트가 발생할 때 몇 마리를 스폰할지 설정합니다.")]
            public int spawnCount = 1;

            [Tooltip("Random Range 모드일 때 사용합니다. 한 번 스폰 이벤트가 발생할 때 스폰될 최소 마릿수입니다.")]
            public int randomMinSpawnCount = 1;

            [Tooltip("Random Range 모드일 때 사용합니다. 한 번 스폰 이벤트가 발생할 때 스폰될 최대 마릿수입니다.")]
            public int randomMaxSpawnCount = 3;

            [Tooltip("여러 마리를 스폰할 때 한 마리씩 나오는 간격입니다.\n0이면 동시에 스폰됩니다.")]
            public float intervalBetweenSpawns = 0.15f;

            [Header("Trap Field Maintain - Start")]
            [Tooltip("Trap Field Maintain 모드일 때 사용합니다. 체크하면 게임 시작 후 바로 함정을 최대 개수만큼 배치합니다.")]
            public bool trapSpawnOnStart = true;

            [Tooltip("trapSpawnOnStart가 꺼져 있을 때 사용합니다.\n게임 시작 후 몇 초에 함정 필드 유지 기능을 시작할지 설정합니다.")]
            public float trapActivateTimeSeconds = 0f;

            [Tooltip("함정 필드 유지 기능이 시작될 때 즉시 최대 개수만큼 함정을 스폰합니다.")]
            public bool spawnInitialTrapsOnActivate = true;

            [Header("Trap Field Maintain - Count")]
            [Tooltip("필드에 동시에 존재할 수 있는 최대 함정 수입니다.")]
            public int maxActiveTraps = 4;

            [Tooltip("함정이 죽은 뒤 새 함정이 다시 스폰되기까지의 시간입니다.")]
            public float respawnDelay = 60f;

            [Header("Trap Field Maintain - Position")]
            [Tooltip("플레이어 기준 최소 스폰 거리입니다.")]
            public float minDistanceFromPlayer = 2f;

            [Tooltip("플레이어 기준 최대 스폰 거리입니다.")]
            public float maxDistanceFromPlayer = 5f;

            [Tooltip("함정끼리 최소한 이 정도 거리를 두고 배치합니다.")]
            public float minDistanceBetweenTraps = 1.5f;

            [Tooltip("적절한 위치를 찾기 위해 몇 번까지 재시도할지 설정합니다.")]
            public int spawnPositionTryCount = 20;
        }

        private class RuntimeTimedSpawnEvent
        {
            public TimedSpawnEntry entry;
            public float scheduledTime;
            public int spawnCount;
            public bool spawned;
            public string runtimeMemo;
        }

        private class RuntimeTrapFieldState
        {
            public TimedSpawnEntry entry;
            public bool activated;
            public readonly List<Monster> activeTraps = new List<Monster>();
            public readonly List<Coroutine> respawnCoroutines = new List<Coroutine>();
        }

        [Header("References")]
        [Tooltip("씬에 있는 LevelManager를 넣어주세요.\n비워두면 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Header("Schedule")]
        [Tooltip("특수 몬스터 스폰 테이블입니다.\n저격수는 Timed Spawn, 함정은 Trap Field Maintain 모드로 추가하면 됩니다.")]
        [SerializeField] private TimedSpawnEntry[] spawnEntries;

        [Header("Mini Stage Guard")]
        [Tooltip("미니 스테이지 진행 중에는 시간표 특수 몬스터와 함정 몬스터 스폰을 멈춥니다.")]
        [SerializeField] private bool blockWhileRunFlowPaused = true;

        [Tooltip("미니 스테이지 때문에 스폰이 차단될 때 로그를 출력합니다.")]
        [SerializeField] private bool logMiniStageBlock = false;

        [Header("Debug")]
        [Tooltip("체크하면 특수 몬스터 스폰 시간표 계산, 스폰 성공, 오류 로그를 Console에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private EntityManager entityManager;
        private LevelBlueprint levelBlueprint;
        private Character playerCharacter;
        private MethodInfo spawnMonsterMethod;

        private readonly List<RuntimeTimedSpawnEvent> runtimeTimedSpawnEvents = new List<RuntimeTimedSpawnEvent>();
        private readonly List<RuntimeTrapFieldState> runtimeTrapFieldStates = new List<RuntimeTrapFieldState>();

        private float elapsedTime = 0f;
        private bool runtimeScheduleBuilt = false;

        private void Awake()
        {
            BuildRuntimeSchedule();
        }

        private IEnumerator Start()
        {
            ResolveReferences();

            // LevelManager가 EntityManager 풀을 초기화하기 전에 이 스포너가 먼저 실행될 수 있어서 2프레임 대기.
            yield return null;
            yield return null;

            ResolveReferences();
        }

        private void Update()
        {
            if (!runtimeScheduleBuilt)
            {
                BuildRuntimeSchedule();
            }

            if (entityManager == null || levelBlueprint == null || playerCharacter == null || spawnMonsterMethod == null)
            {
                ResolveReferences();

                if (entityManager == null || levelBlueprint == null)
                {
                    return;
                }
            }

            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[TimedSpecialMonsterSpawner] 미니 스테이지 진행 중이라 특수 몬스터 스폰 타이머를 멈춥니다.", this);
                }

                return;
            }

            elapsedTime += Time.deltaTime;

            UpdateTimedSpawnEvents();
            UpdateTrapFieldStates();
        }

        /// <summary>
        /// 같은 씬에서 게임을 다시 시작하는 구조일 때 외부에서 호출하면 스포너 상태를 초기화할 수 있다.
        /// 씬을 다시 로드하는 방식이면 직접 호출하지 않아도 된다.
        /// </summary>
        public void ResetSpawnerRuntime()
        {
            StopAllCoroutines();
            RemoveAllTrapListeners();

            elapsedTime = 0f;
            runtimeScheduleBuilt = false;

            BuildRuntimeSchedule();

            if (debugLog)
            {
                Debug.Log("[TimedSpecialMonsterSpawner] 런타임 스폰 테이블을 초기화했습니다.", this);
            }
        }

        private void BuildRuntimeSchedule()
        {
            runtimeTimedSpawnEvents.Clear();
            runtimeTrapFieldStates.Clear();

            if (spawnEntries == null)
            {
                spawnEntries = new TimedSpawnEntry[0];
            }

            for (int entryIndex = 0; entryIndex < spawnEntries.Length; entryIndex++)
            {
                TimedSpawnEntry entry = spawnEntries[entryIndex];

                if (entry == null)
                {
                    continue;
                }

                if (entry.specialSpawnMode == SpecialSpawnMode.TimedSpawn)
                {
                    BuildTimedSpawnRuntimeEvents(entry, entryIndex);
                }
                else if (entry.specialSpawnMode == SpecialSpawnMode.TrapFieldMaintain)
                {
                    RuntimeTrapFieldState state = new RuntimeTrapFieldState
                    {
                        entry = entry,
                        activated = false
                    };

                    runtimeTrapFieldStates.Add(state);
                }
            }

            runtimeTimedSpawnEvents.Sort((a, b) => a.scheduledTime.CompareTo(b.scheduledTime));
            runtimeScheduleBuilt = true;

            if (debugLog)
            {
                Debug.Log(
                    $"[TimedSpecialMonsterSpawner] 런타임 스폰 테이블 생성 완료 | " +
                    $"Timed Events: {runtimeTimedSpawnEvents.Count} | " +
                    $"Trap Fields: {runtimeTrapFieldStates.Count}",
                    this
                );

                for (int i = 0; i < runtimeTimedSpawnEvents.Count; i++)
                {
                    RuntimeTimedSpawnEvent runtimeEvent = runtimeTimedSpawnEvents[i];

                    string blueprintName = runtimeEvent.entry != null && runtimeEvent.entry.monsterBlueprint != null
                        ? runtimeEvent.entry.monsterBlueprint.name
                        : "NULL";

                    Debug.Log(
                        $"[TimedSpecialMonsterSpawner] Timed 예약 #{i + 1} | " +
                        $"시간: {FormatTime(runtimeEvent.scheduledTime)} | " +
                        $"마릿수: {runtimeEvent.spawnCount} | " +
                        $"Blueprint: {blueprintName} | " +
                        $"Memo: {runtimeEvent.runtimeMemo}",
                        this
                    );
                }

                for (int i = 0; i < runtimeTrapFieldStates.Count; i++)
                {
                    RuntimeTrapFieldState state = runtimeTrapFieldStates[i];
                    TimedSpawnEntry entry = state.entry;

                    string blueprintName = entry != null && entry.monsterBlueprint != null
                        ? entry.monsterBlueprint.name
                        : "NULL";

                    Debug.Log(
                        $"[TimedSpecialMonsterSpawner] Trap Field 예약 #{i + 1} | " +
                        $"시작 시간: {FormatTime(GetTrapActivateTime(entry))} | " +
                        $"MaxActiveTraps: {entry.maxActiveTraps} | " +
                        $"RespawnDelay: {entry.respawnDelay} | " +
                        $"Blueprint: {blueprintName} | " +
                        $"Memo: {entry.memo}",
                        this
                    );
                }
            }
        }

        private void BuildTimedSpawnRuntimeEvents(TimedSpawnEntry entry, int entryIndex)
        {
            int eventCount = GetTimedEventCount(entry);

            for (int eventIndex = 0; eventIndex < eventCount; eventIndex++)
            {
                RuntimeTimedSpawnEvent runtimeEvent = new RuntimeTimedSpawnEvent
                {
                    entry = entry,
                    scheduledTime = GetScheduledTime(entry),
                    spawnCount = GetSpawnCount(entry),
                    spawned = false,
                    runtimeMemo = MakeRuntimeMemo(entry, entryIndex, eventIndex, eventCount)
                };

                runtimeTimedSpawnEvents.Add(runtimeEvent);
            }
        }

        private void UpdateTimedSpawnEvents()
        {
            for (int i = 0; i < runtimeTimedSpawnEvents.Count; i++)
            {
                RuntimeTimedSpawnEvent runtimeEvent = runtimeTimedSpawnEvents[i];

                if (runtimeEvent == null || runtimeEvent.spawned)
                {
                    continue;
                }

                if (elapsedTime >= runtimeEvent.scheduledTime)
                {
                    runtimeEvent.spawned = true;
                    StartCoroutine(SpawnTimedEventRoutine(runtimeEvent));
                }
            }
        }

        private void UpdateTrapFieldStates()
        {
            for (int i = 0; i < runtimeTrapFieldStates.Count; i++)
            {
                RuntimeTrapFieldState state = runtimeTrapFieldStates[i];

                if (state == null || state.entry == null)
                {
                    continue;
                }

                if (state.activated)
                {
                    CleanupNullTraps(state);
                    continue;
                }

                float activateTime = GetTrapActivateTime(state.entry);

                if (elapsedTime >= activateTime)
                {
                    ActivateTrapField(state);
                }
            }
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();

                if (levelManager != null && debugLog)
                {
                    Debug.Log("[TimedSpecialMonsterSpawner] LevelManager를 자동으로 찾았습니다.", this);
                }
            }

            if (levelManager != null)
            {
                entityManager = levelManager.EntityManager;
                levelBlueprint = levelManager.CurrentLevelBlueprint;
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindPlayerCharacter();
            }

            if (entityManager != null && spawnMonsterMethod == null)
            {
                spawnMonsterMethod = entityManager.GetType().GetMethod(
                    "SpawnMonster",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(int), typeof(Vector2), typeof(MonsterBlueprint), typeof(float) },
                    null
                );
            }

            if (debugLog)
            {
                if (entityManager == null)
                {
                    Debug.LogWarning(
                        "[TimedSpecialMonsterSpawner] EntityManager를 아직 찾지 못했습니다.\nLevelManager 연결을 확인하세요.",
                        this
                    );
                }

                if (levelBlueprint == null)
                {
                    Debug.LogWarning(
                        "[TimedSpecialMonsterSpawner] LevelBlueprint를 아직 찾지 못했습니다. LevelManager 초기화 순서를 확인하세요.",
                        this
                    );
                }

                if (playerCharacter == null)
                {
                    Debug.LogWarning(
                        "[TimedSpecialMonsterSpawner] Player Character를 아직 찾지 못했습니다. Player 태그 또는 Character 컴포넌트를 확인하세요.",
                        this
                    );
                }

                if (entityManager != null && spawnMonsterMethod == null)
                {
                    Debug.LogWarning(
                        "[TimedSpecialMonsterSpawner] EntityManager의 SpawnMonster 메서드를 찾지 못했습니다.\n함정 몬스터 위치 지정 스폰이 불가능합니다.",
                        this
                    );
                }
            }
        }

        private IEnumerator SpawnTimedEventRoutine(RuntimeTimedSpawnEvent runtimeEvent)
        {
            yield return WaitUntilMiniStageUnblocked();

            if (runtimeEvent == null || runtimeEvent.entry == null)
            {
                yield break;
            }

            TimedSpawnEntry entry = runtimeEvent.entry;

            if (!CanSpawnCommon(entry, out int resolvedPoolIndex))
            {
                yield break;
            }

            int count = Mathf.Max(1, runtimeEvent.spawnCount);

            if (debugLog)
            {
                string prefabName = levelBlueprint.monsters[resolvedPoolIndex].monstersPrefab != null
                    ? levelBlueprint.monsters[resolvedPoolIndex].monstersPrefab.name
                    : "NULL PREFAB";

                Debug.Log(
                    $"[TimedSpecialMonsterSpawner] Timed Spawn 시작 | " +
                    $"현재 시간: {FormatTime(elapsedTime)} | " +
                    $"예약 시간: {FormatTime(runtimeEvent.scheduledTime)} | " +
                    $"Memo: {runtimeEvent.runtimeMemo} | " +
                    $"ResolvedPoolIndex: {resolvedPoolIndex} | " +
                    $"Prefab: {prefabName} | " +
                    $"Blueprint: {entry.monsterBlueprint.name} | " +
                    $"Count: {count}",
                    this
                );
            }

            for (int i = 0; i < count; i++)
            {
                yield return WaitUntilMiniStageUnblocked();

                Monster monster = entityManager.SpawnMonsterRandomPosition(
                    resolvedPoolIndex,
                    entry.monsterBlueprint,
                    entry.hpBuff
                );

                if (debugLog)
                {
                    string monsterName = monster != null ? monster.name : "NULL";

                    Debug.Log(
                        $"[TimedSpecialMonsterSpawner] Timed Spawn 완료 " +
                        $"({i + 1}/{count}) | Monster: {monsterName} | Memo: {runtimeEvent.runtimeMemo}",
                        this
                    );
                }

                if (entry.intervalBetweenSpawns > 0f && i < count - 1)
                {
                    yield return WaitSecondsRespectingMiniStagePause(entry.intervalBetweenSpawns);
                }
            }
        }

        private void ActivateTrapField(RuntimeTrapFieldState state)
        {
            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[TimedSpecialMonsterSpawner] 미니 스테이지 진행 중이라 Trap Field 활성화를 막았습니다.", this);
                }

                return;
            }

            if (state == null || state.entry == null)
            {
                return;
            }

            state.activated = true;

            TimedSpawnEntry entry = state.entry;

            if (debugLog)
            {
                Debug.Log(
                    $"[TimedSpecialMonsterSpawner] Trap Field 활성화 | " +
                    $"현재 시간: {FormatTime(elapsedTime)} | " +
                    $"Memo: {entry.memo} | " +
                    $"MaxActiveTraps: {entry.maxActiveTraps} | " +
                    $"RespawnDelay: {entry.respawnDelay}",
                    this
                );
            }

            if (!CanSpawnTrap(state))
            {
                Debug.LogError(
                    $"[TimedSpecialMonsterSpawner] Trap Field 활성화 실패 | Memo: {entry.memo}",
                    this
                );

                return;
            }

            if (!entry.spawnInitialTrapsOnActivate)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[TimedSpecialMonsterSpawner] Spawn Initial Traps On Activate가 꺼져 있어서 초기 함정을 스폰하지 않습니다.\nMemo: {entry.memo}",
                        this
                    );
                }

                return;
            }

            int targetCount = Mathf.Max(0, entry.maxActiveTraps);

            if (targetCount <= 0)
            {
                Debug.LogWarning(
                    $"[TimedSpecialMonsterSpawner] Max Active Traps가 0 이하라서 함정을 스폰하지 않습니다.\nMemo: {entry.memo}",
                    this
                );

                return;
            }

            for (int i = 0; i < targetCount; i++)
            {
                SpawnOneTrap(state);
            }
        }

        private bool CanSpawnTrap(RuntimeTrapFieldState state)
        {
            if (state == null || state.entry == null)
            {
                return false;
            }

            TimedSpawnEntry entry = state.entry;

            if (!CanSpawnCommon(entry, out int _))
            {
                return false;
            }

            if (spawnMonsterMethod == null)
            {
                ResolveReferences();

                if (spawnMonsterMethod == null)
                {
                    Debug.LogError(
                        "[TimedSpecialMonsterSpawner] EntityManager의 SpawnMonster 메서드를 찾지 못했습니다.\n함정 몬스터를 지정 위치에 스폰할 수 없습니다.",
                        this
                    );

                    return false;
                }
            }

            if (playerCharacter == null)
            {
                ResolveReferences();

                if (playerCharacter == null)
                {
                    Debug.LogError(
                        "[TimedSpecialMonsterSpawner] Player Character를 찾지 못했습니다. 함정 몬스터 위치 계산을 할 수 없습니다.",
                        this
                    );

                    return false;
                }
            }

            if (!(entry.monsterBlueprint is TrapMonsterBlueprint))
            {
                Debug.LogWarning(
                    $"[TimedSpecialMonsterSpawner] Trap Field Maintain 모드인데 Monster Blueprint가 TrapMonsterBlueprint 타입이 아닙니다. " +
                    $"그래도 스폰은 시도합니다.\nBlueprint: {entry.monsterBlueprint.name} | Memo: {entry.memo}",
                    this
                );
            }

            return true;
        }

        private void SpawnOneTrap(RuntimeTrapFieldState state)
        {
            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[TimedSpecialMonsterSpawner] 미니 스테이지 진행 중이라 함정 스폰을 막았습니다.", this);
                }

                return;
            }

            if (state == null || state.entry == null)
            {
                return;
            }

            TimedSpawnEntry entry = state.entry;

            if (!CanSpawnTrap(state))
            {
                return;
            }

            CleanupNullTraps(state);

            int maxActiveTraps = Mathf.Max(0, entry.maxActiveTraps);

            if (state.activeTraps.Count >= maxActiveTraps)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[TimedSpecialMonsterSpawner] 최대 함정 수에 도달해서 함정을 추가 스폰하지 않습니다.\n" +
                        $"현재 {state.activeTraps.Count}/{maxActiveTraps} | Memo: {entry.memo}",
                        this
                    );
                }

                return;
            }

            if (!TryResolvePoolIndex(entry, out int resolvedPoolIndex))
            {
                return;
            }

            Vector2 spawnPosition = GetRandomTrapSpawnPosition(state);

            if (debugLog)
            {
                Debug.Log(
                    $"[TimedSpecialMonsterSpawner] 함정 스폰 시도 | " +
                    $"PoolIndex: {resolvedPoolIndex} | " +
                    $"Position: {spawnPosition} | " +
                    $"Memo: {entry.memo}",
                    this
                );
            }

            Monster spawnedMonster = SpawnMonsterAtPosition(
                resolvedPoolIndex,
                spawnPosition,
                entry.monsterBlueprint,
                entry.hpBuff
            );

            if (spawnedMonster == null)
            {
                Debug.LogError(
                    $"[TimedSpecialMonsterSpawner] 함정 스폰 실패: SpawnMonster 결과가 NULL입니다.\n" +
                    $"PoolIndex와 Monster Blueprint 등록 상태를 확인하세요.\nMemo: {entry.memo}",
                    this
                );

                return;
            }

            state.activeTraps.Add(spawnedMonster);
            spawnedMonster.OnKilled.AddListener(OnAnyTrapKilled);

            if (debugLog)
            {
                Debug.Log(
                    $"[TimedSpecialMonsterSpawner] 함정 스폰 완료 | " +
                    $"Monster: {spawnedMonster.name} | " +
                    $"현재 함정 수 {state.activeTraps.Count}/{maxActiveTraps} | " +
                    $"위치 {spawnedMonster.transform.position} | " +
                    $"Memo: {entry.memo}",
                    spawnedMonster
                );
            }
        }

        private Monster SpawnMonsterAtPosition(
            int poolIndex,
            Vector2 spawnPosition,
            MonsterBlueprint monsterBlueprint,
            float hpBuff
        )
        {
            if (spawnMonsterMethod == null)
            {
                return null;
            }

            try
            {
                object result = spawnMonsterMethod.Invoke(
                    entityManager,
                    new object[] { poolIndex, spawnPosition, monsterBlueprint, hpBuff }
                );

                return result as Monster;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    "[TimedSpecialMonsterSpawner] SpawnMonster 호출 중 예외 발생\n" + exception,
                    this
                );

                return null;
            }
        }

        private void OnAnyTrapKilled(Monster killedTrap)
        {
            if (killedTrap != null)
            {
                killedTrap.OnKilled.RemoveListener(OnAnyTrapKilled);
            }

            for (int i = 0; i < runtimeTrapFieldStates.Count; i++)
            {
                RuntimeTrapFieldState state = runtimeTrapFieldStates[i];

                if (state == null)
                {
                    continue;
                }

                if (!state.activeTraps.Contains(killedTrap))
                {
                    continue;
                }

                state.activeTraps.Remove(killedTrap);
                CleanupNullTraps(state);

                TimedSpawnEntry entry = state.entry;

                if (debugLog)
                {
                    Debug.Log(
                        $"[TimedSpecialMonsterSpawner] 함정 사망 감지 | " +
                        $"현재 함정 수 {state.activeTraps.Count}/{Mathf.Max(0, entry.maxActiveTraps)} | " +
                        $"{entry.respawnDelay:0.##}초 뒤 재스폰 예약 | " +
                        $"Memo: {entry.memo}",
                        this
                    );
                }

                Coroutine coroutine = StartCoroutine(RespawnTrapAfterDelay(state));
                state.respawnCoroutines.Add(coroutine);

                return;
            }
        }

        private IEnumerator RespawnTrapAfterDelay(RuntimeTrapFieldState state)
        {
            if (state == null || state.entry == null)
            {
                yield break;
            }

            float delay = Mathf.Max(0f, state.entry.respawnDelay);

            yield return WaitSecondsRespectingMiniStagePause(delay);
            yield return WaitUntilMiniStageUnblocked();

            if (state == null || state.entry == null || !state.activated)
            {
                yield break;
            }

            if (IsMiniStageSpawnBlocked())
            {
                yield break;
            }

            CleanupNullTraps(state);

            if (state.activeTraps.Count < Mathf.Max(0, state.entry.maxActiveTraps))
            {
                SpawnOneTrap(state);
            }
        }

        private Vector2 GetRandomTrapSpawnPosition(RuntimeTrapFieldState state)
        {
            TimedSpawnEntry entry = state.entry;

            Vector2 playerPosition = playerCharacter != null
                ? (Vector2)playerCharacter.transform.position
                : Vector2.zero;

            float minDistance = Mathf.Max(0.1f, entry.minDistanceFromPlayer);
            float maxDistance = Mathf.Max(minDistance, entry.maxDistanceFromPlayer);

            Vector2 bestPosition = playerPosition + Vector2.right * minDistance;
            float bestDistanceScore = -1f;

            int tryCount = Mathf.Max(1, entry.spawnPositionTryCount);

            for (int i = 0; i < tryCount; i++)
            {
                Vector2 candidate = GetRandomPointAroundPlayer(playerPosition, minDistance, maxDistance);
                float nearestTrapDistance = GetNearestTrapDistance(state, candidate);

                if (nearestTrapDistance >= Mathf.Max(0f, entry.minDistanceBetweenTraps))
                {
                    return candidate;
                }

                if (nearestTrapDistance > bestDistanceScore)
                {
                    bestDistanceScore = nearestTrapDistance;
                    bestPosition = candidate;
                }
            }

            return bestPosition;
        }

        private Vector2 GetRandomPointAroundPlayer(Vector2 playerPosition, float minDistance, float maxDistance)
        {
            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

            if (direction == Vector2.zero)
            {
                direction = Vector2.right;
            }

            float distance = UnityEngine.Random.Range(minDistance, maxDistance);

            return playerPosition + direction * distance;
        }

        private float GetNearestTrapDistance(RuntimeTrapFieldState state, Vector2 position)
        {
            CleanupNullTraps(state);

            if (state.activeTraps.Count <= 0)
            {
                return float.MaxValue;
            }

            float nearestDistance = float.MaxValue;

            for (int i = 0; i < state.activeTraps.Count; i++)
            {
                Monster trap = state.activeTraps[i];

                if (trap == null)
                {
                    continue;
                }

                float distance = Vector2.Distance(position, trap.transform.position);

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                }
            }

            return nearestDistance;
        }

        private void CleanupNullTraps(RuntimeTrapFieldState state)
        {
            if (state == null)
            {
                return;
            }

            for (int i = state.activeTraps.Count - 1; i >= 0; i--)
            {
                Monster trap = state.activeTraps[i];

                if (trap == null || !trap.gameObject.activeInHierarchy)
                {
                    state.activeTraps.RemoveAt(i);
                }
            }
        }

        private bool CanSpawnCommon(TimedSpawnEntry entry, out int resolvedPoolIndex)
        {
            resolvedPoolIndex = -1;

            if (entityManager == null || levelBlueprint == null)
            {
                ResolveReferences();

                if (entityManager == null || levelBlueprint == null)
                {
                    Debug.LogError(
                        "[TimedSpecialMonsterSpawner] EntityManager 또는 LevelBlueprint가 없어 몬스터를 스폰할 수 없습니다.",
                        this
                    );

                    return false;
                }
            }

            if (entry == null)
            {
                Debug.LogError("[TimedSpecialMonsterSpawner] Spawn Entry가 NULL입니다.", this);
                return false;
            }

            if (entry.monsterBlueprint == null)
            {
                Debug.LogError(
                    $"[TimedSpecialMonsterSpawner] Monster Blueprint가 비어 있습니다.\nMemo: {entry.memo}",
                    this
                );

                return false;
            }

            if (!TryResolvePoolIndex(entry, out resolvedPoolIndex))
            {
                return false;
            }

            return true;
        }

        private bool TryResolvePoolIndex(TimedSpawnEntry entry, out int resolvedPoolIndex)
        {
            resolvedPoolIndex = entry.monsterPoolIndex;

            if (entry.autoResolvePoolIndex)
            {
                if (!TryFindPoolIndexByBlueprint(entry.monsterBlueprint, out resolvedPoolIndex))
                {
                    Debug.LogError(
                        $"[TimedSpecialMonsterSpawner] 자동 poolIndex 탐색 실패 | " +
                        $"Blueprint={entry.monsterBlueprint.name} | Memo={entry.memo}\n" +
                        "Level Blueprint의 Monster Settings에 해당 Blueprint가 등록되어 있는지 확인하세요.",
                        this
                    );

                    return false;
                }
            }

            if (!IsValidPoolIndex(resolvedPoolIndex))
            {
                Debug.LogError(
                    $"[TimedSpecialMonsterSpawner] 잘못된 poolIndex입니다.\n" +
                    $"PoolIndex={resolvedPoolIndex} | " +
                    $"Blueprint={entry.monsterBlueprint.name} | " +
                    $"Memo={entry.memo}",
                    this
                );

                return false;
            }

            return true;
        }

        private bool TryFindPoolIndexByBlueprint(MonsterBlueprint targetBlueprint, out int poolIndex)
        {
            poolIndex = -1;

            if (levelBlueprint == null || levelBlueprint.monsters == null || targetBlueprint == null)
            {
                return false;
            }

            for (int i = 0; i < levelBlueprint.monsters.Length; i++)
            {
                LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[i];

                if (container == null || container.monsterBlueprints == null)
                {
                    continue;
                }

                for (int j = 0; j < container.monsterBlueprints.Length; j++)
                {
                    if (container.monsterBlueprints[j] == targetBlueprint)
                    {
                        poolIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsValidPoolIndex(int poolIndex)
        {
            if (levelBlueprint == null || levelBlueprint.monsters == null)
            {
                return false;
            }

            if (poolIndex < 0 || poolIndex >= levelBlueprint.monsters.Length)
            {
                return false;
            }

            LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

            if (container == null || container.monstersPrefab == null)
            {
                return false;
            }

            return true;
        }

        private int GetTimedEventCount(TimedSpawnEntry entry)
        {
            if (entry.spawnTimeMode == SpawnTimeMode.RandomTimeWindow)
            {
                return Mathf.Max(1, entry.randomEventCount);
            }

            return 1;
        }

        private float GetScheduledTime(TimedSpawnEntry entry)
        {
            if (entry.spawnTimeMode == SpawnTimeMode.ExactTime)
            {
                return Mathf.Max(0f, entry.spawnTimeSeconds);
            }

            float startTime = Mathf.Max(0f, entry.randomStartTimeSeconds);
            float endTime = Mathf.Max(0f, entry.randomEndTimeSeconds);

            if (endTime < startTime)
            {
                float temp = startTime;
                startTime = endTime;
                endTime = temp;
            }

            if (Mathf.Approximately(startTime, endTime))
            {
                return startTime;
            }

            return UnityEngine.Random.Range(startTime, endTime);
        }

        private int GetSpawnCount(TimedSpawnEntry entry)
        {
            if (entry.spawnCountMode == SpawnCountMode.FixedCount)
            {
                return Mathf.Max(1, entry.spawnCount);
            }

            int minCount = Mathf.Max(1, entry.randomMinSpawnCount);
            int maxCount = Mathf.Max(1, entry.randomMaxSpawnCount);

            if (maxCount < minCount)
            {
                int temp = minCount;
                minCount = maxCount;
                maxCount = temp;
            }

            return UnityEngine.Random.Range(minCount, maxCount + 1);
        }

        private float GetTrapActivateTime(TimedSpawnEntry entry)
        {
            if (entry == null)
            {
                return 0f;
            }

            if (entry.trapSpawnOnStart)
            {
                return 0f;
            }

            return Mathf.Max(0f, entry.trapActivateTimeSeconds);
        }

        private string MakeRuntimeMemo(TimedSpawnEntry entry, int entryIndex, int eventIndex, int eventCount)
        {
            string baseMemo = string.IsNullOrEmpty(entry.memo)
                ? $"Entry {entryIndex}"
                : entry.memo;

            if (eventCount <= 1)
            {
                return baseMemo;
            }

            return $"{baseMemo} ({eventIndex + 1}/{eventCount})";
        }

        private Character FindPlayerCharacter()
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
            {
                Character character = playerObject.GetComponent<Character>();

                if (character != null)
                {
                    return character;
                }

                character = playerObject.GetComponentInParent<Character>();

                if (character != null)
                {
                    return character;
                }
            }

            return FindObjectOfType<Character>();
        }

        private string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int remainSeconds = totalSeconds % 60;

            return $"{minutes:00}:{remainSeconds:00}";
        }

        private bool IsMiniStageSpawnBlocked()
        {
            if (MiniStageRuntimeState.IsInsideMiniStage)
            {
                return true;
            }

            if (!blockWhileRunFlowPaused)
            {
                return false;
            }

            if (levelManager == null)
            {
                ResolveReferences();
            }

            return levelManager != null && levelManager.IsRunFlowPaused;
        }

        private IEnumerator WaitUntilMiniStageUnblocked()
        {
            while (IsMiniStageSpawnBlocked())
            {
                yield return null;
            }
        }

        private IEnumerator WaitSecondsRespectingMiniStagePause(float seconds)
        {
            float timer = 0f;
            float targetTime = Mathf.Max(0f, seconds);

            while (timer < targetTime)
            {
                if (!IsMiniStageSpawnBlocked())
                {
                    timer += Time.deltaTime;
                }

                yield return null;
            }
        }

        private void RemoveAllTrapListeners()
        {
            for (int i = 0; i < runtimeTrapFieldStates.Count; i++)
            {
                RuntimeTrapFieldState state = runtimeTrapFieldStates[i];

                if (state == null)
                {
                    continue;
                }

                for (int j = 0; j < state.activeTraps.Count; j++)
                {
                    Monster trap = state.activeTraps[j];

                    if (trap != null)
                    {
                        trap.OnKilled.RemoveListener(OnAnyTrapKilled);
                    }
                }

                state.activeTraps.Clear();
                state.respawnCoroutines.Clear();
            }
        }

        private void OnDisable()
        {
            RemoveAllTrapListeners();
            StopAllCoroutines();
        }

        private void OnDestroy()
        {
            RemoveAllTrapListeners();
        }
    }
}
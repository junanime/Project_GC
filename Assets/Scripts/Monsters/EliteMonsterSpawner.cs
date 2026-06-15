using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // 엘리트 몬스터 전용 스포너
    //
    // 핵심 구조:
    // 1. ManualFlatIndexPool 모드
    // - 기존 방식처럼 Elite Monster Flat Indices에서 랜덤으로 엘리트 선택
    //
    // 2. FollowNormalSpawnTable 모드
    // - 현재 Level 1의 일반 몬스터 스폰 테이블에서 먼저 일반 몬스터 flat index를 뽑음
    // - Normal To Elite Mappings에 등록된 대응 관계를 보고 엘리트 flat index로 변환
    // - 결과적으로 "현재 시간대에 많이 나오는 일반 몬스터의 엘리트"가 더 자주 등장함
    public class EliteMonsterSpawner : MonoBehaviour
    {
        public enum EliteSelectionMode
        {
            FollowNormalSpawnTable,
            ManualFlatIndexPool
        }

        [System.Serializable]
        public class NormalToEliteMapping
        {
            [Tooltip("일반 몬스터 flat index입니다. 예: 初級小兵 = 0")]
            public int normalMonsterFlatIndex;

            [Tooltip("위 일반 몬스터에 대응하는 엘리트 몬스터 flat index입니다. 예: Elite 初級小兵 = 8")]
            public int eliteMonsterFlatIndex;

            [Tooltip("체크 해제하면 이 매핑은 무시됩니다.")]
            public bool enabled = true;
        }

        [System.Serializable]
        public class EliteSpawnPhase
        {
            [Header("Phase Info")]
            [Tooltip("인스펙터에서 구분하기 위한 이름입니다. 예: 2분대 엘리트")]
            public string phaseName = "Elite Phase";

            [Tooltip("이 시간부터 엘리트 스폰을 시작합니다. 초 단위입니다.")]
            public float startTime = 120f;

            [Tooltip("이 시간 이후에는 이 Phase가 작동하지 않습니다. 0 이하로 두면 종료 시간 없이 계속 작동합니다.")]
            public float endTime = 210f;

            [Header("Selection Mode")]
            [Tooltip("엘리트 선택 방식입니다. FollowNormalSpawnTable을 추천합니다.")]
            public EliteSelectionMode selectionMode = EliteSelectionMode.FollowNormalSpawnTable;

            [Header("Spawn Timing")]
            [Tooltip("몇 초마다 엘리트를 스폰할지 설정합니다.")]
            public float spawnInterval = 30f;

            [Tooltip("Phase가 처음 시작되는 순간 바로 한 번 스폰할지 여부입니다.")]
            public bool spawnImmediatelyOnPhaseStart = true;

            [Header("Spawn Count")]
            [Tooltip("한 번 스폰될 때 최소 몇 마리 스폰할지 설정합니다.")]
            public int minSpawnCount = 1;

            [Tooltip("한 번 스폰될 때 최대 몇 마리 스폰할지 설정합니다.")]
            public int maxSpawnCount = 1;

            [Tooltip("이 Phase 동안 필드에 동시에 존재할 수 있는 엘리트 최대 수입니다.")]
            public int maxAliveInPhase = 1;

            [Header("Follow Normal Spawn Table Mode")]
            [Tooltip("일반 몬스터 flat index -> 엘리트 몬스터 flat index 대응표입니다.")]
            public NormalToEliteMapping[] normalToEliteMappings;

            [Tooltip("일반 스폰 테이블에서 매핑 가능한 몬스터가 나올 때까지 몇 번까지 다시 뽑을지 설정합니다.")]
            public int maxNormalTableSampleAttempts = 30;

            [Tooltip("한 웨이브 안에서 같은 엘리트가 중복 선택되지 않도록 시도합니다.")]
            public bool avoidDuplicateEliteInOneWave = true;

            [Tooltip("현재 시간대 스폰 테이블에서 매핑 가능한 몬스터를 못 찾았을 때, Manual Pool을 예비 후보로 사용할지 여부입니다.")]
            public bool fallbackToManualPoolIfNoMappedElite = false;

            [Header("Manual Pool Mode / Fallback Pool")]
            [Tooltip("ManualFlatIndexPool 모드에서 사용할 엘리트 flat index 목록입니다. Follow 모드에서는 fallback 용도로도 쓸 수 있습니다.")]
            public int[] eliteMonsterFlatIndices;

            [Header("Extra HP Buff")]
            [Tooltip("추가 HP 보정입니다. 보통 0으로 두면 됩니다. 엘리트 체력 2배는 EliteMonsterBlueprint의 HP Multiplier에서 처리합니다.")]
            public float additionalHpBuff = 0f;
        }

        private struct FlatMonsterEntry
        {
            public int flatIndex;
            public int poolIndex;
            public int blueprintIndex;
            public MonsterBlueprint blueprint;
            public GameObject prefab;
        }

        [Header("References")]
        [Tooltip("씬의 LevelManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Header("Global Settings")]
        [SerializeField] private bool spawnEnabled = true;

        [Tooltip("전체 Phase를 통틀어 필드에 동시에 존재할 수 있는 엘리트 최대 수입니다.")]
        [SerializeField] private int globalMaxAliveElites = 8;

        [Tooltip("체크하면 EliteMonsterBlueprint가 아닌 몬스터 인덱스는 스폰하지 않고 경고를 출력합니다.")]
        [SerializeField] private bool requireEliteMonsterBlueprint = true;

        [Tooltip("스폰된 엘리트가 죽거나 비활성화되었는지 매 프레임 정리합니다.")]
        [SerializeField] private bool cleanupInactiveElitesEveryFrame = true;

        [Header("Mini Stage Guard")]
        [Tooltip("미니 스테이지 진행 중에는 필드 엘리트 몬스터 스폰을 멈춥니다.")]
        [SerializeField] private bool blockWhileRunFlowPaused = true;

        [Tooltip("미니 스테이지 때문에 엘리트 스폰이 차단될 때 로그를 출력합니다.")]
        [SerializeField] private bool logMiniStageBlock = false;

        [Header("Spawn Phases")]
        [SerializeField] private EliteSpawnPhase[] spawnPhases;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("게임 시작 시 Level 1 Blueprint의 몬스터 flat index 목록을 Console에 출력합니다.")]
        [SerializeField] private bool logMonsterIndexTableOnStart = true;

        [Tooltip("FollowNormalSpawnTable에서 어떤 일반 몬스터가 어떤 엘리트로 변환됐는지 로그를 출력합니다.")]
        [SerializeField] private bool debugFollowSelection = true;

        private readonly List<Monster> activeElites = new List<Monster>();

        private float[] phaseTimers;
        private bool[] phaseStarted;
        private bool ready = false;

        private void Awake()
        {
            EnsureRuntimeArrays();

            if (debugLog)
            {
                Debug.Log("[EliteMonsterSpawner] Awake 호출됨 - 컴포넌트 활성 상태 확인 완료", this);
            }
        }

        private IEnumerator Start()
        {
            ResolveReferences();

            // LevelManager.Start()에서 EntityManager.Init()이 끝날 시간을 준다.
            yield return null;
            yield return null;

            ResolveReferences();
            EnsureRuntimeArrays();

            ready = levelManager != null &&
                    levelManager.EntityManager != null &&
                    levelManager.CurrentLevelBlueprint != null;

            if (logMonsterIndexTableOnStart)
            {
                LogMonsterIndexTable();
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[EliteMonsterSpawner] Start 준비 완료 | " +
                    $"Ready: {ready} | " +
                    $"LevelManager: {levelManager != null} | " +
                    $"EntityManager: {(levelManager != null && levelManager.EntityManager != null)} | " +
                    $"LevelBlueprint: {(levelManager != null && levelManager.CurrentLevelBlueprint != null)} | " +
                    $"Phase Count: {(spawnPhases != null ? spawnPhases.Length : 0)}",
                    this
                );
            }
        }

        private void Update()
        {
            if (!spawnEnabled)
            {
                return;
            }

            ResolveReferences();

            if (levelManager == null ||
                levelManager.EntityManager == null ||
                levelManager.CurrentLevelBlueprint == null)
            {
                return;
            }

            ready = true;

            EnsureRuntimeArrays();

            if (cleanupInactiveElitesEveryFrame)
            {
                CleanupActiveElites();
            }

            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[EliteMonsterSpawner] 미니 스테이지 진행 중이라 엘리트 스폰 타이머를 멈춥니다.", this);
                }

                return;
            }

            float currentTime = levelManager.CurrentLevelTime;

            if (spawnPhases == null || spawnPhases.Length == 0)
            {
                return;
            }

            for (int i = 0; i < spawnPhases.Length; i++)
            {
                EliteSpawnPhase phase = spawnPhases[i];

                if (phase == null)
                {
                    continue;
                }

                if (!IsPhaseActive(phase, currentTime))
                {
                    continue;
                }

                if (!phaseStarted[i])
                {
                    phaseStarted[i] = true;
                    phaseTimers[i] = 0f;

                    if (debugLog)
                    {
                        Debug.Log(
                            $"[EliteMonsterSpawner] Phase 시작 | " +
                            $"Phase: {phase.phaseName} | Time: {currentTime:0.##} | Mode: {phase.selectionMode}",
                            this
                        );
                    }

                    if (phase.spawnImmediatelyOnPhaseStart)
                    {
                        TrySpawnFromPhase(phase);
                    }
                }

                phaseTimers[i] += Time.deltaTime;

                float interval = Mathf.Max(0.05f, phase.spawnInterval);

                if (phaseTimers[i] >= interval)
                {
                    TrySpawnFromPhase(phase);
                    phaseTimers[i] = Mathf.Repeat(phaseTimers[i], interval);
                }
            }
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }
        }

        private void EnsureRuntimeArrays()
        {
            int count = spawnPhases != null ? spawnPhases.Length : 0;

            if (phaseTimers == null || phaseTimers.Length != count)
            {
                phaseTimers = new float[count];
            }

            if (phaseStarted == null || phaseStarted.Length != count)
            {
                phaseStarted = new bool[count];
            }
        }

        private bool IsPhaseActive(EliteSpawnPhase phase, float currentTime)
        {
            if (currentTime < phase.startTime)
            {
                return false;
            }

            if (phase.endTime > 0f && currentTime >= phase.endTime)
            {
                return false;
            }

            return true;
        }

        private void TrySpawnFromPhase(EliteSpawnPhase phase)
        {
            if (IsMiniStageSpawnBlocked())
            {
                if (logMiniStageBlock)
                {
                    Debug.Log("[EliteMonsterSpawner] 미니 스테이지 진행 중이라 엘리트 즉시 스폰을 막았습니다.", this);
                }

                return;
            }

            if (!ready)
            {
                if (debugLog)
                {
                    Debug.LogWarning("[EliteMonsterSpawner] 아직 준비되지 않아 엘리트를 스폰하지 않습니다.", this);
                }

                return;
            }

            CleanupActiveElites();

            int currentAlive = activeElites.Count;
            int globalCapacity = Mathf.Max(0, globalMaxAliveElites - currentAlive);
            int phaseCapacity = Mathf.Max(0, phase.maxAliveInPhase - currentAlive);
            int finalCapacity = Mathf.Min(globalCapacity, phaseCapacity);

            if (finalCapacity <= 0)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[EliteMonsterSpawner] 스폰 생략 | Phase: {phase.phaseName} | " +
                        $"GlobalCapacity: {globalCapacity} | PhaseCapacity: {phaseCapacity} | Active: {currentAlive}",
                        this
                    );
                }

                return;
            }

            int minCount = Mathf.Max(1, phase.minSpawnCount);
            int maxCount = Mathf.Max(minCount, phase.maxSpawnCount);
            int spawnCount = Random.Range(minCount, maxCount + 1);
            spawnCount = Mathf.Min(spawnCount, finalCapacity);

            HashSet<int> selectedEliteIndicesThisWave = new HashSet<int>();
            int spawnedCount = 0;

            for (int i = 0; i < spawnCount; i++)
            {
                if (IsMiniStageSpawnBlocked())
                {
                    return;
                }

                int eliteFlatIndex;

                if (!TrySelectEliteFlatIndex(phase, selectedEliteIndicesThisWave, out eliteFlatIndex))
                {
                    if (debugLog)
                    {
                        Debug.LogWarning(
                            $"[EliteMonsterSpawner] 엘리트 후보 선택 실패 | Phase: {phase.phaseName}",
                            this
                        );
                    }

                    continue;
                }

                selectedEliteIndicesThisWave.Add(eliteFlatIndex);

                Monster spawnedElite = TrySpawnEliteByFlatIndex(
                    eliteFlatIndex,
                    phase.additionalHpBuff
                );

                if (spawnedElite != null)
                {
                    spawnedCount++;
                }
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[EliteMonsterSpawner] 엘리트 웨이브 스폰 결과 | " +
                    $"Phase: {phase.phaseName} | Spawned: {spawnedCount}/{spawnCount} | Active: {activeElites.Count}",
                    this
                );
            }
        }

        private bool TrySelectEliteFlatIndex(
            EliteSpawnPhase phase,
            HashSet<int> selectedEliteIndicesThisWave,
            out int eliteFlatIndex)
        {
            eliteFlatIndex = -1;

            if (phase.selectionMode == EliteSelectionMode.ManualFlatIndexPool)
            {
                return TrySelectManualEliteFlatIndex(
                    phase,
                    selectedEliteIndicesThisWave,
                    out eliteFlatIndex
                );
            }

            bool selectedFromNormalTable = TrySelectMappedEliteFollowingNormalSpawnTable(
                phase,
                selectedEliteIndicesThisWave,
                out eliteFlatIndex
            );

            if (selectedFromNormalTable)
            {
                return true;
            }

            if (phase.fallbackToManualPoolIfNoMappedElite)
            {
                return TrySelectManualEliteFlatIndex(
                    phase,
                    selectedEliteIndicesThisWave,
                    out eliteFlatIndex
                );
            }

            return false;
        }

        private bool TrySelectMappedEliteFollowingNormalSpawnTable(
            EliteSpawnPhase phase,
            HashSet<int> selectedEliteIndicesThisWave,
            out int eliteFlatIndex)
        {
            eliteFlatIndex = -1;

            if (levelManager == null ||
                levelManager.CurrentLevelBlueprint == null ||
                levelManager.CurrentLevelBlueprint.monsterSpawnTable == null)
            {
                return false;
            }

            if (phase.normalToEliteMappings == null ||
                phase.normalToEliteMappings.Length == 0)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        $"[EliteMonsterSpawner] {phase.phaseName}: Normal To Elite Mappings가 비어 있습니다.",
                        this
                    );
                }

                return false;
            }

            float normalizedTime = levelManager.GetNormalizedLevelTime();
            int attempts = Mathf.Max(1, phase.maxNormalTableSampleAttempts);

            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int normalFlatIndex =
                    levelManager.CurrentLevelBlueprint.monsterSpawnTable.SelectMonster(normalizedTime);

                if (normalFlatIndex < 0)
                {
                    continue;
                }

                int mappedEliteFlatIndex;

                if (!TryGetMappedEliteFlatIndex(phase, normalFlatIndex, out mappedEliteFlatIndex))
                {
                    continue;
                }

                if (phase.avoidDuplicateEliteInOneWave &&
                    selectedEliteIndicesThisWave != null &&
                    selectedEliteIndicesThisWave.Contains(mappedEliteFlatIndex))
                {
                    continue;
                }

                eliteFlatIndex = mappedEliteFlatIndex;

                if (debugFollowSelection)
                {
                    Debug.Log(
                        $"[EliteMonsterSpawner] 일반 스폰 테이블 추종 선택 | " +
                        $"normalFlatIndex={normalFlatIndex} -> eliteFlatIndex={eliteFlatIndex} | " +
                        $"normalizedTime={normalizedTime:0.###}",
                        this
                    );
                }

                return true;
            }

            if (debugLog)
            {
                Debug.LogWarning(
                    $"[EliteMonsterSpawner] 현재 일반 스폰 테이블에서 매핑 가능한 엘리트를 찾지 못했습니다. " +
                    $"Phase={phase.phaseName} | Attempts={attempts}",
                    this
                );
            }

            return false;
        }

        private bool TryGetMappedEliteFlatIndex(
            EliteSpawnPhase phase,
            int normalFlatIndex,
            out int eliteFlatIndex)
        {
            eliteFlatIndex = -1;

            if (phase.normalToEliteMappings == null)
            {
                return false;
            }

            for (int i = 0; i < phase.normalToEliteMappings.Length; i++)
            {
                NormalToEliteMapping mapping = phase.normalToEliteMappings[i];

                if (mapping == null || !mapping.enabled)
                {
                    continue;
                }

                if (mapping.normalMonsterFlatIndex != normalFlatIndex)
                {
                    continue;
                }

                eliteFlatIndex = mapping.eliteMonsterFlatIndex;
                return true;
            }

            return false;
        }

        private bool TrySelectManualEliteFlatIndex(
            EliteSpawnPhase phase,
            HashSet<int> selectedEliteIndicesThisWave,
            out int eliteFlatIndex)
        {
            eliteFlatIndex = -1;

            if (phase.eliteMonsterFlatIndices == null ||
                phase.eliteMonsterFlatIndices.Length == 0)
            {
                return false;
            }

            List<int> candidates = new List<int>();

            for (int i = 0; i < phase.eliteMonsterFlatIndices.Length; i++)
            {
                int candidate = phase.eliteMonsterFlatIndices[i];

                if (phase.avoidDuplicateEliteInOneWave &&
                    selectedEliteIndicesThisWave != null &&
                    selectedEliteIndicesThisWave.Contains(candidate))
                {
                    continue;
                }

                candidates.Add(candidate);
            }

            if (candidates.Count <= 0)
            {
                for (int i = 0; i < phase.eliteMonsterFlatIndices.Length; i++)
                {
                    candidates.Add(phase.eliteMonsterFlatIndices[i]);
                }
            }

            if (candidates.Count <= 0)
            {
                return false;
            }

            eliteFlatIndex = candidates[Random.Range(0, candidates.Count)];
            return true;
        }

        private Monster TrySpawnEliteByFlatIndex(int flatIndex, float additionalHpBuff)
        {
            if (IsMiniStageSpawnBlocked())
            {
                return null;
            }

            FlatMonsterEntry entry;

            if (!TryGetFlatMonsterEntry(flatIndex, out entry))
            {
                Debug.LogWarning($"[EliteMonsterSpawner] 잘못된 flat index입니다: {flatIndex}", this);
                return null;
            }

            if (entry.blueprint == null)
            {
                Debug.LogWarning($"[EliteMonsterSpawner] MonsterBlueprint가 비어 있습니다. flatIndex={flatIndex}", this);
                return null;
            }

            if (requireEliteMonsterBlueprint && !(entry.blueprint is EliteMonsterBlueprint))
            {
                Debug.LogWarning(
                    $"[EliteMonsterSpawner] flatIndex={flatIndex}는 EliteMonsterBlueprint가 아닙니다. " +
                    $"name={entry.blueprint.name}, type={entry.blueprint.GetType().Name}. 스폰하지 않습니다.",
                    this
                );

                return null;
            }

            if (!ValidatePrefabAndBlueprintCompatibility(entry))
            {
                return null;
            }

            Monster spawnedMonster = levelManager.EntityManager.SpawnMonsterRandomPosition(
                entry.poolIndex,
                entry.blueprint,
                additionalHpBuff
            );

            if (spawnedMonster == null)
            {
                Debug.LogWarning($"[EliteMonsterSpawner] 엘리트 스폰 실패. flatIndex={flatIndex}", this);
                return null;
            }

            activeElites.Add(spawnedMonster);
            spawnedMonster.OnKilled.AddListener(OnEliteKilled);

            if (debugLog)
            {
                Debug.Log(
                    $"[EliteMonsterSpawner] 엘리트 스폰 완료 | " +
                    $"flatIndex={flatIndex} | poolIndex={entry.poolIndex} | blueprintIndex={entry.blueprintIndex} | " +
                    $"name={entry.blueprint.name} | active={activeElites.Count}",
                    this
                );
            }

            return spawnedMonster;
        }

        private bool ValidatePrefabAndBlueprintCompatibility(FlatMonsterEntry entry)
        {
            if (entry.prefab == null)
            {
                Debug.LogWarning(
                    $"[EliteMonsterSpawner] flatIndex={entry.flatIndex}의 Monsters Prefab이 비어 있습니다.",
                    this
                );

                return false;
            }

            Monster prefabMonster = entry.prefab.GetComponent<Monster>();

            if (prefabMonster == null)
            {
                Debug.LogWarning(
                    $"[EliteMonsterSpawner] flatIndex={entry.flatIndex}의 프리팹에 Monster 컴포넌트가 없습니다. " +
                    $"Prefab={entry.prefab.name}",
                    this
                );

                return false;
            }

            if (entry.blueprint is MeleeMonsterBlueprint && !(prefabMonster is MeleeMonster))
            {
                Debug.LogWarning(
                    $"[EliteMonsterSpawner] 프리팹/블루프린트 타입이 맞지 않습니다. " +
                    $"flatIndex={entry.flatIndex} | Prefab={entry.prefab.name}({prefabMonster.GetType().Name}) | " +
                    $"Blueprint={entry.blueprint.name}({entry.blueprint.GetType().Name}) | " +
                    $"MeleeMonsterBlueprint 계열은 MeleeMonster 프리팹에 넣어야 합니다.",
                    this
                );

                return false;
            }

            if (prefabMonster is BossMonster)
            {
                Debug.LogWarning(
                    $"[EliteMonsterSpawner] 보스 프리팹은 엘리트 몬스터 풀로 사용할 수 없습니다. " +
                    $"flatIndex={entry.flatIndex} | Prefab={entry.prefab.name}",
                    this
                );

                return false;
            }

            return true;
        }

        private bool TryGetFlatMonsterEntry(int targetFlatIndex, out FlatMonsterEntry result)
        {
            result = default;

            if (levelManager == null || levelManager.CurrentLevelBlueprint == null)
            {
                return false;
            }

            LevelBlueprint levelBlueprint = levelManager.CurrentLevelBlueprint;

            if (levelBlueprint.monsters == null)
            {
                return false;
            }

            int flatIndex = 0;

            for (int poolIndex = 0; poolIndex < levelBlueprint.monsters.Length; poolIndex++)
            {
                LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

                if (container == null || container.monsterBlueprints == null)
                {
                    continue;
                }

                for (int blueprintIndex = 0; blueprintIndex < container.monsterBlueprints.Length; blueprintIndex++)
                {
                    MonsterBlueprint blueprint = container.monsterBlueprints[blueprintIndex];

                    if (flatIndex == targetFlatIndex)
                    {
                        result = new FlatMonsterEntry
                        {
                            flatIndex = flatIndex,
                            poolIndex = poolIndex,
                            blueprintIndex = blueprintIndex,
                            blueprint = blueprint,
                            prefab = container.monstersPrefab
                        };

                        return true;
                    }

                    flatIndex++;
                }
            }

            return false;
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

        private void OnEliteKilled(Monster killedMonster)
        {
            if (killedMonster != null)
            {
                killedMonster.OnKilled.RemoveListener(OnEliteKilled);
            }

            activeElites.Remove(killedMonster);

            if (debugLog)
            {
                Debug.Log(
                    $"[EliteMonsterSpawner] 엘리트 사망 감지 | Active: {activeElites.Count}",
                    this
                );
            }
        }

        private void CleanupActiveElites()
        {
            for (int i = activeElites.Count - 1; i >= 0; i--)
            {
                Monster monster = activeElites[i];

                if (monster == null || !monster.gameObject.activeInHierarchy || monster.HP <= 0f)
                {
                    if (monster != null)
                    {
                        monster.OnKilled.RemoveListener(OnEliteKilled);
                    }

                    activeElites.RemoveAt(i);
                }
            }
        }

        private void LogMonsterIndexTable()
        {
            if (levelManager == null || levelManager.CurrentLevelBlueprint == null)
            {
                Debug.LogWarning(
                    "[EliteMonsterSpawner] Monster Index Table 출력 실패: LevelManager 또는 LevelBlueprint가 없습니다.",
                    this
                );

                return;
            }

            LevelBlueprint levelBlueprint = levelManager.CurrentLevelBlueprint;

            if (levelBlueprint.monsters == null)
            {
                Debug.LogWarning("[EliteMonsterSpawner] LevelBlueprint.monsters가 비어 있습니다.", this);
                return;
            }

            Debug.Log("[EliteMonsterSpawner] ===== Monster Flat Index Table Start =====", this);

            int flatIndex = 0;

            for (int poolIndex = 0; poolIndex < levelBlueprint.monsters.Length; poolIndex++)
            {
                LevelBlueprint.MonstersContainer container = levelBlueprint.monsters[poolIndex];

                if (container == null || container.monsterBlueprints == null)
                {
                    continue;
                }

                string prefabName = container.monstersPrefab != null
                    ? container.monstersPrefab.name
                    : "NULL_PREFAB";

                for (int blueprintIndex = 0; blueprintIndex < container.monsterBlueprints.Length; blueprintIndex++)
                {
                    MonsterBlueprint blueprint = container.monsterBlueprints[blueprintIndex];

                    string blueprintName = blueprint != null ? blueprint.name : "NULL";
                    string blueprintType = blueprint != null ? blueprint.GetType().Name : "NULL";
                    bool isElite = blueprint is EliteMonsterBlueprint;

                    Debug.Log(
                        $"[EliteIndex] flatIndex={flatIndex} | poolIndex={poolIndex} | " +
                        $"blueprintIndex={blueprintIndex} | prefab={prefabName} | " +
                        $"name={blueprintName} | type={blueprintType} | elite={isElite}",
                        this
                    );

                    flatIndex++;
                }
            }

            Debug.Log("[EliteMonsterSpawner] ===== Monster Flat Index Table End =====", this);
        }

        private void OnDisable()
        {
            for (int i = activeElites.Count - 1; i >= 0; i--)
            {
                if (activeElites[i] != null)
                {
                    activeElites[i].OnKilled.RemoveListener(OnEliteKilled);
                }
            }

            activeElites.Clear();
        }
    }
}
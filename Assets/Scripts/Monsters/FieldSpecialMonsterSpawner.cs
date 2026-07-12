using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 영양 도둑균, 추격형 보물 몬스터 같은 독립 필드 특수 몬스터를 생성하는 스포너입니다.
    ///
    /// 기존 일반 몬스터 풀을 건드리지 않고 Instantiate 방식으로 생성합니다.
    /// 미니 스테이지 진입 중이거나 런 흐름이 멈춘 상태에서는 스폰하지 않습니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class FieldSpecialMonsterSpawner : MonoBehaviour
    {
        [Serializable]
        private class SpawnEntry
        {
            [Tooltip("스폰할 필드 특수 몬스터 프리팹입니다.")]
            public FieldSpecialMonsterBase prefab;

            [Tooltip("게임 시작 후 첫 생성까지 걸리는 시간입니다.")]
            public float firstSpawnTime = 40f;

            [Tooltip("첫 생성 이후 재생성 간격입니다.")]
            public float spawnInterval = 60f;

            [Tooltip("동시에 필드에 존재할 수 있는 최대 수입니다.")]
            public int maxAliveCount = 1;

            [Tooltip("플레이어 기준 생성 거리입니다.")]
            public float spawnDistanceFromPlayer = 9f;

            [Tooltip("생성 거리의 랜덤 오차입니다.")]
            public float spawnDistanceJitter = 1.5f;

            [Tooltip("이 항목의 스폰을 활성화할지 여부입니다.")]
            public bool enabled = true;

            [NonSerialized] public float timer;
            [NonSerialized] public bool hasSpawnedOnce;
            [NonSerialized] public float nextFailureLogTime;
            [NonSerialized] public readonly List<FieldSpecialMonsterBase> alive = new List<FieldSpecialMonsterBase>();
        }

        [Header("References")]
        [Tooltip("현재 스테이지의 LevelManager입니다. 비워두면 자동으로 찾습니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("EntityManager입니다. 비워두면 LevelManager에서 가져오거나 자동으로 찾습니다.")]
        [SerializeField] private EntityManager entityManager;

        [Tooltip("플레이어 캐릭터입니다. 비워두면 LevelManager에서 가져오거나 자동으로 찾습니다.")]
        [SerializeField] private Character playerCharacter;

        [Header("Spawn Entries")]
        [Tooltip("생성할 필드 특수 몬스터 목록입니다. 영양 도둑균과 보물 몬스터를 각각 등록하세요.")]
        [SerializeField] private List<SpawnEntry> spawnEntries = new List<SpawnEntry>();

        [Header("Blocking")]
        [Tooltip("LevelManager.IsRunFlowPaused가 true일 때 스폰 타이머를 멈춥니다.")]
        [SerializeField] private bool blockWhileRunFlowPaused = true;

        [Tooltip("MiniStageRuntimeState.IsInsideMiniStage가 true일 때 스폰하지 않습니다.")]
        [SerializeField] private bool blockInsideMiniStage = true;

        [Header("Debug")]
        [Tooltip("스폰 성공/실패/참조 누락 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        [Tooltip("미니 스테이지나 RunFlowPaused 때문에 스폰이 막힐 때 로그를 출력합니다.")]
        [SerializeField] private bool logBlockedState = false;

        [Tooltip("스폰 실패 로그가 너무 많이 찍히지 않도록 제한하는 간격입니다.")]
        [SerializeField] private float failureLogInterval = 1f;

        private float nextBlockedLogTime;

        private void Awake()
        {
            ResolveReferences();
            InitializeTimers();

            if (debugLog)
            {
                Debug.Log(
                    $"[FieldSpecialMonsterSpawner] Awake 완료 | " +
                    $"Entry Count: {(spawnEntries != null ? spawnEntries.Count : 0)}",
                    this);
            }
        }

        private IEnumerator Start()
        {
            // LevelManager / EntityManager / PlayerCharacter 초기화 순서 때문에
            // 스포너가 먼저 실행되면 참조가 비어 있을 수 있으므로 2프레임 대기 후 다시 찾습니다.
            yield return null;
            yield return null;

            ResolveReferences();

            if (debugLog)
            {
                Debug.Log(
                    $"[FieldSpecialMonsterSpawner] Start 참조 확인 | " +
                    $"LevelManager: {(levelManager != null ? levelManager.name : "NULL")} | " +
                    $"EntityManager: {(entityManager != null ? entityManager.name : "NULL")} | " +
                    $"Player: {(playerCharacter != null ? playerCharacter.name : "NULL")} | " +
                    $"Entry Count: {(spawnEntries != null ? spawnEntries.Count : 0)}",
                    this);
            }
        }

        private void Update()
        {
            ResolveReferences();

            if (ShouldBlockSpawn())
            {
                return;
            }

            if (spawnEntries == null || spawnEntries.Count <= 0)
            {
                if (debugLog && Time.time >= nextBlockedLogTime)
                {
                    nextBlockedLogTime = Time.time + Mathf.Max(0.5f, failureLogInterval);
                    Debug.LogWarning(
                        "[FieldSpecialMonsterSpawner] Spawn Entries가 비어 있습니다. " +
                        "Inspector에서 영양 도둑균/보물 몬스터 프리팹을 등록하세요.",
                        this);
                }

                return;
            }

            for (int i = 0; i < spawnEntries.Count; i++)
            {
                UpdateEntry(spawnEntries[i], i);
            }
        }

        /// <summary>
        /// FieldSpecialMonsterBase가 사망/소멸/Destroy될 때 호출하는 제거 알림입니다.
        /// alive 리스트에서 제거해야 다음 개체가 정상적으로 다시 스폰됩니다.
        /// </summary>
        public void NotifySpecialMonsterRemoved(FieldSpecialMonsterBase monster)
        {
            if (monster == null || spawnEntries == null)
            {
                return;
            }

            for (int i = 0; i < spawnEntries.Count; i++)
            {
                SpawnEntry entry = spawnEntries[i];

                if (entry == null)
                {
                    continue;
                }

                entry.alive.Remove(monster);
            }
        }

        private void InitializeTimers()
        {
            if (spawnEntries == null)
            {
                return;
            }

            for (int i = 0; i < spawnEntries.Count; i++)
            {
                SpawnEntry entry = spawnEntries[i];

                if (entry == null)
                {
                    continue;
                }

                entry.timer = 0f;
                entry.hasSpawnedOnce = false;
                entry.nextFailureLogTime = 0f;
                entry.alive.Clear();
            }
        }

        private void UpdateEntry(SpawnEntry entry, int entryIndex)
        {
            if (entry == null)
            {
                return;
            }

            if (!entry.enabled)
            {
                LogEntryFailureThrottled(
                    entry,
                    entryIndex,
                    "Entry가 비활성화되어 있습니다. Enabled를 체크하세요.");
                return;
            }

            if (entry.prefab == null)
            {
                LogEntryFailureThrottled(
                    entry,
                    entryIndex,
                    "Prefab이 비어 있습니다. 영양 도둑균/보물 몬스터 프리팹을 넣으세요.");
                return;
            }

            CleanupEntry(entry);

            int maxAlive = Mathf.Max(1, entry.maxAliveCount);

            if (entry.alive.Count >= maxAlive)
            {
                return;
            }

            entry.timer += Time.deltaTime;

            float requiredTime = entry.hasSpawnedOnce
                ? Mathf.Max(0.1f, entry.spawnInterval)
                : Mathf.Max(0f, entry.firstSpawnTime);

            if (entry.timer < requiredTime)
            {
                return;
            }

            bool spawned = TrySpawn(entry, entryIndex);

            if (spawned)
            {
                entry.hasSpawnedOnce = true;
                entry.timer = 0f;
            }
            else
            {
                // 참조가 늦게 잡히는 경우를 대비해서 매 프레임 로그 폭탄이 나지 않도록
                // 1초 뒤 재시도하게 타이머를 살짝 되돌립니다.
                entry.timer = Mathf.Max(0f, requiredTime - 1f);
            }
        }

        private bool TrySpawn(SpawnEntry entry, int entryIndex)
        {
            if (entry == null)
            {
                return false;
            }

            if (entry.prefab == null)
            {
                LogEntryFailureThrottled(entry, entryIndex, "Prefab이 null이라 스폰할 수 없습니다.");
                return false;
            }

            if (playerCharacter == null)
            {
                ResolveReferences();

                if (playerCharacter == null)
                {
                    LogEntryFailureThrottled(
                        entry,
                        entryIndex,
                        "PlayerCharacter를 찾지 못해서 스폰할 수 없습니다. LevelManager.PlayerCharacter 또는 씬의 Character를 확인하세요.");
                    return false;
                }
            }

            Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.right;
            }

            float distance = Mathf.Max(
                1f,
                entry.spawnDistanceFromPlayer + UnityEngine.Random.Range(
                    -entry.spawnDistanceJitter,
                    entry.spawnDistanceJitter));

            Vector2 spawnPosition =
                (Vector2)playerCharacter.transform.position + direction * distance;

            FieldSpecialMonsterBase monster = Instantiate(
                entry.prefab,
                spawnPosition,
                Quaternion.identity);

            if (monster == null)
            {
                LogEntryFailureThrottled(
                    entry,
                    entryIndex,
                    "Instantiate 결과가 null입니다. 프리팹에 FieldSpecialMonsterBase 상속 컴포넌트가 있는지 확인하세요.");
                return false;
            }

            monster.SetupRuntime(this, entityManager, playerCharacter);
            entry.alive.Add(monster);

            if (debugLog)
            {
                Debug.Log(
                    $"[FieldSpecialMonsterSpawner] 특수 몬스터 생성 성공 | " +
                    $"Entry #{entryIndex} | Prefab: {entry.prefab.name} | " +
                    $"Pos: {spawnPosition} | FirstSpawnDone: {entry.hasSpawnedOnce}",
                    this);
            }

            return true;
        }

        private void CleanupEntry(SpawnEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            for (int i = entry.alive.Count - 1; i >= 0; i--)
            {
                FieldSpecialMonsterBase monster = entry.alive[i];

                if (monster == null || !monster.IsAlive)
                {
                    entry.alive.RemoveAt(i);
                }
            }
        }

        private bool ShouldBlockSpawn()
        {
            if (blockInsideMiniStage && MiniStageRuntimeState.IsInsideMiniStage)
            {
                LogBlockedThrottled("[FieldSpecialMonsterSpawner] MiniStageRuntimeState.IsInsideMiniStage=true라 스폰 타이머가 정지 중입니다.");
                return true;
            }

            if (blockWhileRunFlowPaused && levelManager != null && levelManager.IsRunFlowPaused)
            {
                LogBlockedThrottled("[FieldSpecialMonsterSpawner] LevelManager.IsRunFlowPaused=true라 스폰 타이머가 정지 중입니다.");
                return true;
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (levelManager != null)
            {
                if (entityManager == null)
                {
                    entityManager = levelManager.EntityManager;
                }

                if (playerCharacter == null)
                {
                    playerCharacter = levelManager.PlayerCharacter;
                }
            }

            if (entityManager == null)
            {
                entityManager = FindObjectOfType<EntityManager>();
            }

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
            }
        }

        private void LogBlockedThrottled(string message)
        {
            if (!debugLog || !logBlockedState)
            {
                return;
            }

            if (Time.time < nextBlockedLogTime)
            {
                return;
            }

            nextBlockedLogTime = Time.time + Mathf.Max(0.5f, failureLogInterval);
            Debug.Log(message, this);
        }

        private void LogEntryFailureThrottled(
            SpawnEntry entry,
            int entryIndex,
            string reason)
        {
            if (!debugLog)
            {
                return;
            }

            if (entry == null)
            {
                return;
            }

            if (Time.time < entry.nextFailureLogTime)
            {
                return;
            }

            entry.nextFailureLogTime = Time.time + Mathf.Max(0.5f, failureLogInterval);

            string prefabName = entry.prefab != null ? entry.prefab.name : "NULL";

            Debug.LogWarning(
                $"[FieldSpecialMonsterSpawner] Entry #{entryIndex} 스폰 대기/실패 | " +
                $"Prefab: {prefabName} | " +
                $"Timer: {entry.timer:F1} | " +
                $"FirstSpawnTime: {entry.firstSpawnTime:F1} | " +
                $"SpawnInterval: {entry.spawnInterval:F1} | " +
                $"HasSpawnedOnce: {entry.hasSpawnedOnce} | " +
                $"Alive: {entry.alive.Count}/{Mathf.Max(1, entry.maxAliveCount)} | " +
                $"Reason: {reason}",
                this);
        }
    }
}
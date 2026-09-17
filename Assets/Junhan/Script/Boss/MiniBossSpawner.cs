using static UnityEngine.Object;
using System.Collections;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 지정된 시간에 미니보스를 스폰하는 스포너.
    /// 현재는 8분, 15분 스폰용으로 사용한다.
    /// </summary>
    [System.Serializable]
    public class MiniBossSpawner : RuntimeModule
    {
        [System.Serializable]
        public class MiniBossSpawnEntry
        {
            [Header("Timing")]
            [Tooltip("게임 시작 후 몇 초에 스폰할지 설정합니다. 480 = 8분, 900 = 15분")]
            public float spawnTimeSeconds = 480f;

            [Header("Mini Boss")]
            [Tooltip("스폰할 미니보스 블루프린트입니다.")]
            public MiniBossMonsterBlueprint miniBossBlueprint;

            [Tooltip("true면 블루프린트가 Level 1 Blueprint의 어느 poolIndex에 들어있는지 자동으로 찾습니다.")]
            public bool autoResolvePoolIndex = true;

            [Tooltip("autoResolvePoolIndex가 꺼져 있을 때만 사용하는 수동 poolIndex입니다.")]
            public int monsterPoolIndex = -1;

            [Header("Spawn Position")]
            [Tooltip("플레이어 주변에 스폰할지 여부입니다.")]
            public bool spawnAroundPlayer = true;

            [Tooltip("플레이어로부터 떨어져 스폰되는 거리입니다.")]
            public float spawnDistanceFromPlayer = 8f;

            [Tooltip("고정 스폰 위치를 사용할 경우 넣습니다.")]
            public Transform fixedSpawnPoint;

            [Header("Optional")]
            [Tooltip("추가 HP 보정입니다. 보통 0으로 둡니다.")]
            public float hpBuff = 0f;

            [Tooltip("디버그 메모입니다.")]
            public string memo = "Mini Boss Spawn";
        }

        [Header("References")]
        [SerializeField] private LevelManager levelManager;

        [Header("Schedule")]
        [SerializeField]
        private MiniBossSpawnEntry[] spawnEntries =
        {
            new MiniBossSpawnEntry
            {
                spawnTimeSeconds = 480f,
                memo = "8분 미니보스"
            },
            new MiniBossSpawnEntry
            {
                spawnTimeSeconds = 900f,
                memo = "15분 미니보스"
            }
        };

        [Header("Options")]
        [Tooltip("true면 이미 같은 Entry가 실행된 뒤 다시 실행되지 않습니다.")]
        [SerializeField] private bool spawnEachEntryOnlyOnce = true;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        private bool[] spawnedFlags;
        private EntityManager entityManager;
        private LevelBlueprint levelBlueprint;

        protected override void OnAwake()
        {
            if (spawnEntries == null)
            {
                spawnEntries = new MiniBossSpawnEntry[0];
            }

            spawnedFlags = new bool[spawnEntries.Length];
        }

        protected override System.Collections.IEnumerator OnStart()
        {
            ResolveReferences();

            yield break;
        }

        protected override void OnTick()
        {
            if (entityManager == null || levelBlueprint == null)
            {
                ResolveReferences();

                if (entityManager == null || levelBlueprint == null)
                {
                    return;
                }
            }

            float currentTime = levelManager.CurrentLevelTime;

            for (int i = 0; i < spawnEntries.Length; i++)
            {
                MiniBossSpawnEntry entry = spawnEntries[i];

                if (entry == null)
                {
                    continue;
                }

                if (spawnEachEntryOnlyOnce && spawnedFlags[i])
                {
                    continue;
                }

                if (currentTime >= entry.spawnTimeSeconds)
                {
                    spawnedFlags[i] = true;
                    StartCoroutine(SpawnMiniBossRoutine(entry));
                }
            }
        }

        private void ResolveReferences()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            if (levelManager != null)
            {
                entityManager = levelManager.EntityManager;
                levelBlueprint = levelManager.CurrentLevelBlueprint;
            }
        }

        private IEnumerator SpawnMiniBossRoutine(MiniBossSpawnEntry entry)
        {
            if (entry == null)
            {
                yield break;
            }

            if (entityManager == null || levelBlueprint == null)
            {
                ResolveReferences();

                if (entityManager == null || levelBlueprint == null)
                {
                    Debug.LogError("[MiniBossSpawner] EntityManager 또는 LevelBlueprint를 찾지 못했습니다.", Host);
                    yield break;
                }
            }

            if (entry.miniBossBlueprint == null)
            {
                Debug.LogError($"[MiniBossSpawner] MiniBossBlueprint가 비어 있습니다. Memo={entry.memo}", Host);
                yield break;
            }

            int resolvedPoolIndex = entry.monsterPoolIndex;

            if (entry.autoResolvePoolIndex)
            {
                if (!TryFindPoolIndexByBlueprint(entry.miniBossBlueprint, out resolvedPoolIndex))
                {
                    Debug.LogError(
                        $"[MiniBossSpawner] 미니보스 poolIndex 자동 탐색 실패 | " +
                        $"Blueprint={entry.miniBossBlueprint.name} | Memo={entry.memo}\n" +
                        "Level 1 Blueprint의 Monster Settings에 해당 미니보스 블루프린트가 등록되어 있는지 확인하세요.", Host
                    );

                    yield break;
                }
            }

            if (!IsValidPoolIndex(resolvedPoolIndex))
            {
                Debug.LogError(
                    $"[MiniBossSpawner] 잘못된 poolIndex입니다. " +
                    $"PoolIndex={resolvedPoolIndex} | Blueprint={entry.miniBossBlueprint.name} | Memo={entry.memo}", Host
                );

                yield break;
            }

            Vector2 spawnPosition = GetSpawnPosition(entry);

            Monster spawnedMiniBoss = entityManager.SpawnMonster(
                resolvedPoolIndex,
                spawnPosition,
                entry.miniBossBlueprint,
                entry.hpBuff
            );

            if (debugLog)
            {
                string prefabName = levelBlueprint.monsters[resolvedPoolIndex].monstersPrefab != null
                    ? levelBlueprint.monsters[resolvedPoolIndex].monstersPrefab.name
                    : "NULL PREFAB";

                Debug.Log(
                    $"[MiniBossSpawner] 미니보스 스폰 완료 | " +
                    $"Time={FormatTime(levelManager.CurrentLevelTime)} | " +
                    $"Memo={entry.memo} | PoolIndex={resolvedPoolIndex} | " +
                    $"Prefab={prefabName} | Blueprint={entry.miniBossBlueprint.name} | " +
                    $"Position={spawnPosition} | Monster={(spawnedMiniBoss != null ? spawnedMiniBoss.name : "NULL")}", Host
                );
            }

            yield break;
        }

        private Vector2 GetSpawnPosition(MiniBossSpawnEntry entry)
        {
            if (entry.fixedSpawnPoint != null)
            {
                return entry.fixedSpawnPoint.position;
            }

            Character player = levelManager != null ? levelManager.PlayerCharacter : null;

            if (entry.spawnAroundPlayer && player != null)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

                if (direction == Vector2.zero)
                {
                    direction = Vector2.up;
                }

                float distance = Mathf.Max(0.1f, entry.spawnDistanceFromPlayer);

                return (Vector2)player.transform.position + direction * distance;
            }

            return transform.position;
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

            return container != null && container.monstersPrefab != null;
        }

        private string FormatTime(float seconds)
        {
            int totalSeconds = Mathf.FloorToInt(seconds);
            int minutes = totalSeconds / 60;
            int remainSeconds = totalSeconds % 60;

            return $"{minutes:00}:{remainSeconds:00}";
        }
    }
}
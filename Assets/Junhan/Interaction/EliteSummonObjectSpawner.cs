using static UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 엘리트 몬스터 소환 상호작용 오브젝트를 일정 시간마다 필드에 생성한다.
    ///
    /// 기본 규칙:
    /// - 60초마다 1회 스폰
    /// - 한 번에 2개씩 스폰
    /// - 플레이어 주변 일정 거리 밖에 생성
    /// - 보스 소환 오브젝트는 여기서 스폰하지 않는다.
    /// </summary>
    [System.Serializable]
    public class EliteSummonObjectSpawner : RuntimeModule
    {
        [Header("References")]
        [SerializeField] private LevelManager levelManager;
        [SerializeField] private GameObject eliteSummonObjectPrefab;

        [Header("Spawn Timing")]
        [Tooltip("첫 스폰까지 대기 시간입니다. 60이면 게임 시작 1분 뒤 첫 스폰입니다.")]
        [SerializeField] private float firstSpawnDelay = 60f;

        [Tooltip("스폰 주기입니다. 60이면 1분마다 스폰합니다.")]
        [SerializeField] private float spawnInterval = 60f;

        [Tooltip("한 번에 생성할 엘리트 소환 오브젝트 수입니다.")]
        [SerializeField] private int spawnCountPerWave = 2;

        [Header("Spawn Position")]
        [Tooltip("플레이어로부터 최소 몇 거리 밖에 생성할지 설정합니다.")]
        [SerializeField] private float minSpawnDistanceFromPlayer = 8f;

        [Tooltip("플레이어로부터 최대 몇 거리 안에 생성할지 설정합니다.")]
        [SerializeField] private float maxSpawnDistanceFromPlayer = 14f;

        [Tooltip("각 오브젝트끼리 너무 겹치지 않게 하는 최소 거리입니다.")]
        [SerializeField] private float minDistanceBetweenSpawnedObjects = 5f;

        [Tooltip("스폰 위치 탐색 최대 시도 횟수입니다.")]
        [SerializeField] private int maxPositionAttempts = 30;

        [Header("Active Limit")]
        [Tooltip("필드에 동시에 존재할 수 있는 엘리트 소환 오브젝트 최대 수입니다.")]
        [SerializeField] private int maxActiveObjects = 8;

        [Tooltip("상호작용 후 비활성화된 오브젝트를 Destroy해서 하이어라키 누적을 막습니다.")]
        [SerializeField] private bool destroyInactiveSpawnedObjects = true;

        [Header("Debug")]
        [SerializeField] private bool debugLog = true;

        private readonly List<GameObject> activeObjects = new List<GameObject>();

        private float timer;
        private bool firstSpawnDone;

        protected override void OnAwake()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();
            }

            timer = 0f;
            firstSpawnDone = false;
        }

        protected override void OnTick()
        {
            if (levelManager == null)
            {
                levelManager = FindObjectOfType<LevelManager>();

                if (levelManager == null)
                {
                    return;
                }
            }

            if (eliteSummonObjectPrefab == null)
            {
                return;
            }

            CleanupActiveObjects();

            if (MiniStageRuntimeState.IsInsideMiniStage || levelManager.IsRunFlowPaused)
            {
                return;
            }

            timer += Time.deltaTime;

            if (!firstSpawnDone)
            {
                if (timer >= firstSpawnDelay)
                {
                    timer = 0f;
                    firstSpawnDone = true;
                    SpawnWave();
                }

                return;
            }

            if (timer >= spawnInterval)
            {
                timer = 0f;
                SpawnWave();
            }
        }

        private void SpawnWave()
        {
            if (MiniStageRuntimeState.IsInsideMiniStage ||
                (levelManager != null && levelManager.IsRunFlowPaused))
            {
                return;
            }

            CleanupActiveObjects();

            int availableSlots = Mathf.Max(0, maxActiveObjects - activeObjects.Count);

            if (availableSlots <= 0)
            {
                if (debugLog)
                {
                    Debug.Log("[EliteSummonObjectSpawner] 최대 활성 수에 도달해서 스폰하지 않습니다.", Host);
                }

                return;
            }

            int spawnCount = Mathf.Min(
                Mathf.Max(1, spawnCountPerWave),
                availableSlots
            );

            for (int i = 0; i < spawnCount; i++)
            {
                if (!TryGetSpawnPosition(out Vector3 spawnPosition))
                {
                    if (debugLog)
                    {
                        Debug.LogWarning("[EliteSummonObjectSpawner] 스폰 위치를 찾지 못했습니다.", Host);
                    }

                    continue;
                }

                GameObject spawnedObject = Instantiate(
                    eliteSummonObjectPrefab,
                    spawnPosition,
                    Quaternion.identity
                );

                activeObjects.Add(spawnedObject);

                if (debugLog)
                {
                    Debug.Log(
                        $"[EliteSummonObjectSpawner] 엘리트 소환 오브젝트 스폰 | " +
                        $"Position={spawnPosition} | Active={activeObjects.Count}/{maxActiveObjects}",
                        spawnedObject
                    );
                }
            }
        }

        private bool TryGetSpawnPosition(out Vector3 spawnPosition)
        {
            spawnPosition = transform.position;

            Character player = levelManager != null ? levelManager.PlayerCharacter : null;

            if (player == null)
            {
                return false;
            }

            Vector2 playerPosition = player.transform.position;

            float minDistance = Mathf.Max(0.1f, minSpawnDistanceFromPlayer);
            float maxDistance = Mathf.Max(minDistance, maxSpawnDistanceFromPlayer);

            for (int i = 0; i < maxPositionAttempts; i++)
            {
                Vector2 direction = UnityEngine.Random.insideUnitCircle.normalized;

                if (direction == Vector2.zero)
                {
                    direction = Vector2.up;
                }

                float distance = UnityEngine.Random.Range(minDistance, maxDistance);
                Vector2 candidate = playerPosition + direction * distance;

                if (IsTooCloseToOtherInteractables(candidate))
                {
                    continue;
                }

                spawnPosition = new Vector3(candidate.x, candidate.y, 0f);
                return true;
            }

            return false;
        }

        private bool IsTooCloseToOtherInteractables(Vector2 candidate)
        {
            float minDistanceSqr = minDistanceBetweenSpawnedObjects * minDistanceBetweenSpawnedObjects;

            for (int i = 0; i < activeObjects.Count; i++)
            {
                GameObject obj = activeObjects[i];

                if (obj == null || !obj.activeInHierarchy)
                {
                    continue;
                }

                float sqrDistance = ((Vector2)obj.transform.position - candidate).sqrMagnitude;

                if (sqrDistance < minDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        private void CleanupActiveObjects()
        {
            for (int i = activeObjects.Count - 1; i >= 0; i--)
            {
                GameObject obj = activeObjects[i];

                if (obj == null)
                {
                    activeObjects.RemoveAt(i);
                    continue;
                }

                if (!obj.activeInHierarchy)
                {
                    if (destroyInactiveSpawnedObjects)
                    {
                        Destroy(obj);
                    }

                    activeObjects.RemoveAt(i);
                }
            }
        }
    }
}

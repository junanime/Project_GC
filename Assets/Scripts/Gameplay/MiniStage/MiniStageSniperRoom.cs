using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageSniperRoom : MiniStageRoomBase
    {
        [Header("Sniper Room Settings")]
        [Tooltip("미니 스테이지에 생성할 스나이퍼 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에서 스나이퍼 프리팹 Element 번호를 입력합니다.")]
        [SerializeField] private int sniperMonsterPoolIndex = 0;

        [Tooltip("미니 스테이지에 사용할 스나이퍼 MonsterBlueprint입니다. 가능하면 Enforce Spawn Distance를 꺼둔 전용 Blueprint를 사용하세요.")]
        [SerializeField] private MonsterBlueprint sniperBlueprint;

        [Tooltip("스나이퍼 추가 체력입니다. 0이면 Blueprint 기본 체력만 사용합니다.")]
        [SerializeField] private float sniperHpBuff = 0f;

        [Tooltip("스폰 포인트를 배열 순서대로 사용할지 여부입니다. 체크하면 Sniper Spawn Points를 우선 사용합니다.")]
        [SerializeField] private bool useExplicitSniperSpawnPoints = true;

        [Tooltip("스나이퍼를 배치할 위치 목록입니다. Room Prefab 안에 배치한 SpawnPoint들을 연결하세요.")]
        [SerializeField] private Transform[] sniperSpawnPoints;

        [Tooltip("스폰 포인트보다 적은 수만 생성하고 싶을 때 사용합니다. 0 이하이면 스폰 포인트 개수만큼 생성합니다.")]
        [SerializeField] private int sniperSpawnCountOverride = 0;

        [Header("Random Fallback Spawn")]
        [Tooltip("스폰 포인트가 없을 때, 방 중심 기준 가로 반경입니다.")]
        [SerializeField] private float arenaHalfWidth = 12f;

        [Tooltip("스폰 포인트가 없을 때, 방 중심 기준 세로 반경입니다.")]
        [SerializeField] private float arenaHalfHeight = 12f;

        [Tooltip("외곽 벽에 너무 붙지 않게 안쪽으로 당기는 거리입니다.")]
        [SerializeField] private float edgePadding = 1.5f;

        [Tooltip("스나이퍼끼리 너무 붙지 않도록 하는 최소 거리입니다.")]
        [SerializeField] private float minimumSniperDistance = 2f;

        [Tooltip("랜덤 위치를 찾기 위해 시도할 최대 횟수입니다.")]
        [SerializeField] private int spawnPositionTryLimit = 100;

        [Header("Position Safety")]
        [Tooltip("SniperMonsterBlueprint의 스폰 거리 보정 때문에 위치가 바뀌는 경우를 막기 위해, 스폰 직후 지정 위치로 한 번 더 고정합니다.")]
        [SerializeField] private bool forcePositionAfterSpawn = true;

        [Header("Debug")]
        [Tooltip("스나이퍼 스폰/처치 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<Monster> spawnedSnipers = new List<Monster>();
        private int remainingSniperCount;
        private Vector3 lastSniperKilledPosition;

        protected override void OnBeginRoom()
        {
            SpawnSnipers();
        }

        private void SpawnSnipers()
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageSniperRoom] EntityManager가 없어 스나이퍼를 생성할 수 없습니다.");
                CompleteRoom(transform.position);
                return;
            }

            if (sniperBlueprint == null)
            {
                Debug.LogWarning("[MiniStageSniperRoom] Sniper Blueprint가 비어 있습니다.");
                CompleteRoom(transform.position);
                return;
            }

            spawnedSnipers.Clear();
            remainingSniperCount = 0;
            lastSniperKilledPosition = transform.position;

            int spawnCount = GetSpawnCount();

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 spawnPosition = GetSpawnPosition(i);

                Monster sniper = entityManager.SpawnMonster(
                    sniperMonsterPoolIndex,
                    spawnPosition,
                    sniperBlueprint,
                    sniperHpBuff,
                    true

                );

                if (sniper == null)
                {
                    Debug.LogWarning($"[MiniStageSniperRoom] 스나이퍼 생성 실패. index={i}");
                    continue;
                }

                if (forcePositionAfterSpawn)
                {
                    ForceMonsterPosition(sniper, spawnPosition);
                }

                sniper.OnKilled.AddListener(OnSniperKilled);

                spawnedSnipers.Add(sniper);
                remainingSniperCount++;

                if (debugLog)
                {
                    Debug.Log($"[MiniStageSniperRoom] 스나이퍼 생성: {i + 1}/{spawnCount}, position={spawnPosition}");
                }
            }

            if (remainingSniperCount <= 0)
            {
                Debug.LogWarning("[MiniStageSniperRoom] 생성된 스나이퍼가 없습니다. 방을 즉시 클리어 처리합니다.");
                CompleteRoom(transform.position);
            }
        }

        private int GetSpawnCount()
        {
            if (sniperSpawnCountOverride > 0)
            {
                return sniperSpawnCountOverride;
            }

            if (useExplicitSniperSpawnPoints && sniperSpawnPoints != null && sniperSpawnPoints.Length > 0)
            {
                return sniperSpawnPoints.Length;
            }

            return 10;
        }

        private Vector2 GetSpawnPosition(int index)
        {
            if (useExplicitSniperSpawnPoints &&
                sniperSpawnPoints != null &&
                sniperSpawnPoints.Length > 0)
            {
                Transform point = sniperSpawnPoints[index % sniperSpawnPoints.Length];

                if (point != null)
                {
                    return point.position;
                }
            }

            return GetRandomOuterPosition();
        }

        private Vector2 GetRandomOuterPosition()
        {
            List<Vector2> existingPositions = new List<Vector2>();

            for (int i = 0; i < spawnedSnipers.Count; i++)
            {
                if (spawnedSnipers[i] != null)
                {
                    existingPositions.Add(spawnedSnipers[i].transform.position);
                }
            }

            for (int attempt = 0; attempt < spawnPositionTryLimit; attempt++)
            {
                Vector2 candidate = GetRandomOuterPositionRaw();

                bool tooClose = false;

                for (int i = 0; i < existingPositions.Count; i++)
                {
                    if (Vector2.Distance(candidate, existingPositions[i]) < minimumSniperDistance)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    return candidate;
                }
            }

            return GetRandomOuterPositionRaw();
        }

        private Vector2 GetRandomOuterPositionRaw()
        {
            Vector2 center = transform.position;

            float minX = center.x - arenaHalfWidth + edgePadding;
            float maxX = center.x + arenaHalfWidth - edgePadding;
            float minY = center.y - arenaHalfHeight + edgePadding;
            float maxY = center.y + arenaHalfHeight - edgePadding;

            int side = Random.Range(0, 4);

            switch (side)
            {
                case 0:
                    return new Vector2(minX, Random.Range(minY, maxY));

                case 1:
                    return new Vector2(maxX, Random.Range(minY, maxY));

                case 2:
                    return new Vector2(Random.Range(minX, maxX), minY);

                default:
                    return new Vector2(Random.Range(minX, maxX), maxY);
            }
        }

        private void ForceMonsterPosition(Monster monster, Vector2 position)
        {
            monster.transform.position = position;

            Rigidbody2D monsterRigidbody = monster.GetComponent<Rigidbody2D>();

            if (monsterRigidbody != null)
            {
                monsterRigidbody.position = position;
                monsterRigidbody.velocity = Vector2.zero;
                monsterRigidbody.angularVelocity = 0f;
            }
        }

        private void OnSniperKilled(Monster killedMonster)
        {
            if (killedMonster != null)
            {
                killedMonster.OnKilled.RemoveListener(OnSniperKilled);
                lastSniperKilledPosition = killedMonster.transform.position;
            }

            remainingSniperCount = Mathf.Max(0, remainingSniperCount - 1);

            if (debugLog)
            {
                Debug.Log($"[MiniStageSniperRoom] 스나이퍼 처치. 남은 수={remainingSniperCount}");
            }

            if (remainingSniperCount <= 0)
            {
                CompleteRoom(lastSniperKilledPosition);
            }
        }

        protected override void OnCleanupRoom()
        {
            for (int i = 0; i < spawnedSnipers.Count; i++)
            {
                Monster sniper = spawnedSnipers[i];

                if (sniper != null)
                {
                    sniper.OnKilled.RemoveListener(OnSniperKilled);
                }
            }

            spawnedSnipers.Clear();
            remainingSniperCount = 0;
        }
    }
}
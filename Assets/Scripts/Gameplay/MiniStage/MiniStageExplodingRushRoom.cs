using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageExplodingRushRoom : MiniStageRoomBase
    {
        private enum SpawnSide
        {
            Top,
            Bottom,
            Left,
            Right
        }

        [Header("Exploding Monster Settings")]
        [Tooltip("미니 스테이지에 생성할 자폭 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에서 ExplodingMonster 프리팹 Element 번호를 입력합니다.")]
        [SerializeField] private int explodingMonsterPoolIndex = 0;

        [Tooltip("미니 스테이지에 사용할 자폭 몬스터 MonsterBlueprint입니다. ExplodingMonsterBlueprint 에셋을 넣어야 합니다.")]
        [SerializeField] private MonsterBlueprint explodingMonsterBlueprint;

        [Tooltip("자폭 몬스터 추가 체력입니다. 0이면 Blueprint 기본 체력만 사용합니다.")]
        [SerializeField] private float explodingMonsterHpBuff = 0f;

        [Header("Total Spawn Count")]
        [Tooltip("이 방에서 생성할 자폭 몬스터 총 최소 수입니다.")]
        [SerializeField] private int totalMonsterCountMin = 24;

        [Tooltip("이 방에서 생성할 자폭 몬스터 총 최대 수입니다.")]
        [SerializeField] private int totalMonsterCountMax = 36;

        [Header("Wave Settings")]
        [Tooltip("한 웨이브에서 생성할 최소 몬스터 수입니다.")]
        [SerializeField] private int monstersPerWaveMin = 4;

        [Tooltip("한 웨이브에서 생성할 최대 몬스터 수입니다.")]
        [SerializeField] private int monstersPerWaveMax = 6;

        [Tooltip("같은 웨이브 안에서 몬스터 한 마리씩 등장하는 최소 간격입니다.")]
        [SerializeField] private float delayBetweenMonstersMin = 0.35f;

        [Tooltip("같은 웨이브 안에서 몬스터 한 마리씩 등장하는 최대 간격입니다.")]
        [SerializeField] private float delayBetweenMonstersMax = 0.6f;

        [Tooltip("한 웨이브가 끝난 뒤 다음 웨이브가 시작되기 전 최소 대기 시간입니다.")]
        [SerializeField] private float delayBetweenWavesMin = 1.2f;

        [Tooltip("한 웨이브가 끝난 뒤 다음 웨이브가 시작되기 전 최대 대기 시간입니다.")]
        [SerializeField] private float delayBetweenWavesMax = 2.2f;

        [Header("Arena Spawn Area")]
        [Tooltip("방 중심 기준 가로 반경입니다. 실제 벽 안쪽보다 살짝 작게 잡아야 벽 밖 스폰을 막을 수 있습니다.")]
        [SerializeField] private float arenaHalfWidth = 12f;

        [Tooltip("방 중심 기준 세로 반경입니다. 실제 벽 안쪽보다 살짝 작게 잡아야 벽 밖 스폰을 막을 수 있습니다.")]
        [SerializeField] private float arenaHalfHeight = 12f;

        [Tooltip("벽에 너무 붙지 않도록 안쪽으로 당기는 거리입니다.")]
        [SerializeField] private float edgePadding = 1.5f;

        [Tooltip("방 루트 위치와 실제 전투장 중심이 다를 때 사용하는 중심 오프셋입니다.")]
        [SerializeField] private Vector2 roomCenterOffset = Vector2.zero;

        [Header("Spawn Side Options")]
        [Tooltip("위쪽 벽 라인에서 스폰할지 여부입니다.")]
        [SerializeField] private bool spawnFromTop = true;

        [Tooltip("아래쪽 벽 라인에서 스폰할지 여부입니다.")]
        [SerializeField] private bool spawnFromBottom = true;

        [Tooltip("왼쪽 벽 라인에서 스폰할지 여부입니다.")]
        [SerializeField] private bool spawnFromLeft = true;

        [Tooltip("오른쪽 벽 라인에서 스폰할지 여부입니다.")]
        [SerializeField] private bool spawnFromRight = true;

        [Header("Position Safety")]
        [Tooltip("자폭 몬스터 Blueprint나 Setup 과정에서 위치가 바뀌는 경우를 막기 위해, 스폰 직후 지정 위치로 한 번 더 고정합니다.")]
        [SerializeField] private bool forcePositionAfterSpawn = true;

        [Header("Debug")]
        [Tooltip("자폭 러시 방 진행 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<Monster> spawnedMonsters = new List<Monster>();

        private Coroutine spawnRoutine;

        private int targetMonsterCount;
        private int spawnedCount;
        private int remainingAliveCount;

        private bool allMonstersSpawned;
        private bool roomCompleted;

        private Vector3 lastKilledPosition;

        protected override void OnBeginRoom()
        {
            StartExplodingRush();
        }

        private void StartExplodingRush()
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageExplodingRushRoom] EntityManager가 없어 자폭 몬스터를 생성할 수 없습니다.");
                CompleteRoom(transform.position);
                return;
            }

            if (explodingMonsterBlueprint == null)
            {
                Debug.LogWarning("[MiniStageExplodingRushRoom] Exploding Monster Blueprint가 비어 있습니다.");
                CompleteRoom(transform.position);
                return;
            }

            spawnedMonsters.Clear();

            targetMonsterCount = Random.Range(
                Mathf.Min(totalMonsterCountMin, totalMonsterCountMax),
                Mathf.Max(totalMonsterCountMin, totalMonsterCountMax) + 1
            );

            spawnedCount = 0;
            remainingAliveCount = 0;
            allMonstersSpawned = false;
            roomCompleted = false;
            lastKilledPosition = transform.position;

            if (debugLog)
            {
                Debug.Log($"[MiniStageExplodingRushRoom] 자폭 러시 시작. 목표 생성 수={targetMonsterCount}");
            }

            spawnRoutine = StartCoroutine(SpawnWavesRoutine());
        }

        private IEnumerator SpawnWavesRoutine()
        {
            while (spawnedCount < targetMonsterCount)
            {
                int waveSpawnCount = Random.Range(
                    Mathf.Min(monstersPerWaveMin, monstersPerWaveMax),
                    Mathf.Max(monstersPerWaveMin, monstersPerWaveMax) + 1
                );

                waveSpawnCount = Mathf.Min(waveSpawnCount, targetMonsterCount - spawnedCount);

                if (debugLog)
                {
                    Debug.Log($"[MiniStageExplodingRushRoom] 웨이브 시작. 이번 웨이브 생성 수={waveSpawnCount}");
                }

                for (int i = 0; i < waveSpawnCount; i++)
                {
                    SpawnOneExplodingMonster();

                    if (spawnedCount >= targetMonsterCount)
                    {
                        break;
                    }

                    float unitDelay = Random.Range(delayBetweenMonstersMin, delayBetweenMonstersMax);
                    yield return new WaitForSeconds(unitDelay);
                }

                if (spawnedCount < targetMonsterCount)
                {
                    float waveDelay = Random.Range(delayBetweenWavesMin, delayBetweenWavesMax);
                    yield return new WaitForSeconds(waveDelay);
                }
            }

            allMonstersSpawned = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageExplodingRushRoom] 모든 자폭 몬스터 생성 완료.");
            }

            TryCompleteRoom();
        }

        private void SpawnOneExplodingMonster()
        {
            Vector2 spawnPosition = GetRandomEdgeSpawnPosition();

            Monster monster = entityManager.SpawnMonster(
                explodingMonsterPoolIndex,
                spawnPosition,
                explodingMonsterBlueprint,
                explodingMonsterHpBuff
            );

            if (monster == null)
            {
                Debug.LogWarning("[MiniStageExplodingRushRoom] 자폭 몬스터 생성 실패.");
                return;
            }

            if (forcePositionAfterSpawn)
            {
                ForceMonsterPosition(monster, spawnPosition);
            }

            monster.OnKilled.AddListener(OnExplodingMonsterKilled);

            spawnedMonsters.Add(monster);
            spawnedCount++;
            remainingAliveCount++;

            if (debugLog)
            {
                Debug.Log($"[MiniStageExplodingRushRoom] 자폭 몬스터 생성. {spawnedCount}/{targetMonsterCount}, alive={remainingAliveCount}, position={spawnPosition}");
            }
        }

        private Vector2 GetRandomEdgeSpawnPosition()
        {
            SpawnSide side = GetRandomAllowedSide();

            Vector2 center = (Vector2)transform.position + roomCenterOffset;

            float minX = center.x - arenaHalfWidth + edgePadding;
            float maxX = center.x + arenaHalfWidth - edgePadding;
            float minY = center.y - arenaHalfHeight + edgePadding;
            float maxY = center.y + arenaHalfHeight - edgePadding;

            switch (side)
            {
                case SpawnSide.Top:
                    return new Vector2(Random.Range(minX, maxX), maxY);

                case SpawnSide.Bottom:
                    return new Vector2(Random.Range(minX, maxX), minY);

                case SpawnSide.Left:
                    return new Vector2(minX, Random.Range(minY, maxY));

                case SpawnSide.Right:
                    return new Vector2(maxX, Random.Range(minY, maxY));

                default:
                    return center;
            }
        }

        private SpawnSide GetRandomAllowedSide()
        {
            List<SpawnSide> allowedSides = new List<SpawnSide>();

            if (spawnFromTop)
            {
                allowedSides.Add(SpawnSide.Top);
            }

            if (spawnFromBottom)
            {
                allowedSides.Add(SpawnSide.Bottom);
            }

            if (spawnFromLeft)
            {
                allowedSides.Add(SpawnSide.Left);
            }

            if (spawnFromRight)
            {
                allowedSides.Add(SpawnSide.Right);
            }

            if (allowedSides.Count <= 0)
            {
                allowedSides.Add(SpawnSide.Top);
                allowedSides.Add(SpawnSide.Bottom);
                allowedSides.Add(SpawnSide.Left);
                allowedSides.Add(SpawnSide.Right);
            }

            int randomIndex = Random.Range(0, allowedSides.Count);
            return allowedSides[randomIndex];
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

        private void OnExplodingMonsterKilled(Monster killedMonster)
        {
            if (killedMonster != null)
            {
                killedMonster.OnKilled.RemoveListener(OnExplodingMonsterKilled);
                lastKilledPosition = killedMonster.transform.position;
            }

            remainingAliveCount = Mathf.Max(0, remainingAliveCount - 1);

            if (debugLog)
            {
                Debug.Log($"[MiniStageExplodingRushRoom] 자폭 몬스터 처치/소멸. 남은 수={remainingAliveCount}, 생성 완료={allMonstersSpawned}");
            }

            TryCompleteRoom();
        }

        private void TryCompleteRoom()
        {
            if (roomCompleted)
            {
                return;
            }

            if (!allMonstersSpawned)
            {
                return;
            }

            if (remainingAliveCount > 0)
            {
                return;
            }

            roomCompleted = true;

            if (debugLog)
            {
                Debug.Log($"[MiniStageExplodingRushRoom] 자폭 러시 방 클리어. 보상 위치={lastKilledPosition}");
            }

            CompleteRoom(lastKilledPosition);
        }

        protected override void OnCleanupRoom()
        {
            if (spawnRoutine != null)
            {
                StopCoroutine(spawnRoutine);
                spawnRoutine = null;
            }

            for (int i = 0; i < spawnedMonsters.Count; i++)
            {
                Monster monster = spawnedMonsters[i];

                if (monster != null)
                {
                    monster.OnKilled.RemoveListener(OnExplodingMonsterKilled);
                }
            }

            spawnedMonsters.Clear();

            targetMonsterCount = 0;
            spawnedCount = 0;
            remainingAliveCount = 0;
            allMonstersSpawned = false;
            roomCompleted = false;
        }
    }
}
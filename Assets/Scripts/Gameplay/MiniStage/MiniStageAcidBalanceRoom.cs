using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageAcidBalanceRoom : MiniStageRoomBase
    {
        [Header("Acid Slime Monster")]
        [Tooltip("미니 스테이지에 생성할 위산 슬라임 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에서 위산 슬라임 프리팹 Element 번호를 입력합니다.")]
        [SerializeField] private int acidSlimeMonsterPoolIndex = 0;

        [Tooltip("미니 스테이지에 사용할 위산 슬라임 MonsterBlueprint입니다.")]
        [SerializeField] private MonsterBlueprint acidSlimeBlueprint;

        [Tooltip("위산 슬라임 추가 체력입니다. 0이면 Blueprint 기본 체력만 사용합니다.")]
        [SerializeField] private float acidSlimeHpBuff = 0f;

        [Header("Clear Rule")]
        [Tooltip("방 진행 시간입니다. 이 시간이 끝나는 순간의 위산 슬라임 개체수를 기준으로 성공/실패를 판정합니다.")]
        [SerializeField] private float roomDuration = 40f;

        [Tooltip("클리어 성공에 필요한 최소 위산 슬라임 개체수입니다.")]
        [SerializeField] private int requiredMinAliveCount = 20;

        [Tooltip("클리어 성공에 필요한 최대 위산 슬라임 개체수입니다.")]
        [SerializeField] private int requiredMaxAliveCount = 28;

        [Tooltip("실패 시 보상 없이 귀환 가능하게 만들지 여부입니다.")]
        [SerializeField] private bool unlockReturnOnFailure = true;

        [Header("Initial Spawn")]
        [Tooltip("방 시작 시 바로 생성할 위산 슬라임 수입니다. 목표 범위 중간값인 24를 추천합니다.")]
        [SerializeField] private int initialSpawnCount = 24;

        [Tooltip("초기 생성 시 한 프레임에 너무 많이 생성하지 않도록 나눠서 생성할지 여부입니다.")]
        [SerializeField] private bool spreadInitialSpawnOverFrames = true;

        [Tooltip("초기 생성 중 한 프레임에 생성할 최대 수입니다.")]
        [SerializeField] private int initialSpawnPerFrame = 6;

        [Header("Continuous Spawn")]
        [Tooltip("방 진행 중 위산 슬라임을 계속 생성할지 여부입니다.")]
        [SerializeField] private bool spawnDuringRoom = true;

        [Tooltip("위산 슬라임 웨이브 사이 최소 간격입니다.")]
        [SerializeField] private float waveIntervalMin = 1.2f;

        [Tooltip("위산 슬라임 웨이브 사이 최대 간격입니다.")]
        [SerializeField] private float waveIntervalMax = 1.8f;

        [Tooltip("한 웨이브에서 생성할 최소 위산 슬라임 수입니다.")]
        [SerializeField] private int slimesPerWaveMin = 2;

        [Tooltip("한 웨이브에서 생성할 최대 위산 슬라임 수입니다.")]
        [SerializeField] private int slimesPerWaveMax = 4;

        [Tooltip("성능 보호용 최대 활성 위산 슬라임 수입니다. 이 수 이상이면 추가 스폰을 잠시 건너뜁니다.")]
        [SerializeField] private int safetyMaxAliveBeforeSkippingSpawn = 45;

        [Header("Spawn Area")]
        [Tooltip("방 중심 기준 가로 반경입니다. 실제 벽 안쪽보다 살짝 작게 잡아야 벽 밖 스폰을 막을 수 있습니다.")]
        [SerializeField] private float arenaHalfWidth = 12f;

        [Tooltip("방 중심 기준 세로 반경입니다. 실제 벽 안쪽보다 살짝 작게 잡아야 벽 밖 스폰을 막을 수 있습니다.")]
        [SerializeField] private float arenaHalfHeight = 12f;

        [Tooltip("벽에 너무 붙지 않도록 안쪽으로 당기는 거리입니다.")]
        [SerializeField] private float edgePadding = 1.5f;

        [Tooltip("방 루트 위치와 실제 전투장 중심이 다를 때 사용하는 중심 오프셋입니다.")]
        [SerializeField] private Vector2 roomCenterOffset = Vector2.zero;

        [Tooltip("플레이어 바로 위에 스폰되지 않도록 하는 최소 거리입니다.")]
        [SerializeField] private float minDistanceFromPlayer = 2f;

        [Tooltip("적절한 위치를 찾기 위해 몇 번까지 재시도할지 설정합니다.")]
        [SerializeField] private int spawnPositionTryLimit = 30;

        [Header("Position Safety")]
        [Tooltip("MonsterBlueprint나 Setup 과정에서 위치가 바뀌는 경우를 막기 위해, 스폰 직후 지정 위치로 한 번 더 고정합니다.")]
        [SerializeField] private bool forcePositionAfterSpawn = true;

        [Header("Gauge UI")]
        [Tooltip("직접 만든 게이지 UI 프리팹입니다. 비워두면 임시 UI를 자동 생성합니다.")]
        [SerializeField] private MiniStageAcidBalanceGaugeUI gaugeUIPrefab;

        [Tooltip("게이지 UI가 표시할 최대 개체수입니다. 현재 개체수가 이 값을 넘으면 마커가 맨 위에 붙습니다.")]
        [SerializeField] private int gaugeMaxCount = 40;

        [Tooltip("방 종료 후 결과 확인을 위해 UI를 잠시 유지합니다. 귀환 시에는 자동 삭제됩니다.")]
        [SerializeField] private bool keepGaugeVisibleUntilReturn = true;

        [Header("Cleanup")]
        [Tooltip("방을 나갈 때 남아 있는 위산 슬라임을 비활성화합니다. 체크를 추천합니다.")]
        [SerializeField] private bool disableRemainingSlimesOnCleanup = true;

        [Header("Debug")]
        [Tooltip("위장 산도 조절 방 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<Monster> activeSlimes = new List<Monster>();

        private MiniStageAcidBalanceGaugeUI activeGaugeUI;

        private bool roomRunning;
        private bool roomFinished;
        private bool initialSpawnFinished;

        private float elapsedTime;
        private float nextWaveTimer;
        private int pendingInitialSpawnCount;

        protected override void OnBeginRoom()
        {
            StartAcidBalanceRoom();
        }

        private void Update()
        {
            if (!roomRunning || roomFinished)
            {
                return;
            }

            CleanupInactiveSlimes();

            elapsedTime += Time.deltaTime;

            if (!initialSpawnFinished)
            {
                ProcessInitialSpawn();
            }

            if (spawnDuringRoom && initialSpawnFinished)
            {
                UpdateContinuousSpawn();
            }

            UpdateGaugeUI();

            if (elapsedTime >= roomDuration)
            {
                EvaluateResult();
            }
        }

        private void StartAcidBalanceRoom()
        {
            activeSlimes.Clear();

            roomRunning = true;
            roomFinished = false;
            initialSpawnFinished = false;

            elapsedTime = 0f;
            pendingInitialSpawnCount = Mathf.Max(0, initialSpawnCount);
            nextWaveTimer = GetRandomWaveInterval();

            CreateGaugeUI();
            UpdateGaugeUI();

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageAcidBalanceRoom] 위장 산도 조절 방 시작. " +
                    $"duration={roomDuration}, 목표={requiredMinAliveCount}~{requiredMaxAliveCount}, initial={initialSpawnCount}"
                );
            }

            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageAcidBalanceRoom] EntityManager가 없어 위산 슬라임을 생성할 수 없습니다.");
                FailRoomImmediately();
                return;
            }

            if (acidSlimeBlueprint == null)
            {
                Debug.LogWarning("[MiniStageAcidBalanceRoom] Acid Slime Blueprint가 비어 있습니다.");
                FailRoomImmediately();
                return;
            }

            if (!spreadInitialSpawnOverFrames)
            {
                SpawnSlimes(pendingInitialSpawnCount);
                pendingInitialSpawnCount = 0;
                initialSpawnFinished = true;
            }
        }

        private void ProcessInitialSpawn()
        {
            if (pendingInitialSpawnCount <= 0)
            {
                initialSpawnFinished = true;
                return;
            }

            int spawnThisFrame = Mathf.Min(
                Mathf.Max(1, initialSpawnPerFrame),
                pendingInitialSpawnCount
            );

            SpawnSlimes(spawnThisFrame);
            pendingInitialSpawnCount -= spawnThisFrame;

            if (pendingInitialSpawnCount <= 0)
            {
                initialSpawnFinished = true;
            }
        }

        private void UpdateContinuousSpawn()
        {
            nextWaveTimer -= Time.deltaTime;

            if (nextWaveTimer > 0f)
            {
                return;
            }

            int currentAliveCount = GetCurrentAliveCount();

            if (safetyMaxAliveBeforeSkippingSpawn > 0 &&
                currentAliveCount >= safetyMaxAliveBeforeSkippingSpawn)
            {
                if (debugLog)
                {
                    Debug.Log(
                        $"[MiniStageAcidBalanceRoom] 안전 최대 개체수 도달로 스폰 건너뜀. " +
                        $"current={currentAliveCount}, safetyMax={safetyMaxAliveBeforeSkippingSpawn}"
                    );
                }

                nextWaveTimer = GetRandomWaveInterval();
                return;
            }

            int spawnCount = Random.Range(
                Mathf.Min(slimesPerWaveMin, slimesPerWaveMax),
                Mathf.Max(slimesPerWaveMin, slimesPerWaveMax) + 1
            );

            SpawnSlimes(spawnCount);
            nextWaveTimer = GetRandomWaveInterval();
        }

        private void SpawnSlimes(int count)
        {
            if (count <= 0)
            {
                return;
            }

            if (entityManager == null || acidSlimeBlueprint == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector2 spawnPosition = GetRandomSpawnPosition();

                Monster slime = entityManager.SpawnMonster(
                    acidSlimeMonsterPoolIndex,
                    spawnPosition,
                    acidSlimeBlueprint,
                    acidSlimeHpBuff
                );

                if (slime == null)
                {
                    Debug.LogWarning("[MiniStageAcidBalanceRoom] 위산 슬라임 생성 실패.");
                    continue;
                }

                if (forcePositionAfterSpawn)
                {
                    ForceMonsterPosition(slime, spawnPosition);
                }

                slime.OnKilled.RemoveListener(OnSlimeKilled);
                slime.OnKilled.AddListener(OnSlimeKilled);

                activeSlimes.Add(slime);

                if (debugLog)
                {
                    Debug.Log($"[MiniStageAcidBalanceRoom] 위산 슬라임 생성. active={activeSlimes.Count}, position={spawnPosition}");
                }
            }
        }

        private Vector2 GetRandomSpawnPosition()
        {
            Vector2 center = (Vector2)transform.position + roomCenterOffset;

            float minX = center.x - arenaHalfWidth + edgePadding;
            float maxX = center.x + arenaHalfWidth - edgePadding;
            float minY = center.y - arenaHalfHeight + edgePadding;
            float maxY = center.y + arenaHalfHeight - edgePadding;

            Vector2 playerPosition = playerCharacter != null
                ? (Vector2)playerCharacter.transform.position
                : center;

            int tryCount = Mathf.Max(1, spawnPositionTryLimit);

            for (int i = 0; i < tryCount; i++)
            {
                Vector2 candidate = new Vector2(
                    Random.Range(minX, maxX),
                    Random.Range(minY, maxY)
                );

                if (Vector2.Distance(candidate, playerPosition) >= minDistanceFromPlayer)
                {
                    return candidate;
                }
            }

            return new Vector2(
                Random.Range(minX, maxX),
                Random.Range(minY, maxY)
            );
        }

        private void ForceMonsterPosition(Monster monster, Vector2 position)
        {
            if (monster == null)
            {
                return;
            }

            monster.transform.position = position;

            Rigidbody2D monsterRigidbody = monster.GetComponent<Rigidbody2D>();

            if (monsterRigidbody != null)
            {
                monsterRigidbody.position = position;
                monsterRigidbody.velocity = Vector2.zero;
                monsterRigidbody.angularVelocity = 0f;
            }
        }

        private void OnSlimeKilled(Monster killedMonster)
        {
            if (killedMonster != null)
            {
                killedMonster.OnKilled.RemoveListener(OnSlimeKilled);
                activeSlimes.Remove(killedMonster);
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageAcidBalanceRoom] 위산 슬라임 처치/소멸. active={GetCurrentAliveCount()}");
            }

            UpdateGaugeUI();
        }

        private void EvaluateResult()
        {
            if (roomFinished)
            {
                return;
            }

            CleanupInactiveSlimes();

            int finalAliveCount = GetCurrentAliveCount();
            bool success = finalAliveCount >= requiredMinAliveCount &&
                           finalAliveCount <= requiredMaxAliveCount;

            roomFinished = true;
            roomRunning = false;

            if (activeGaugeUI != null)
            {
                activeGaugeUI.UpdateGauge(finalAliveCount, 0f);
                activeGaugeUI.ShowResult(
                    success,
                    finalAliveCount,
                    requiredMinAliveCount,
                    requiredMaxAliveCount
                );
            }

            if (success)
            {
                if (debugLog)
                {
                    Debug.Log($"[MiniStageAcidBalanceRoom] 산도 조절 성공. finalAlive={finalAliveCount}");
                }

                CompleteRoom();
            }
            else
            {
                if (debugLog)
                {
                    Debug.Log($"[MiniStageAcidBalanceRoom] 산도 조절 실패. finalAlive={finalAliveCount}");
                }

                if (unlockReturnOnFailure)
                {
                    CompleteRoomWithoutReward();
                }
            }
        }

        private void FailRoomImmediately()
        {
            roomFinished = true;
            roomRunning = false;

            if (activeGaugeUI != null)
            {
                activeGaugeUI.ShowResult(
                    false,
                    GetCurrentAliveCount(),
                    requiredMinAliveCount,
                    requiredMaxAliveCount
                );
            }

            if (unlockReturnOnFailure)
            {
                CompleteRoomWithoutReward();
            }
        }

        private int GetCurrentAliveCount()
        {
            CleanupInactiveSlimes();
            return activeSlimes.Count;
        }

        private void CleanupInactiveSlimes()
        {
            for (int i = activeSlimes.Count - 1; i >= 0; i--)
            {
                Monster slime = activeSlimes[i];

                if (slime == null || !slime.gameObject.activeInHierarchy || slime.HP <= 0f)
                {
                    if (slime != null)
                    {
                        slime.OnKilled.RemoveListener(OnSlimeKilled);
                    }

                    activeSlimes.RemoveAt(i);
                }
            }
        }

        private float GetRandomWaveInterval()
        {
            return Random.Range(
                Mathf.Min(waveIntervalMin, waveIntervalMax),
                Mathf.Max(waveIntervalMin, waveIntervalMax)
            );
        }

        private void CreateGaugeUI()
        {
            if (gaugeUIPrefab != null)
            {
                activeGaugeUI = Instantiate(gaugeUIPrefab);
            }
            else
            {
                activeGaugeUI = MiniStageAcidBalanceGaugeUI.CreateTemporaryGauge(gaugeMaxCount);
            }

            if (activeGaugeUI != null)
            {
                activeGaugeUI.Initialize(
                    requiredMinAliveCount,
                    requiredMaxAliveCount,
                    gaugeMaxCount
                );
            }
        }

        private void UpdateGaugeUI()
        {
            if (activeGaugeUI == null)
            {
                return;
            }

            float remainingTime = Mathf.Max(0f, roomDuration - elapsedTime);
            int currentAliveCount = GetCurrentAliveCount();

            activeGaugeUI.UpdateGauge(
                currentAliveCount,
                remainingTime
            );
        }

        protected override void OnCleanupRoom()
        {
            roomRunning = false;
            roomFinished = true;

            for (int i = activeSlimes.Count - 1; i >= 0; i--)
            {
                Monster slime = activeSlimes[i];

                if (slime == null)
                {
                    continue;
                }

                slime.OnKilled.RemoveListener(OnSlimeKilled);

                if (disableRemainingSlimesOnCleanup)
                {
                    slime.gameObject.SetActive(false);
                }
            }

            activeSlimes.Clear();

            if (activeGaugeUI != null && !keepGaugeVisibleUntilReturn)
            {
                activeGaugeUI.DestroyGauge();
                activeGaugeUI = null;
            }
            else if (activeGaugeUI != null)
            {
                activeGaugeUI.DestroyGauge();
                activeGaugeUI = null;
            }

            pendingInitialSpawnCount = 0;
            initialSpawnFinished = false;
        }
    }
}
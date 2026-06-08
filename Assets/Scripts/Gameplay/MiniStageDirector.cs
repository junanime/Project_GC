using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class MiniStageDirector : MonoBehaviour
    {
        [Header("Core References")]
        [Tooltip("현재 스테이지의 LevelManager입니다. 미니 스테이지 진입 중에는 기본 런 시간/스폰 흐름을 멈추기 위해 사용합니다.")]
        [SerializeField] private LevelManager levelManager;

        [Tooltip("몬스터, 상자, 경험치, 코인 등을 스폰하는 EntityManager입니다.")]
        [SerializeField] private EntityManager entityManager;

        [Tooltip("플레이어 캐릭터입니다. 비워두면 LevelManager에서 자동으로 가져오려고 시도합니다.")]
        [SerializeField] private Character playerCharacter;

        [Header("Entrance Portal")]
        [Tooltip("필드 혈전을 파괴했을 때 생성할 미니 스테이지 입장 포탈 프리팹입니다.")]
        [SerializeField] private BloodClotMiniStagePortal entrancePortalPrefab;

        [Tooltip("혈전이 죽은 위치에서 포탈을 살짝 옮겨 생성하고 싶을 때 사용하는 오프셋입니다.")]
        [SerializeField] private Vector2 entrancePortalSpawnOffset = Vector2.zero;

        [Tooltip("이미 열린 입장 포탈이 있을 때 새 포탈이 열리면 기존 포탈을 제거할지 여부입니다.")]
        [SerializeField] private bool removePreviousPortalWhenNewPortalOpens = true;

        [Header("Mini Stage Area")]
        [Tooltip("미니 스테이지 전체 루트 오브젝트입니다. 배경, 벽, 테두리, 장식 오브젝트를 이 아래에 넣습니다.")]
        [SerializeField] private GameObject miniStageRoot;

        [Tooltip("플레이어가 미니 스테이지에 들어왔을 때 이동할 위치입니다.")]
        [SerializeField] private Transform miniStageStartPoint;

        [Tooltip("스나이퍼를 모두 처치하고 보상까지 획득한 뒤 E키로 복귀할 중앙 혈전 상호작용 오브젝트입니다.")]
        [SerializeField] private MiniStageReturnInteractable returnInteractable;

        [Header("Mini Stage Sniper Content")]
        [Tooltip("미니 스테이지에 생성할 스나이퍼 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에서 스나이퍼 프리팹 Element 번호를 입력합니다.")]
        [SerializeField] private int sniperMonsterPoolIndex = 0;

        [Tooltip("미니 스테이지에 사용할 스나이퍼 MonsterBlueprint입니다. SniperMonsterBlueprint 에셋을 넣어야 합니다.")]
        [SerializeField] private MonsterBlueprint miniStageSniperBlueprint;

        [Tooltip("미니 스테이지 입장 시 동시에 생성할 스나이퍼 수입니다.")]
        [SerializeField] private int sniperSpawnCount = 10;

        [Tooltip("스나이퍼 추가 체력입니다. 0이면 Blueprint의 기본 체력만 사용합니다.")]
        [SerializeField] private float sniperHpBuff = 0f;

        [Tooltip("외곽 랜덤 스폰 대신 지정된 위치 목록을 사용할지 여부입니다. 체크하면 아래 Sniper Spawn Points를 사용합니다.")]
        [SerializeField] private bool useExplicitSniperSpawnPoints = false;

        [Tooltip("스나이퍼를 배치할 수동 위치 목록입니다. useExplicitSniperSpawnPoints가 true일 때 사용합니다.")]
        [SerializeField] private Transform[] sniperSpawnPoints;

        [Tooltip("자동 랜덤 배치 시 미니 스테이지 중심에서 가로 반경입니다.")]
        [SerializeField] private float arenaHalfWidth = 14f;

        [Tooltip("자동 랜덤 배치 시 미니 스테이지 중심에서 세로 반경입니다.")]
        [SerializeField] private float arenaHalfHeight = 14f;

        [Tooltip("외곽 벽에 너무 붙지 않게 안쪽으로 당기는 거리입니다.")]
        [SerializeField] private float edgePadding = 1.2f;

        [Tooltip("스나이퍼끼리 너무 붙지 않게 하는 최소 거리입니다.")]
        [SerializeField] private float minimumSniperDistance = 2f;

        [Tooltip("랜덤 위치를 찾기 위해 시도할 최대 횟수입니다.")]
        [SerializeField] private int spawnPositionTryLimit = 100;

        [Header("Reward")]
        [Tooltip("모든 스나이퍼 처치 후 마지막 스나이퍼가 죽은 위치에 생성할 보상 상자입니다. 보스 처치 후 나오는 레벨업 상자 Blueprint를 넣으세요.")]
        [SerializeField] private ChestBlueprint sniperClearRewardChestBlueprint;

        [Tooltip("마지막 스나이퍼 사망 위치에서 보상 상자를 살짝 이동시키고 싶을 때 사용하는 오프셋입니다.")]
        [SerializeField] private Vector2 rewardChestOffset = Vector2.zero;

        [Tooltip("보상 상자를 열어야 중앙 혈전 귀환 상호작용이 활성화됩니다.")]
        [SerializeField] private bool requireRewardChestOpenedBeforeReturn = true;

        [Header("Transition")]
        [Tooltip("포탈에서 E를 누른 뒤 미니 스테이지로 이동하기 전 대기 시간입니다.")]
        [SerializeField] private float enterDelay = 0.25f;

        [Tooltip("중앙 귀환 혈전에서 E를 누른 뒤 원래 위치로 복귀하기 전 대기 시간입니다.")]
        [SerializeField] private float returnDelay = 0.5f;

        [Tooltip("복귀할 때 원래 위치 그대로 돌아가면 몬스터와 겹칠 수 있으므로 살짝 밀어낼 오프셋입니다.")]
        [SerializeField] private Vector2 returnOffset = new Vector2(0f, 1.5f);

        [Header("Debug")]
        [Tooltip("미니 스테이지 진입/스나이퍼/보상/복귀 과정을 콘솔에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private bool isInsideMiniStage = false;
        private bool isTransitioning = false;
        private bool allSnipersKilled = false;
        private bool rewardChestOpened = false;

        private Vector3 savedReturnPosition;
        private Vector3 lastSniperKilledPosition;

        private int remainingSniperCount = 0;

        private readonly List<Monster> spawnedMiniStageSnipers = new List<Monster>();

        private BloodClotMiniStagePortal activeEntrancePortal;
        private Chest activeRewardChest;

        public bool IsInsideMiniStage => isInsideMiniStage;

        private void Awake()
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

            if (returnInteractable == null)
            {
                returnInteractable = FindObjectOfType<MiniStageReturnInteractable>();
            }

            if (miniStageRoot != null)
            {
                miniStageRoot.SetActive(false);
            }

            if (returnInteractable != null)
            {
                returnInteractable.SetUnlocked(false);
            }
        }

        private void OnDestroy()
        {
            Chest.OnAnyChestOpened -= OnAnyChestOpened;
        }

        public void OpenEntrancePortal(Vector3 worldPosition)
        {
            if (isInsideMiniStage || isTransitioning)
            {
                if (debugLog)
                {
                    Debug.Log("[MiniStageDirector] 이미 미니 스테이지 진행 중이라 포탈을 열지 않습니다.");
                }

                return;
            }

            if (entrancePortalPrefab == null)
            {
                Debug.LogWarning("[MiniStageDirector] Entrance Portal Prefab이 비어 있습니다. MiniStageDirector 인스펙터에 포탈 프리팹을 연결하세요.");
                return;
            }

            if (removePreviousPortalWhenNewPortalOpens && activeEntrancePortal != null)
            {
                Destroy(activeEntrancePortal.gameObject);
                activeEntrancePortal = null;
            }

            Vector3 spawnPosition = worldPosition + (Vector3)entrancePortalSpawnOffset;

            activeEntrancePortal = Instantiate(
                entrancePortalPrefab,
                spawnPosition,
                Quaternion.identity
            );

            activeEntrancePortal.Setup(this);

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 혈전 포탈 생성 완료. position={spawnPosition}");
            }
        }

        public void EnterMiniStageFromPortal(BloodClotMiniStagePortal portal)
        {
            if (isInsideMiniStage || isTransitioning)
            {
                return;
            }

            if (portal != null)
            {
                portal.Consume();

                if (activeEntrancePortal == portal)
                {
                    activeEntrancePortal = null;
                }
            }

            StartCoroutine(EnterMiniStageRoutine());
        }

        public void EnterMiniStage(BloodClotMonster entranceBloodClot)
        {
            if (entranceBloodClot == null)
            {
                return;
            }

            OpenEntrancePortal(entranceBloodClot.transform.position);
        }

        private IEnumerator EnterMiniStageRoutine()
        {
            isTransitioning = true;

            if (levelManager == null || entityManager == null || playerCharacter == null)
            {
                Debug.LogWarning("[MiniStageDirector] 필수 참조가 비어 있습니다. LevelManager, EntityManager, PlayerCharacter를 확인하세요.");
                isTransitioning = false;
                yield break;
            }

            if (miniStageStartPoint == null)
            {
                Debug.LogWarning("[MiniStageDirector] MiniStageStartPoint가 비어 있습니다.");
                isTransitioning = false;
                yield break;
            }

            savedReturnPosition = playerCharacter.transform.position;

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 미니 스테이지 진입 시작. returnPosition={savedReturnPosition}");
            }

            levelManager.SetRunFlowPaused(true);

            if (miniStageRoot != null)
            {
                miniStageRoot.SetActive(true);
            }

            yield return new WaitForSeconds(enterDelay);

            MovePlayer(miniStageStartPoint.position);

            isInsideMiniStage = true;
            isTransitioning = false;

            ResetMiniStageContentState();
            SpawnMiniStageSnipers();

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 진입 완료. 스나이퍼 콘텐츠 시작.");
            }
        }

        private void ResetMiniStageContentState()
        {
            Chest.OnAnyChestOpened -= OnAnyChestOpened;
            Chest.OnAnyChestOpened += OnAnyChestOpened;

            allSnipersKilled = false;
            rewardChestOpened = false;
            activeRewardChest = null;
            remainingSniperCount = 0;
            lastSniperKilledPosition = miniStageStartPoint != null
                ? miniStageStartPoint.position
                : transform.position;

            spawnedMiniStageSnipers.Clear();

            if (returnInteractable != null)
            {
                returnInteractable.SetUnlocked(false);
            }
        }

        private void SpawnMiniStageSnipers()
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageDirector] EntityManager가 없어 스나이퍼를 생성할 수 없습니다.");
                return;
            }

            if (miniStageSniperBlueprint == null)
            {
                Debug.LogWarning("[MiniStageDirector] Mini Stage Sniper Blueprint가 비어 있습니다.");
                return;
            }

            int spawnCount = Mathf.Max(0, sniperSpawnCount);

            for (int i = 0; i < spawnCount; i++)
            {
                Vector2 spawnPosition = GetSniperSpawnPosition(i);

                Monster sniper = entityManager.SpawnMonster(
                    sniperMonsterPoolIndex,
                    spawnPosition,
                    miniStageSniperBlueprint,
                    sniperHpBuff
                );

                if (sniper == null)
                {
                    Debug.LogWarning($"[MiniStageDirector] 스나이퍼 생성 실패. index={i}");
                    continue;
                }

                sniper.OnKilled.AddListener(OnMiniStageSniperKilled);

                spawnedMiniStageSnipers.Add(sniper);
                remainingSniperCount++;

                if (debugLog)
                {
                    Debug.Log($"[MiniStageDirector] 미니 스테이지 스나이퍼 생성: {i + 1}/{spawnCount}, position={spawnPosition}");
                }
            }

            if (remainingSniperCount <= 0)
            {
                Debug.LogWarning("[MiniStageDirector] 생성된 스나이퍼가 없습니다. 보상 상자를 바로 생성합니다.");
                OnAllMiniStageSnipersKilled();
            }
        }

        private Vector2 GetSniperSpawnPosition(int index)
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

            Vector2 center = miniStageRoot != null
                ? (Vector2)miniStageRoot.transform.position
                : (Vector2)transform.position;

            List<Vector2> chosenPositions = new List<Vector2>();

            foreach (Monster sniper in spawnedMiniStageSnipers)
            {
                if (sniper != null)
                {
                    chosenPositions.Add(sniper.transform.position);
                }
            }

            for (int attempt = 0; attempt < spawnPositionTryLimit; attempt++)
            {
                Vector2 candidate = GetRandomOuterPosition(center);

                bool tooClose = false;

                for (int i = 0; i < chosenPositions.Count; i++)
                {
                    if (Vector2.Distance(candidate, chosenPositions[i]) < minimumSniperDistance)
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

            return GetRandomOuterPosition(center);
        }

        private Vector2 GetRandomOuterPosition(Vector2 center)
        {
            int side = Random.Range(0, 4);

            float minX = center.x - arenaHalfWidth + edgePadding;
            float maxX = center.x + arenaHalfWidth - edgePadding;
            float minY = center.y - arenaHalfHeight + edgePadding;
            float maxY = center.y + arenaHalfHeight - edgePadding;

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

        private void OnMiniStageSniperKilled(Monster killedMonster)
        {
            if (killedMonster != null)
            {
                killedMonster.OnKilled.RemoveListener(OnMiniStageSniperKilled);
                lastSniperKilledPosition = killedMonster.transform.position;
            }

            remainingSniperCount = Mathf.Max(0, remainingSniperCount - 1);

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 미니 스테이지 스나이퍼 처치. 남은 수={remainingSniperCount}");
            }

            if (remainingSniperCount <= 0)
            {
                OnAllMiniStageSnipersKilled();
            }
        }

        private void OnAllMiniStageSnipersKilled()
        {
            if (allSnipersKilled)
            {
                return;
            }

            allSnipersKilled = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 모든 스나이퍼 처치 완료. 보상 상자를 생성합니다.");
            }

            SpawnSniperClearRewardChest();
        }

        private void SpawnSniperClearRewardChest()
        {
            if (entityManager == null)
            {
                return;
            }

            if (sniperClearRewardChestBlueprint == null)
            {
                Debug.LogWarning("[MiniStageDirector] Sniper Clear Reward Chest Blueprint가 비어 있습니다. 보상 상자 없이 귀환 상호작용을 활성화합니다.");
                UnlockReturnInteractable();
                return;
            }

            Vector2 rewardPosition = (Vector2)lastSniperKilledPosition + rewardChestOffset;

            activeRewardChest = entityManager.SpawnChest(
                sniperClearRewardChestBlueprint,
                rewardPosition
            );

            if (!requireRewardChestOpenedBeforeReturn)
            {
                UnlockReturnInteractable();
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 스나이퍼 클리어 보상 상자 생성. position={rewardPosition}");
            }
        }

        private void OnAnyChestOpened(Chest openedChest)
        {
            if (!isInsideMiniStage)
            {
                return;
            }

            if (!requireRewardChestOpenedBeforeReturn)
            {
                return;
            }

            if (activeRewardChest == null)
            {
                return;
            }

            if (openedChest != activeRewardChest)
            {
                return;
            }

            rewardChestOpened = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 보상 상자 획득 확인. 귀환 상호작용을 활성화합니다.");
            }

            UnlockReturnInteractable();
        }

        private void UnlockReturnInteractable()
        {
            if (returnInteractable != null)
            {
                returnInteractable.SetUnlocked(true);
            }
            else
            {
                Debug.LogWarning("[MiniStageDirector] Return Interactable이 비어 있어 귀환 상호작용을 활성화할 수 없습니다.");
            }
        }

        public void ReturnToFieldFromInteractable(MiniStageReturnInteractable interactable)
        {
            if (!isInsideMiniStage || isTransitioning)
            {
                return;
            }

            if (returnInteractable != null && interactable != returnInteractable)
            {
                return;
            }

            if (requireRewardChestOpenedBeforeReturn && !rewardChestOpened)
            {
                if (debugLog)
                {
                    Debug.Log("[MiniStageDirector] 아직 보상 상자를 획득하지 않아 복귀할 수 없습니다.");
                }

                return;
            }

            StartCoroutine(ReturnToFieldRoutine());
        }

        private IEnumerator ReturnToFieldRoutine()
        {
            isTransitioning = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 중앙 귀환 혈전 상호작용 확인. 원래 필드로 복귀합니다.");
            }

            yield return new WaitForSeconds(returnDelay);

            Vector3 returnPosition = savedReturnPosition + (Vector3)returnOffset;

            MovePlayer(returnPosition);

            if (miniStageRoot != null)
            {
                miniStageRoot.SetActive(false);
            }

            CleanupMiniStageState();

            if (levelManager != null)
            {
                levelManager.SetRunFlowPaused(false);
            }

            isTransitioning = false;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 종료 및 원래 필드 복귀 완료.");
            }
        }

        private void CleanupMiniStageState()
        {
            Chest.OnAnyChestOpened -= OnAnyChestOpened;

            for (int i = 0; i < spawnedMiniStageSnipers.Count; i++)
            {
                Monster sniper = spawnedMiniStageSnipers[i];

                if (sniper != null)
                {
                    sniper.OnKilled.RemoveListener(OnMiniStageSniperKilled);
                }
            }

            spawnedMiniStageSnipers.Clear();

            isInsideMiniStage = false;
            allSnipersKilled = false;
            rewardChestOpened = false;
            remainingSniperCount = 0;
            activeRewardChest = null;

            if (returnInteractable != null)
            {
                returnInteractable.SetUnlocked(false);
            }
        }

        private void MovePlayer(Vector3 position)
        {
            playerCharacter.transform.position = position;

            Rigidbody2D playerRigidbody = playerCharacter.GetComponent<Rigidbody2D>();

            if (playerRigidbody != null)
            {
                playerRigidbody.position = position;
                playerRigidbody.velocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }
        }
    }
}
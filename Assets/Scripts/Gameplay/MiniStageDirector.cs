using System.Collections;
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

        [Header("Mini Stage Area")]
        [Tooltip("미니 스테이지 전체 루트 오브젝트입니다. 배경, 벽, 테두리, 장식 오브젝트를 이 아래에 넣습니다. Director 자신은 이 루트 안에 넣지 않는 것을 추천합니다.")]
        [SerializeField] private GameObject miniStageRoot;

        [Tooltip("플레이어가 미니 스테이지에 들어왔을 때 이동할 위치입니다.")]
        [SerializeField] private Transform miniStageStartPoint;

        [Tooltip("미니 스테이지 핵 혈전이 생성될 위치입니다.")]
        [SerializeField] private Transform miniStageCoreSpawnPoint;

        [Header("Mini Stage Core Monster")]
        [Tooltip("혈전 핵 몬스터가 들어있는 Monster Pool Index입니다. LevelBlueprint의 Monsters 배열에 추가한 혈전 프리팹의 인덱스를 입력합니다.")]
        [SerializeField] private int miniStageCoreMonsterPoolIndex = 0;

        [Tooltip("미니 스테이지 내부에서 파괴해야 하는 혈전 핵 MonsterBlueprint입니다.")]
        [SerializeField] private MonsterBlueprint miniStageCoreBlueprint;

        [Tooltip("혈전 핵 추가 체력입니다. 0이면 MonsterBlueprint의 기본 체력만 사용합니다.")]
        [SerializeField] private float miniStageCoreHpBuff = 0f;

        [Header("Transition")]
        [Tooltip("혈전을 파괴한 뒤 미니 스테이지로 이동하기 전 대기 시간입니다.")]
        [SerializeField] private float enterDelay = 0.25f;

        [Tooltip("혈전 핵을 파괴한 뒤 원래 위치로 복귀하기 전 대기 시간입니다.")]
        [SerializeField] private float returnDelay = 0.5f;

        [Tooltip("복귀할 때 원래 위치 그대로 돌아가면 몬스터와 겹칠 수 있으므로 살짝 밀어낼 오프셋입니다.")]
        [SerializeField] private Vector2 returnOffset = new Vector2(0f, 1.5f);

        [Header("Reward")]
        [Tooltip("미니 스테이지 클리어 후 상자를 지급할지 여부입니다.")]
        [SerializeField] private bool spawnRewardChest = true;

        [Tooltip("보상 상자로 사용할 ChestBlueprint입니다. 비워두면 상자는 생성하지 않습니다.")]
        [SerializeField] private ChestBlueprint rewardChestBlueprint;

        [Tooltip("미니 스테이지 클리어 후 경험치 보석을 몇 개 뿌릴지 정합니다.")]
        [SerializeField] private int rewardGemCount = 8;

        [Tooltip("보상 경험치 보석 타입입니다.")]
        [SerializeField] private GemType rewardGemType = GemType.White1;

        [Tooltip("미니 스테이지 클리어 후 코인을 몇 개 뿌릴지 정합니다.")]
        [SerializeField] private int rewardCoinCount = 0;

        [Tooltip("보상 코인 타입입니다.")]
        [SerializeField] private CoinType rewardCoinType = CoinType.Bronze1;

        [Header("Debug")]
        [Tooltip("미니 스테이지 진입/완료/복귀 과정을 콘솔에 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private bool isInsideMiniStage = false;
        private bool isTransitioning = false;
        private Vector3 savedReturnPosition;
        private BloodClotMonster currentCoreMonster;

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

            if (miniStageRoot != null)
            {
                miniStageRoot.SetActive(false);
            }
        }

        public void EnterMiniStage(BloodClotMonster entranceBloodClot)
        {
            if (isInsideMiniStage || isTransitioning)
            {
                return;
            }

            StartCoroutine(EnterMiniStageRoutine());
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

            yield return null;

            SpawnMiniStageCore();

            isTransitioning = false;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 진입 완료.");
            }
        }

        private void SpawnMiniStageCore()
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageDirector] EntityManager가 없어 혈전 핵을 생성할 수 없습니다.");
                return;
            }

            if (miniStageCoreSpawnPoint == null)
            {
                Debug.LogWarning("[MiniStageDirector] MiniStageCoreSpawnPoint가 비어 있습니다.");
                return;
            }

            if (miniStageCoreBlueprint == null)
            {
                Debug.LogWarning("[MiniStageDirector] MiniStageCoreBlueprint이 비어 있습니다.");
                return;
            }

            Monster spawnedMonster = entityManager.SpawnMonster(
                miniStageCoreMonsterPoolIndex,
                miniStageCoreSpawnPoint.position,
                miniStageCoreBlueprint,
                miniStageCoreHpBuff
            );

            currentCoreMonster = spawnedMonster as BloodClotMonster;

            if (currentCoreMonster == null)
            {
                Debug.LogWarning("[MiniStageDirector] 생성된 핵 몬스터가 BloodClotMonster가 아닙니다. 혈전 핵 프리팹에 BloodClotMonster 스크립트가 붙어 있는지 확인하세요.");
                return;
            }

            currentCoreMonster.ConfigureMiniStage(
                this,
                BloodClotMiniStageRole.CompleteMiniStageOnDeath
            );

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 혈전 핵 생성 완료.");
            }
        }

        public void CompleteMiniStage(BloodClotMonster coreBloodClot)
        {
            if (!isInsideMiniStage || isTransitioning)
            {
                return;
            }

            StartCoroutine(CompleteMiniStageRoutine());
        }

        private IEnumerator CompleteMiniStageRoutine()
        {
            isTransitioning = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 클리어. 복귀 준비.");
            }

            yield return new WaitForSeconds(returnDelay);

            Vector3 returnPosition = savedReturnPosition + (Vector3)returnOffset;

            MovePlayer(returnPosition);

            if (miniStageRoot != null)
            {
                miniStageRoot.SetActive(false);
            }

            isInsideMiniStage = false;
            currentCoreMonster = null;

            GiveReward(returnPosition);

            if (levelManager != null)
            {
                levelManager.SetRunFlowPaused(false);
            }

            isTransitioning = false;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 종료 및 복귀 완료.");
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

        private void GiveReward(Vector3 rewardCenterPosition)
        {
            if (entityManager == null)
            {
                return;
            }

            if (spawnRewardChest && rewardChestBlueprint != null)
            {
                entityManager.SpawnChest(rewardChestBlueprint, rewardCenterPosition);
            }

            for (int i = 0; i < rewardGemCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 1.5f;
                entityManager.SpawnExpGem((Vector2)rewardCenterPosition + randomOffset, rewardGemType);
            }

            for (int i = 0; i < rewardCoinCount; i++)
            {
                Vector2 randomOffset = Random.insideUnitCircle * 1.5f;
                entityManager.SpawnCoin((Vector2)rewardCenterPosition + randomOffset, rewardCoinType);
            }

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 미니 스테이지 보상 지급 완료.");
            }
        }
    }
}
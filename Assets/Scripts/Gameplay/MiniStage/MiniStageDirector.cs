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

        [Tooltip("플레이어 캐릭터입니다. 비워두면 LevelManager에서 자동으로 가져옵니다.")]
        [SerializeField] private Character playerCharacter;

        [Header("Entrance Portal")]
        [Tooltip("필드 혈전을 파괴했을 때 생성할 미니 스테이지 입장 포탈 프리팹입니다.")]
        [SerializeField] private BloodClotMiniStagePortal entrancePortalPrefab;

        [Tooltip("혈전이 죽은 위치에서 포탈을 살짝 옮겨 생성하고 싶을 때 사용하는 오프셋입니다.")]
        [SerializeField] private Vector2 entrancePortalSpawnOffset = Vector2.zero;

        [Tooltip("이미 열린 입장 포탈이 있을 때 새 포탈이 열리면 기존 포탈을 제거할지 여부입니다.")]
        [SerializeField] private bool removePreviousPortalWhenNewPortalOpens = true;

        [Header("Room Prefab System")]
        [Tooltip("미니 스테이지 방을 생성할 기준 위치입니다. 보통 메인 필드에서 멀리 떨어진 빈 오브젝트를 연결합니다.")]
        [SerializeField] private Transform miniStageAnchor;

        [Tooltip("입장 시 랜덤으로 생성할 미니 스테이지 Room Prefab 목록입니다.")]
        [SerializeField] private MiniStageRoomBase[] roomPrefabs;

        [Tooltip("테스트용으로 특정 방만 강제 실행할지 여부입니다.")]
        [SerializeField] private bool useForcedRoomIndex = false;

        [Tooltip("useForcedRoomIndex가 켜져 있을 때 사용할 방 인덱스입니다.")]
        [SerializeField] private int forcedRoomIndex = 0;

        [Tooltip("방 종료 후 생성했던 Room Prefab 인스턴스를 삭제할지 여부입니다.")]
        [SerializeField] private bool destroyRoomAfterReturn = true;

        [Header("Transition")]
        [Tooltip("포탈에서 E를 누른 뒤 미니 스테이지로 이동하기 전 대기 시간입니다.")]
        [SerializeField] private float enterDelay = 0.25f;

        [Tooltip("귀환 오브젝트에서 E를 누른 뒤 원래 위치로 복귀하기 전 대기 시간입니다.")]
        [SerializeField] private float returnDelay = 0.5f;

        [Tooltip("복귀할 때 원래 위치 그대로 돌아가면 몬스터와 겹칠 수 있으므로 살짝 밀어낼 오프셋입니다.")]
        [SerializeField] private Vector2 returnOffset = new Vector2(0f, 1.5f);

        [Header("Debug")]
        [Tooltip("미니 스테이지 진입/방 생성/복귀 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private bool isInsideMiniStage = false;
        private bool isTransitioning = false;

        private Vector3 savedReturnPosition;

        private BloodClotMiniStagePortal activeEntrancePortal;
        private MiniStageRoomBase currentRoom;

        public bool IsInsideMiniStage => isInsideMiniStage;
        public MiniStageRoomBase CurrentRoom => currentRoom;

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
                Debug.LogWarning("[MiniStageDirector] Entrance Portal Prefab이 비어 있습니다.");
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

        public void EnterMiniStage(BloodClotMonster entranceBloodClot)
        {
            if (entranceBloodClot == null)
            {
                return;
            }

            OpenEntrancePortal(entranceBloodClot.transform.position);
        }

        public void CompleteMiniStage(BloodClotMonster coreBloodClot)
        {
            Debug.LogWarning(
                "[MiniStageDirector] CompleteMiniStage는 예전 혈전 핵 처치 구조용 함수입니다. " +
                "Room Prefab 방식에서는 MiniStageRoomBase가 방 클리어와 귀환을 처리합니다."
            );
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

        private IEnumerator EnterMiniStageRoutine()
        {
            isTransitioning = true;

            if (levelManager == null || entityManager == null || playerCharacter == null)
            {
                Debug.LogWarning("[MiniStageDirector] 필수 참조가 비어 있습니다. LevelManager, EntityManager, PlayerCharacter를 확인하세요.");
                isTransitioning = false;
                yield break;
            }

            MiniStageRoomBase roomPrefab = SelectRoomPrefab();

            if (roomPrefab == null)
            {
                Debug.LogWarning("[MiniStageDirector] 사용할 Room Prefab이 없습니다. Room Prefabs 배열을 확인하세요.");
                isTransitioning = false;
                yield break;
            }

            savedReturnPosition = playerCharacter.transform.position;

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 미니 스테이지 입장 시작. returnPosition={savedReturnPosition}");
            }

            levelManager.SetRunFlowPaused(true);

            Vector3 roomSpawnPosition = miniStageAnchor != null
                ? miniStageAnchor.position
                : transform.position;

            currentRoom = Instantiate(
                roomPrefab,
                roomSpawnPosition,
                Quaternion.identity
            );

            currentRoom.name = $"{roomPrefab.name}_Runtime";
            currentRoom.InitRoom(this, entityManager, playerCharacter);

            yield return new WaitForSeconds(enterDelay);

            MovePlayer(currentRoom.PlayerStartPoint.position);

            isInsideMiniStage = true;
            isTransitioning = false;

            currentRoom.BeginRoom();

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 미니 스테이지 방 시작: {currentRoom.name}");
            }
        }

        private MiniStageRoomBase SelectRoomPrefab()
        {
            if (roomPrefabs == null || roomPrefabs.Length == 0)
            {
                return null;
            }

            if (useForcedRoomIndex)
            {
                int safeIndex = Mathf.Clamp(forcedRoomIndex, 0, roomPrefabs.Length - 1);
                return roomPrefabs[safeIndex];
            }

            int randomIndex = Random.Range(0, roomPrefabs.Length);
            return roomPrefabs[randomIndex];
        }

        public void ReturnToFieldFromInteractable(MiniStageReturnInteractable interactable)
        {
            if (!isInsideMiniStage || isTransitioning)
            {
                return;
            }

            if (currentRoom == null)
            {
                Debug.LogWarning("[MiniStageDirector] CurrentRoom이 없어 복귀할 수 없습니다.");
                return;
            }

            if (!currentRoom.CanReturnFrom(interactable))
            {
                if (debugLog)
                {
                    Debug.Log("[MiniStageDirector] 아직 방 클리어/보상 조건이 충족되지 않아 복귀할 수 없습니다.");
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
                Debug.Log("[MiniStageDirector] 원래 필드 복귀 시작.");
            }

            yield return new WaitForSeconds(returnDelay);

            Vector3 returnPosition = savedReturnPosition + (Vector3)returnOffset;

            MovePlayer(returnPosition);

            CleanupCurrentRoom();

            if (levelManager != null)
            {
                levelManager.SetRunFlowPaused(false);
            }

            isInsideMiniStage = false;
            isTransitioning = false;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 원래 필드 복귀 완료.");
            }
        }

        private void CleanupCurrentRoom()
        {
            if (currentRoom == null)
            {
                return;
            }

            currentRoom.CleanupRoom();

            if (destroyRoomAfterReturn)
            {
                Destroy(currentRoom.gameObject);
            }
            else
            {
                currentRoom.gameObject.SetActive(false);
            }

            currentRoom = null;
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
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

        [Header("Debug")]
        [Tooltip("미니 스테이지 진입/방 생성/복귀 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private bool isInsideMiniStage = false;
        private bool isTransitioning = false;

        private Vector3 savedReturnPosition;

        private BloodClotMiniStagePortal activeEntrancePortal;
        private BloodClotMiniStagePortal returnPortal;
        private BloodClotTravel travel;
        private MiniStageRoomBase currentRoom;

        public bool CurrentEnhanced { get; private set; }
        public bool IsInsideMiniStage => isInsideMiniStage;
        public bool IsTransitioning => isTransitioning;
        public bool CanEnterFromPortal => !isInsideMiniStage && !isTransitioning && BloodClotTravel.CanTravel(playerCharacter);
        public bool CanReturnFromInteractable(MiniStageReturnInteractable interactable)
        {
            return isInsideMiniStage && !isTransitioning && BloodClotTravel.CanTravel(playerCharacter) && currentRoom != null &&
                currentRoom.CanReturnFrom(interactable);
        }
        public MiniStageRoomBase CurrentRoom => currentRoom;

        private void Awake()
        {
            ResolveReferences();
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

            if (playerCharacter == null)
            {
                playerCharacter = FindObjectOfType<Character>();
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

            // 중요:
            // BloodClotMiniStagePortal에는 Init()이 없고 Setup()이 있다.
            activeEntrancePortal.Setup(this);
            playerCharacter?.GetComponent<PrescriptionRuntime>()?.Record(PrescriptionRuntime.Goal.Portal);

            // 포탈 생성 및 Setup까지 실제로 완료된 순간 1회 재생합니다.
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.MiniStagePortalSpawn
            );

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
            ResolveReferences();
            if (!CanEnterFromPortal || portal == null || portal.Reserved || portal.ChallengeInProgress || Time.timeScale <= 0)
            {
                return;
            }

            var roomPrefab=SelectRoomPrefab();
            if(levelManager==null || entityManager==null || roomPrefab==null || roomPrefab.GetComponentInChildren<MiniStageReturnInteractable>(true)==null)
            { Debug.LogWarning("[MiniStageDirector] 입장 방/복귀 혈전 참조가 없어 입장을 취소합니다."); return; }
            CurrentEnhanced = portal.GetComponent<BloodClotOvercharge>()?.Enhanced ?? false;
            returnPortal=portal;
            returnPortal.Reserve(true);
            StartCoroutine(EnterMiniStageRoutine(roomPrefab));
        }

        private IEnumerator EnterMiniStageRoutine(MiniStageRoomBase roomPrefab)
        {
            isTransitioning = true;
            savedReturnPosition = BloodClotTravel.LeftLanding(returnPortal.transform,playerCharacter);
            travel=BloodClotTravel.Ensure(playerCharacter);
            yield return travel.Dive(returnPortal.transform);
            if(playerCharacter==null || !playerCharacter.IsAlive || returnPortal==null || !travel.Busy){EmergencyReleaseMiniStageRuntime();yield break;}
            // Existing enemies keep updating through both animations. Only field spawning/run time
            // pauses during transfer; the regular off-screen field suspension starts after landing.
            levelManager.SetRunFlowPaused(true);
            GameAudioManager.EnterMiniStageAudio();

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
            var destination=currentRoom.ReturnInteractable;
            destination.SetTravelVisual(true,CurrentEnhanced);
            var landing=BloodClotTravel.LeftLanding(destination.transform,playerCharacter);
            if(currentRoom.PlayerStartPoint!=currentRoom.transform)currentRoom.PlayerStartPoint.position=landing;
            MovePlayer(destination.transform.position);
            isInsideMiniStage = true;
            yield return travel.Eject(destination.transform,landing);
            if(playerCharacter==null || !playerCharacter.IsAlive){EmergencyReleaseMiniStageRuntime();yield break;}
            destination.SetTravelVisual(false,CurrentEnhanced);
            MiniStageRuntimeState.EnterMiniStage(this);
            entityManager.SetFieldMonsterRuntimeSuspended(true);
            isTransitioning = false;

            currentRoom.BeginRoom();

            if (debugLog)
            {
                Debug.Log($"[MiniStageDirector] 미니 스테이지 방 시작: {currentRoom.name}");
            }
        }

        public static bool RoomEnabled(MiniStageRoomBase room) => room != null && !(room is MiniStageAcidBalanceRoom) && !(room is MiniStageDigestiveWaveReflectRoom) && !(room is MiniStageAcidLureRoom);
        private MiniStageRoomBase SelectRoomPrefab()
        {
            if (roomPrefabs == null || roomPrefabs.Length == 0)
            {
                return null;
            }

            if (useForcedRoomIndex)
            {
                int safeIndex = Mathf.Clamp(forcedRoomIndex, 0, roomPrefabs.Length - 1);
                return RoomEnabled(roomPrefabs[safeIndex]) ? roomPrefabs[safeIndex] : null;
            }

            var enabledRooms = System.Array.FindAll(roomPrefabs, RoomEnabled);
            return enabledRooms.Length == 0 ? null : enabledRooms[Random.Range(0, enabledRooms.Length)];
        }

        public void ReturnToFieldFromInteractable(MiniStageReturnInteractable interactable)
        {
            if (!isInsideMiniStage || isTransitioning || !BloodClotTravel.CanTravel(playerCharacter) || Time.timeScale<=0)
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

            StartCoroutine(ReturnToFieldRoutine(interactable));
        }

        private IEnumerator ReturnToFieldRoutine(MiniStageReturnInteractable source)
        {
            isTransitioning = true;

            if (debugLog)
            {
                Debug.Log("[MiniStageDirector] 원래 필드 복귀 시작.");
            }

            travel=BloodClotTravel.Ensure(playerCharacter);
            yield return travel.Dive(source.transform);
            if(playerCharacter==null || !playerCharacter.IsAlive){EmergencyReleaseMiniStageRuntime();yield break;}
            MovePlayer(returnPortal!=null ? returnPortal.transform.position : savedReturnPosition);

            CleanupCurrentRoom();

            // 먼저 전역 MiniStage 상태를 해제한다.
            MiniStageRuntimeState.ExitMiniStage(this);

            // 기존 필드 몬스터 행동 재개.
            if (entityManager != null)
            {
                entityManager.SetFieldMonsterRuntimeSuspended(false);
            }

            // 일반 런 흐름 재개.
            if (levelManager != null)
            {
                levelManager.SetRunFlowPaused(false);
            }

            // MiniStage BGM 종료
            // 기존 Ingame BGM을 Pause했던 위치부터 재생
            GameAudioManager.ExitMiniStageAudio();

            isInsideMiniStage = false;
            var landing=returnPortal!=null ? BloodClotTravel.LeftLanding(returnPortal.transform,playerCharacter) : savedReturnPosition;
            yield return travel.Eject(returnPortal!=null?returnPortal.transform:null,landing);
            if(returnPortal!=null)
            {
                if(activeEntrancePortal==returnPortal)activeEntrancePortal=null;
                returnPortal.Consume();
                returnPortal=null;
            }
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
            entityManager?.ClearMiniStagePickups();

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
            if (playerCharacter == null)
            {
                return;
            }

            playerCharacter.transform.position = position;

            Rigidbody2D playerRigidbody = playerCharacter.GetComponent<Rigidbody2D>();

            if (playerRigidbody != null)
            {
                playerRigidbody.position = position;
                playerRigidbody.velocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }
        }
        private void EmergencyReleaseMiniStageRuntime()
        {
            if (!isInsideMiniStage && !isTransitioning)
            {
                return;
            }
            StopAllCoroutines();
            if(travel!=null)travel.Cancel();
            if(playerCharacter!=null)MovePlayer(savedReturnPosition);
            if(returnPortal!=null)returnPortal.Reserve(false);
            returnPortal=null;
            GameAudioManager.ExitMiniStageAudio();

            if (entityManager != null)
            {
                entityManager.SetFieldMonsterRuntimeSuspended(false);
            }

            if (levelManager != null)
            {
                levelManager.SetRunFlowPaused(false);
            }

            CleanupCurrentRoom();
            entityManager?.ClearMiniStagePickups();
            MiniStageRuntimeState.ExitMiniStage(this);

            isInsideMiniStage = false;
            isTransitioning = false;
        }
        private void OnDisable()
        {
            EmergencyReleaseMiniStageRuntime();
        }

        private void OnDestroy()
        {
            EmergencyReleaseMiniStageRuntime();
        }
    }
}

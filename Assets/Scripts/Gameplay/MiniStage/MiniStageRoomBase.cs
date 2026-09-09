using UnityEngine;

namespace Vampire
{
    public abstract class MiniStageRoomBase : MonoBehaviour
    {
        [Header("Common Room Points")]
        [Tooltip("플레이어가 이 방에 입장했을 때 이동할 시작 위치입니다.")]
        [SerializeField] private Transform playerStartPoint;

        [Tooltip("방 클리어 후 보상 상자를 생성할 기본 위치입니다. 비워두면 방 오브젝트 위치를 사용합니다.")]
        [SerializeField] private Transform rewardSpawnPoint;

        [Tooltip("보상 획득 후 E키로 원래 필드로 돌아가기 위한 귀환 상호작용 오브젝트입니다.")]
        [SerializeField] private MiniStageReturnInteractable returnInteractable;

        [Header("Common Reward")]
        [Tooltip("방 클리어 후 지급할 보상 상자입니다. 보스 처치 후 나오는 레벨업 상자 Blueprint를 넣으면 됩니다.")]
        [SerializeField] private ChestBlueprint rewardChestBlueprint;

        [Tooltip("보상 상자를 열어야 귀환 상호작용이 활성화됩니다.")]
        [SerializeField] private bool requireRewardChestOpenedBeforeReturn = true;

        [Tooltip("보상 상자가 없을 때 자동으로 귀환 상호작용을 열지 여부입니다.")]
        [SerializeField] private bool unlockReturnIfRewardChestMissing = true;

        [Header("Debug")]
        [Tooltip("방 시작/클리어/보상/귀환 상태 로그를 출력합니다.")]
        [SerializeField] protected bool debugLog = true;

        protected MiniStageDirector director;
        protected EntityManager entityManager;
        protected Character playerCharacter;

        private Chest activeRewardChest;
        private bool roomStarted;
        private bool roomCleared;
        private bool rewardChestOpened;

        // 추가:
        // 방 클리어는 아니지만, 제한 시간 이후 플레이어가 선택적으로 나갈 수 있게 하는 상태.
        // 이 값이 true이면 CanReturnFrom()에서 roomCleared 조건을 우회합니다.
        private bool optionalReturnUnlocked;

        public Transform PlayerStartPoint
        {
            get
            {
                if (playerStartPoint != null)
                {
                    return playerStartPoint;
                }

                return transform;
            }
        }

        public MiniStageReturnInteractable ReturnInteractable => returnInteractable;
        public bool RoomCleared => roomCleared;
        public bool RewardChestOpened => rewardChestOpened;
        public bool OptionalReturnUnlocked => optionalReturnUnlocked;

        public void InitRoom(
            MiniStageDirector owner,
            EntityManager entityManager,
            Character playerCharacter
        )
        {
            director = owner;
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            roomStarted = false;
            roomCleared = false;
            rewardChestOpened = false;
            optionalReturnUnlocked = false;
            activeRewardChest = null;

            if (returnInteractable == null)
            {
                returnInteractable = GetComponentInChildren<MiniStageReturnInteractable>(true);
            }

            if (returnInteractable != null)
            {
                returnInteractable.SetUnlocked(false);
            }

            Chest.OnAnyChestOpened -= OnAnyChestOpened;
            Chest.OnAnyChestOpened += OnAnyChestOpened;

            OnInitRoom();

            if (debugLog)
            {
                Debug.Log($"[MiniStageRoomBase] 방 초기화 완료: {gameObject.name}");
            }
        }

        public void BeginRoom()
        {
            if (roomStarted)
            {
                return;
            }

            roomStarted = true;

            if (returnInteractable != null)
            {
                returnInteractable.SetUnlocked(false);
            }

            if (debugLog)
            {
                Debug.Log($"[MiniStageRoomBase] 방 시작: {gameObject.name}");
            }

            OnBeginRoom();
        }

        protected void CompleteRoom(Vector3? rewardPosition = null)
        {
            if (roomCleared)
            {
                return;
            }

            roomCleared = true;

            if (debugLog)
            {
                Debug.Log($"[MiniStageRoomBase] 방 클리어: {gameObject.name}");
            }

            OnRoomCleared();
            SpawnRewardChest(rewardPosition);
        }

        protected void CompleteRoomWithoutReward()
        {
            if (roomCleared)
            {
                return;
            }

            roomCleared = true;
            rewardChestOpened = true;

            // 미니 스테이지 보상 획득 SFX
            GameAudioManager.PlaySfx(
                GameAudioManager.GameSfxId.RewardEvent
            );
            if (debugLog)
            {
                Debug.Log(
                    "[MiniStageRoomBase] 보상 상자 획득 확인. 귀환 상호작용 활성화."
                );
            }
            optionalReturnUnlocked = true;
            activeRewardChest = null;

            if (debugLog)
            {
                Debug.Log($"[MiniStageRoomBase] 방 종료: {gameObject.name} / 보상 없음");
            }

            OnRoomCleared();
            UnlockReturnInteractable();
        }

        /// <summary>
        /// 방을 클리어 처리하지 않고 귀환만 허용합니다.
        /// 예: 제한 시간이 지난 뒤, 플레이어가 계속 클리어를 노릴지 그냥 나갈지 선택하게 만들 때 사용합니다.
        /// </summary>
        protected void UnlockOptionalReturn()
        {
            if (optionalReturnUnlocked)
            {
                return;
            }

            optionalReturnUnlocked = true;

            if (debugLog)
            {
                Debug.Log($"[MiniStageRoomBase] 선택형 중도 귀환 활성화: {gameObject.name}");
            }

            UnlockReturnInteractable();
            OnOptionalReturnUnlocked();
        }

        private void SpawnRewardChest(Vector3? rewardPosition = null)
        {
            if (entityManager == null)
            {
                Debug.LogWarning(
                    "[MiniStageRoomBase] EntityManager가 없어 보상 상자를 생성할 수 없습니다."
                );

                UnlockReturnInteractableIfAllowed();
                return;
            }

            if (rewardChestBlueprint == null)
            {
                if (debugLog)
                {
                    Debug.LogWarning(
                        "[MiniStageRoomBase] Reward Chest Blueprint가 비어 있습니다."
                    );
                }

                UnlockReturnInteractableIfAllowed();
                return;
            }

            Vector3 spawnPosition =
                rewardPosition ??
                (
                    rewardSpawnPoint != null
                        ? rewardSpawnPoint.position
                        : transform.position
                );

            activeRewardChest =
                entityManager.SpawnChest(
                    rewardChestBlueprint,
                    spawnPosition
                );

            // 미니 스테이지 클리어 후 보상 상자가
            // 실제로 생성된 경우에만 클리어 효과음을 재생합니다.
            if (activeRewardChest != null)
            {
                GameAudioManager.PlaySfx(
                    GameAudioManager.GameSfxId.MiniStageClear
                );
            }

            if (debugLog)
            {
                Debug.Log(
                    $"[MiniStageRoomBase] 보상 상자 생성: position={spawnPosition}"
                );
            }

            if (!requireRewardChestOpenedBeforeReturn)
            {
                UnlockReturnInteractable();
            }
        }

        private void OnAnyChestOpened(Chest openedChest)
        {
            if (!roomStarted || !roomCleared)
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
                Debug.Log("[MiniStageRoomBase] 보상 상자 획득 확인. 귀환 상호작용 활성화.");
            }

            OnRewardChestOpened();
            UnlockReturnInteractable();
        }

        private void UnlockReturnInteractableIfAllowed()
        {
            if (unlockReturnIfRewardChestMissing)
            {
                UnlockReturnInteractable();
            }
        }

        protected void UnlockReturnInteractable()
        {
            if (returnInteractable == null)
            {
                Debug.LogWarning("[MiniStageRoomBase] ReturnInteractable이 없어 귀환 상호작용을 활성화할 수 없습니다.");
                return;
            }

            returnInteractable.SetUnlocked(true);
        }

        public bool CanReturnFrom(MiniStageReturnInteractable interactable)
        {
            if (returnInteractable != null && interactable != returnInteractable)
            {
                return false;
            }

            // 추가:
            // 제한 시간 이후 선택형 귀환이 열린 상태라면,
            // 방 클리어/보상 여부와 상관없이 귀환을 허용합니다.
            if (optionalReturnUnlocked)
            {
                return true;
            }

            if (!roomCleared)
            {
                return false;
            }

            if (requireRewardChestOpenedBeforeReturn && !rewardChestOpened)
            {
                return false;
            }

            return true;
        }

        public void CleanupRoom()
        {
            Chest.OnAnyChestOpened -= OnAnyChestOpened;
            OnCleanupRoom();

            if (debugLog)
            {
                Debug.Log($"[MiniStageRoomBase] 방 정리 완료: {gameObject.name}");
            }
        }

        protected virtual void OnInitRoom()
        {
        }

        protected abstract void OnBeginRoom();

        protected virtual void OnRoomCleared()
        {
        }

        protected virtual void OnRewardChestOpened()
        {
        }

        protected virtual void OnOptionalReturnUnlocked()
        {
        }

        protected virtual void OnCleanupRoom()
        {
        }

        private void OnDestroy()
        {
            Chest.OnAnyChestOpened -= OnAnyChestOpened;
        }
    }
}
using System.Collections;
using UnityEngine;

namespace Vampire
{
    public class MiniStageBonusChestRoom : MiniStageRoomBase
    {
        [Header("Bonus Chest Room")]
        [Tooltip("방에 입장한 뒤 보상 상자가 생성되기 전까지의 대기 시간입니다. 0이면 즉시 생성됩니다.")]
        [SerializeField] private float rewardSpawnDelay = 0.15f;

        [Tooltip("입장 보너스 방 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private Coroutine rewardRoutine;

        protected override void OnBeginRoom()
        {
            if (rewardRoutine != null)
            {
                StopCoroutine(rewardRoutine);
            }

            rewardRoutine = StartCoroutine(SpawnRewardRoutine());
        }

        private IEnumerator SpawnRewardRoutine()
        {
            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 보너스 상자 방 시작. 곧 보상 상자를 생성합니다.");
            }

            if (rewardSpawnDelay > 0f)
            {
                yield return new WaitForSeconds(rewardSpawnDelay);
            }

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 보너스 상자 생성 요청.");
            }

            CompleteRoom();
        }

        protected override void OnRewardChestOpened()
        {
            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 보너스 상자 획득 완료. 귀환 상호작용이 활성화됩니다.");
            }
        }

        protected override void OnCleanupRoom()
        {
            if (rewardRoutine != null)
            {
                StopCoroutine(rewardRoutine);
                rewardRoutine = null;
            }
        }
    }
}
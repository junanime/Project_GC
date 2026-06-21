using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Vampire
{
    /// <summary>
    /// 개꿀방 개편 버전.
    ///
    /// 기존:
    /// - 입장하자마자 보스 보상 Chest 1개 생성
    /// - Chest를 열고 나감
    ///
    /// 변경:
    /// - 선택 상자 3개 생성
    /// - 상자 구성: 꽝 / 진짜 보스 보상 / 몬스터 상자
    /// - 플레이어가 E로 하나 선택
    /// - 선택하지 않은 상자는 사라짐
    /// - 꽝: UI 문구 출력 후 보상 없이 귀환 가능
    /// - 진짜 보스 보상: 실제 보스 보상 Chest를 생성하고 즉시 오픈 처리 후 귀환 가능
    /// - 몬스터 상자: 지정 몬스터 1마리 생성, 처치 후 귀환 가능
    /// </summary>
    public class MiniStageBonusChestRoom : MiniStageRoomBase
    {
        [Header("Mystery Chest References")]
        [Tooltip("방 안에 배치할 선택 상자들입니다. 기본적으로 3개를 넣습니다.")]
        [SerializeField] private MiniStageMysteryChestInteractable[] mysteryChests;

        [Tooltip("방 시작 시 자식 오브젝트에서 MiniStageMysteryChestInteractable을 자동으로 찾을지 여부입니다.")]
        [SerializeField] private bool autoFindMysteryChests = true;

        [Tooltip("상자 역할을 매번 랜덤으로 섞을지 여부입니다. true면 같은 위치가 항상 같은 결과가 되지 않습니다.")]
        [SerializeField] private bool randomizeChestRolesOnStart = true;

        [Header("True Reward Chest")]
        [Tooltip("진짜 보스 보상으로 사용할 Chest Blueprint입니다. 기존 보스 보상 Chest Blueprint를 여기에 넣으세요.")]
        [SerializeField] private ChestBlueprint trueRewardChestBlueprint;

        [Tooltip("진짜 보상 선택 시 실제 Chest를 생성할 위치입니다. 비워두면 선택한 상자 위치에 생성합니다.")]
        [SerializeField] private Transform trueRewardChestSpawnPoint;

        [Tooltip("진짜 보상 Chest를 생성한 뒤 몇 초 후 자동으로 열지 정합니다.")]
        [SerializeField] private float trueRewardOpenDelay = 0.15f;

        [Tooltip("진짜 보상 Chest를 자동으로 연 뒤 귀환을 열기까지의 대기 시간입니다.")]
        [SerializeField] private float unlockReturnAfterRewardDelay = 0.2f;

        [Tooltip("진짜 보상을 선택했을 때 선택용 가짜 상자 오브젝트를 숨길지 여부입니다.")]
        [SerializeField] private bool hideSelectedMysteryChestOnTrueReward = true;

        [Header("Monster Chest")]
        [Tooltip("몬스터 상자에서 생성할 몬스터의 Pool Index입니다. LevelBlueprint의 Monsters 배열 순서와 맞춰야 합니다.")]
        [SerializeField] private int monsterPoolIndex = 0;

        [Tooltip("몬스터 상자에서 생성할 몬스터 Blueprint입니다.")]
        [SerializeField] private MonsterBlueprint monsterChestBlueprint;

        [Tooltip("몬스터 상자에서 생성된 몬스터에게 적용할 추가 HP입니다. 0이면 Blueprint 기본 체력을 사용합니다.")]
        [SerializeField] private float monsterHpBuff = 0f;

        [Tooltip("몬스터를 선택한 상자 위치에서 얼마나 떨어뜨려 생성할지 정합니다.")]
        [SerializeField] private Vector2 monsterSpawnOffset = Vector2.zero;

        [Tooltip("몬스터 상자를 선택했을 때 선택한 가짜 상자를 숨길지 여부입니다.")]
        [SerializeField] private bool hideSelectedMysteryChestOnMonsterSpawn = true;

        [Header("Result Message UI")]
        [Tooltip("결과 메시지를 보여줄 UI 루트입니다. 예: ResultMessagePanel. 비워도 기능은 작동합니다.")]
        [SerializeField] private GameObject resultMessageRoot;

        [Tooltip("결과 메시지를 표시할 TMP_Text입니다.")]
        [SerializeField] private TMP_Text resultMessageText;

        [Tooltip("꽝 상자를 선택했을 때 표시할 문구입니다.")]
        [SerializeField] private string fakeMessage = "꽝입니다!";

        [Tooltip("진짜 보상 상자를 선택했을 때 표시할 문구입니다.")]
        [SerializeField] private string trueRewardMessage = "진짜 보상입니다!";

        [Tooltip("몬스터 상자를 선택했을 때 표시할 문구입니다.")]
        [SerializeField] private string monsterMessage = "몬스터 상자였습니다!";

        [Tooltip("몬스터를 처치했을 때 표시할 문구입니다.")]
        [SerializeField] private string monsterClearedMessage = "몬스터 처치 완료!";

        [Tooltip("결과 메시지가 자동으로 사라지기까지의 시간입니다. 0 이하이면 자동으로 숨기지 않습니다.")]
        [SerializeField] private float resultMessageDuration = 2f;

        [Header("Debug")]
        [Tooltip("개편 개꿀방 로그를 출력합니다.")]
        [SerializeField] private bool debugLog = true;

        private readonly List<MiniStageMysteryChestOutcome> roleBuffer = new List<MiniStageMysteryChestOutcome>();

        private Coroutine resultMessageRoutine;
        private Coroutine rewardRoutine;

        private bool selectionMade;
        private bool roomFinished;
        private Monster spawnedMonsterFromChest;

        protected override void OnInitRoom()
        {
            ResolveReferences();
            HideResultMessageImmediate();

            if (mysteryChests != null)
            {
                for (int i = 0; i < mysteryChests.Length; i++)
                {
                    if (mysteryChests[i] != null)
                    {
                        mysteryChests[i].SetOwnerRoom(this);
                    }
                }
            }
        }

        protected override void OnBeginRoom()
        {
            ResolveReferences();

            selectionMade = false;
            roomFinished = false;
            spawnedMonsterFromChest = null;

            HideResultMessageImmediate();

            SetupMysteryChests();

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 개편 개꿀방 시작. 선택 상자 3개가 활성화됩니다.");
            }
        }

        public void NotifyMysteryChestSelected(MiniStageMysteryChestInteractable selectedChest)
        {
            if (selectionMade || roomFinished)
            {
                return;
            }

            if (selectedChest == null)
            {
                return;
            }

            selectionMade = true;

            DespawnUnselectedChests(selectedChest);

            selectedChest.Reveal();

            switch (selectedChest.Outcome)
            {
                case MiniStageMysteryChestOutcome.Fake:
                    HandleFakeChest(selectedChest);
                    break;

                case MiniStageMysteryChestOutcome.TrueReward:
                    HandleTrueRewardChest(selectedChest);
                    break;

                case MiniStageMysteryChestOutcome.Monster:
                    HandleMonsterChest(selectedChest);
                    break;

                default:
                    HandleFakeChest(selectedChest);
                    break;
            }
        }

        private void HandleFakeChest(MiniStageMysteryChestInteractable selectedChest)
        {
            roomFinished = true;

            ShowResultMessage(fakeMessage);

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 꽝 상자 선택. 보상 없이 귀환 가능.");
            }

            CompleteRoomWithoutReward();
        }

        private void HandleTrueRewardChest(MiniStageMysteryChestInteractable selectedChest)
        {
            roomFinished = true;

            ShowResultMessage(trueRewardMessage);

            if (hideSelectedMysteryChestOnTrueReward && selectedChest != null)
            {
                selectedChest.DespawnChest();
            }

            if (rewardRoutine != null)
            {
                StopCoroutine(rewardRoutine);
            }

            rewardRoutine = StartCoroutine(TrueRewardRoutine(selectedChest));

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 진짜 보스 보상 상자 선택. 실제 보상 Chest를 생성하고 자동 오픈합니다.");
            }
        }

        private IEnumerator TrueRewardRoutine(MiniStageMysteryChestInteractable selectedChest)
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] EntityManager가 없어 보상 Chest를 생성할 수 없습니다.");
                CompleteRoomWithoutReward();
                yield break;
            }

            if (trueRewardChestBlueprint == null)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] True Reward Chest Blueprint가 비어 있습니다. 보상 없이 귀환을 엽니다.");
                CompleteRoomWithoutReward();
                yield break;
            }

            Vector2 spawnPosition = GetTrueRewardChestSpawnPosition(selectedChest);

            Chest rewardChest = entityManager.SpawnChest(trueRewardChestBlueprint, spawnPosition);

            if (rewardChest == null)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] 보상 Chest 생성에 실패했습니다. 보상 없이 귀환을 엽니다.");
                CompleteRoomWithoutReward();
                yield break;
            }

            if (trueRewardOpenDelay > 0f)
            {
                yield return new WaitForSeconds(trueRewardOpenDelay);
            }

            rewardChest.OpenChest(true);

            if (unlockReturnAfterRewardDelay > 0f)
            {
                yield return new WaitForSeconds(unlockReturnAfterRewardDelay);
            }

            // 이 방은 MiniStageRoomBase의 CompleteRoom()으로 보상 Chest를 생성하지 않는다.
            // 우리가 직접 SpawnChest + OpenChest를 했으므로, 귀환만 열어준다.
            UnlockOptionalReturn();

            rewardRoutine = null;
        }

        private void HandleMonsterChest(MiniStageMysteryChestInteractable selectedChest)
        {
            ShowResultMessage(monsterMessage);

            if (hideSelectedMysteryChestOnMonsterSpawn && selectedChest != null)
            {
                selectedChest.DespawnChest();
            }

            SpawnMonsterFromChest(selectedChest);

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 몬스터 상자 선택. 몬스터 처치 전까지 귀환 불가.");
            }
        }

        private void SpawnMonsterFromChest(MiniStageMysteryChestInteractable selectedChest)
        {
            if (entityManager == null)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] EntityManager가 없어 몬스터를 생성할 수 없습니다. 귀환을 엽니다.");
                CompleteRoomWithoutReward();
                return;
            }

            if (monsterChestBlueprint == null)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] Monster Chest Blueprint가 비어 있습니다. 귀환을 엽니다.");
                CompleteRoomWithoutReward();
                return;
            }

            Vector2 spawnPosition = selectedChest != null
                ? (Vector2)selectedChest.transform.position + monsterSpawnOffset
                : (Vector2)transform.position + monsterSpawnOffset;

            spawnedMonsterFromChest = entityManager.SpawnMonster(
                monsterPoolIndex,
                spawnPosition,
                monsterChestBlueprint,
                monsterHpBuff,
                true
            );

            if (spawnedMonsterFromChest == null)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] 몬스터 생성에 실패했습니다. 귀환을 엽니다.");
                CompleteRoomWithoutReward();
                return;
            }

            spawnedMonsterFromChest.OnKilled.AddListener(OnMonsterChestMonsterKilled);
        }

        private void OnMonsterChestMonsterKilled(Monster monster)
        {
            if (spawnedMonsterFromChest != null)
            {
                spawnedMonsterFromChest.OnKilled.RemoveListener(OnMonsterChestMonsterKilled);
            }

            spawnedMonsterFromChest = null;
            roomFinished = true;

            ShowResultMessage(monsterClearedMessage);

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 몬스터 상자 몬스터 처치 완료. 귀환 가능.");
            }

            CompleteRoomWithoutReward();
        }

        private Vector2 GetTrueRewardChestSpawnPosition(MiniStageMysteryChestInteractable selectedChest)
        {
            if (trueRewardChestSpawnPoint != null)
            {
                return trueRewardChestSpawnPoint.position;
            }

            if (selectedChest != null)
            {
                return selectedChest.transform.position;
            }

            return transform.position;
        }

        private void SetupMysteryChests()
        {
            if (mysteryChests == null || mysteryChests.Length == 0)
            {
                Debug.LogWarning("[MiniStageBonusChestRoom] Mystery Chests가 비어 있습니다.");
                CompleteRoomWithoutReward();
                return;
            }

            BuildRoleList();

            for (int i = 0; i < mysteryChests.Length; i++)
            {
                MiniStageMysteryChestInteractable chest = mysteryChests[i];

                if (chest == null)
                {
                    continue;
                }

                MiniStageMysteryChestOutcome role = i < roleBuffer.Count
                    ? roleBuffer[i]
                    : MiniStageMysteryChestOutcome.Fake;

                chest.SetOwnerRoom(this);
                chest.Setup(role);
                chest.gameObject.SetActive(true);
            }
        }

        private void BuildRoleList()
        {
            roleBuffer.Clear();

            roleBuffer.Add(MiniStageMysteryChestOutcome.Fake);
            roleBuffer.Add(MiniStageMysteryChestOutcome.TrueReward);
            roleBuffer.Add(MiniStageMysteryChestOutcome.Monster);

            while (roleBuffer.Count < mysteryChests.Length)
            {
                roleBuffer.Add(MiniStageMysteryChestOutcome.Fake);
            }

            if (!randomizeChestRolesOnStart)
            {
                return;
            }

            for (int i = 0; i < roleBuffer.Count; i++)
            {
                int randomIndex = Random.Range(i, roleBuffer.Count);

                MiniStageMysteryChestOutcome temp = roleBuffer[i];
                roleBuffer[i] = roleBuffer[randomIndex];
                roleBuffer[randomIndex] = temp;
            }
        }

        private void DespawnUnselectedChests(MiniStageMysteryChestInteractable selectedChest)
        {
            if (mysteryChests == null)
            {
                return;
            }

            for (int i = 0; i < mysteryChests.Length; i++)
            {
                MiniStageMysteryChestInteractable chest = mysteryChests[i];

                if (chest == null || chest == selectedChest)
                {
                    continue;
                }

                chest.DespawnChest();
            }
        }

        private void ShowResultMessage(string message)
        {
            if (resultMessageRoutine != null)
            {
                StopCoroutine(resultMessageRoutine);
                resultMessageRoutine = null;
            }

            if (resultMessageRoot != null)
            {
                resultMessageRoot.SetActive(true);
            }

            if (resultMessageText != null)
            {
                resultMessageText.text = message;
            }

            if (resultMessageDuration > 0f)
            {
                resultMessageRoutine = StartCoroutine(HideResultMessageRoutine());
            }
        }

        private IEnumerator HideResultMessageRoutine()
        {
            yield return new WaitForSeconds(resultMessageDuration);

            HideResultMessageImmediate();

            resultMessageRoutine = null;
        }

        private void HideResultMessageImmediate()
        {
            if (resultMessageRoot != null)
            {
                resultMessageRoot.SetActive(false);
            }

            if (resultMessageText != null)
            {
                resultMessageText.text = string.Empty;
            }
        }

        private void ResolveReferences()
        {
            if ((mysteryChests == null || mysteryChests.Length == 0) && autoFindMysteryChests)
            {
                mysteryChests = GetComponentsInChildren<MiniStageMysteryChestInteractable>(true);
            }
        }

        protected override void OnCleanupRoom()
        {
            if (rewardRoutine != null)
            {
                StopCoroutine(rewardRoutine);
                rewardRoutine = null;
            }

            if (resultMessageRoutine != null)
            {
                StopCoroutine(resultMessageRoutine);
                resultMessageRoutine = null;
            }

            if (spawnedMonsterFromChest != null)
            {
                spawnedMonsterFromChest.OnKilled.RemoveListener(OnMonsterChestMonsterKilled);

                if (spawnedMonsterFromChest.gameObject.activeInHierarchy && spawnedMonsterFromChest.HP > 0f)
                {
                    spawnedMonsterFromChest.StartCoroutine(spawnedMonsterFromChest.Killed(false));
                }

                spawnedMonsterFromChest = null;
            }

            HideResultMessageImmediate();

            if (debugLog)
            {
                Debug.Log("[MiniStageBonusChestRoom] 개편 개꿀방 정리 완료.");
            }
        }
    }
}
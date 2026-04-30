using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class MerchantUIManager : MonoBehaviour
    {
        public static MerchantUIManager Instance { get; private set; }

        [Header("UI References")]
        [SerializeField] private GameObject shopUIContainer;
        [SerializeField] private Button closeButton;

        [Header("Shop Settings")]
        [SerializeField] private List<ShopItemButton> itemButtons;              // 진열대 (버튼들)
        [SerializeField] private List<MerchantItemBlueprint> allAvailableItems; // 우리가 만든 붕어빵(데이터) 전체 목록

        private MerchantNPC currentInteractingNPC;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            shopUIContainer.SetActive(false);
            closeButton.onClick.AddListener(CloseShop);
        }

        public void OpenShop(MerchantNPC npc)
        {
            currentInteractingNPC = npc;
            shopUIContainer.SetActive(true);

            //  1. 상점을 열었을 때 게임 시간 멈추기 (일시정지)
            Time.timeScale = 0f;

            // 1. 전체 아이템 목록을 랜덤하게 섞기
            List<MerchantItemBlueprint> shuffledItems = new List<MerchantItemBlueprint>(allAvailableItems);
            for (int i = 0; i < shuffledItems.Count; i++)
            {
                MerchantItemBlueprint temp = shuffledItems[i];
                int randomIndex = Random.Range(i, shuffledItems.Count);
                shuffledItems[i] = shuffledItems[randomIndex];
                shuffledItems[randomIndex] = temp;
            }

            // 2. 섞인 아이템들을 진열대(버튼)에 하나씩 올리기
            for (int i = 0; i < itemButtons.Count; i++)
            {
                if (i < shuffledItems.Count)
                {
                    itemButtons[i].gameObject.SetActive(true);
                    itemButtons[i].Setup(shuffledItems[i]);
                }
                else
                {
                    itemButtons[i].gameObject.SetActive(false);
                }
            }
        }

        public void CloseShop()
        {
            // 1. 상점 UI 화면에서 숨기기
            shopUIContainer.SetActive(false);

            //  2. 게임 시간 다시 정상으로 돌리기 (전투 재개)
            Time.timeScale = 1f;

            // 3. NPC 상태 초기화
            if (currentInteractingNPC != null)
            {
                currentInteractingNPC.CloseShopUI();
                currentInteractingNPC = null;
            }
        }

        // 버튼에서 호출되는 클릭 이벤트 (인자가 2개로 늘었습니다!)
        public void OnClickPurchaseItem(MerchantItemBlueprint itemToBuy, ShopItemButton clickedButton)
        {
            if (ProcessPayment(itemToBuy.cost))
            {
                // 새로 만든 스탯 적용기 호출!
                ShopStatApplier statApplier = FindObjectOfType<ShopStatApplier>();
                if (statApplier != null)
                {
                    statApplier.ApplyStats(itemToBuy);
                }

                clickedButton.MarkAsSoldOut();
            }
        }

        // 결제(재화 차감 및 저장)만을 전담하는 독립적인 메서드
        private bool ProcessPayment(int cost)
        {
            // 1. 현재 게임을 관장하는 StatsManager를 찾습니다.
            StatsManager currentStats = FindObjectOfType<StatsManager>();

            if (currentStats != null)
            {
                // 2. 외부에서 공개된 속성(Getter)으로 잔액만 슬쩍 확인합니다.
                if (currentStats.CoinsGained >= cost)
                {
                    // 3. 기존 코드 수정 없이, '음수'를 더하는 트릭으로 돈을 차감합니다!
                    // 예: 100골드 차감 -> IncreaseCoinsGained(-100)
                    currentStats.IncreaseCoinsGained(-cost);
                    return true; // 결제 성공
                }
            }
            else
            {
                Debug.LogError("[MerchantUIManager] 맵에 StatsManager가 없습니다!");
            }

            return false; // 잔액 부족 또는 매니저 없음으로 결제 실패
        }
    }
}
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class RelicShopManager : MonoBehaviour
    {
        // ========================================
        // 유물 버튼
        // ========================================

        [Header("Relic Buttons")]
        [SerializeField] private RelicSlotButton[] relicButtons;


        // ========================================
        // 페이지
        // ========================================

        [Header("Pages")]
        [SerializeField] private GameObject[] pages;

        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;

        private int currentPage = 0;


        // ========================================
        // Unity
        // ========================================

        private void Awake()
        {
            InitButtons();

            if (prevButton != null)
            {
                prevButton.onClick.AddListener(PrevPage);
                prevButton.interactable = true;
            }

            if (nextButton != null)
            {
                nextButton.onClick.AddListener(NextPage);
                nextButton.interactable = true;
            }
        }


        private void OnEnable()
        {
            // 상점을 열면 항상 1페이지
            currentPage = 0;

            RefreshAll();
            RefreshPage();
        }


        // ========================================
        // 유물 초기화
        // ========================================

        private void InitButtons()
        {
            if (relicButtons == null)
            {
                return;
            }

            for (int i = 0; i < relicButtons.Length; i++)
            {
                if (relicButtons[i] != null)
                {
                    relicButtons[i].Init(this);
                }
            }
        }


        // ========================================
        // 유물 선택 / 구매 / 장착
        // ========================================

        public void SelectRelic(RelicSlotButton relicButton)
        {
            if (relicButton == null)
            {
                return;
            }

            string relicId = relicButton.RelicId;

            if (string.IsNullOrWhiteSpace(relicId))
            {
                return;
            }

            bool unlocked = RelicSaveData.IsUnlocked(relicId);

            if (!unlocked)
            {
                bool success = TryBuyRelic(relicButton);

                if (!success)
                {
                    Debug.Log(
                        "[RelicShop] 실버가 부족해서 유물을 구매할 수 없습니다."
                    );

                    return;
                }
            }

            // 한 개만 장착
            RelicSaveData.Equip(relicId);

            RefreshAll();
        }


        private bool TryBuyRelic(RelicSlotButton relicButton)
        {
            int price = relicButton.Price;

            bool paid = SilverWallet.TrySpend(price);

            if (!paid)
            {
                return false;
            }

            RelicSaveData.Unlock(relicButton.RelicId);

            Debug.Log(
                $"[RelicShop] 유물 구매 완료: {relicButton.RelicId}"
            );

            return true;
        }


        private void RefreshAll()
        {
            if (relicButtons == null)
            {
                return;
            }

            for (int i = 0; i < relicButtons.Length; i++)
            {
                if (relicButtons[i] != null)
                {
                    relicButtons[i].Refresh();
                }
            }
        }


        // ========================================
        // 다음 페이지
        // ========================================

        private void NextPage()
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            // 마지막 페이지에서는 멈춤
            if (currentPage < pages.Length - 1)
            {
                currentPage++;
            }

            RefreshPage();
        }


        // ========================================
        // 이전 페이지
        // ========================================

        private void PrevPage()
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            // 첫 페이지에서는 멈춤
            if (currentPage > 0)
            {
                currentPage--;
            }

            RefreshPage();
        }


        // ========================================
        // 페이지 표시
        // ========================================

        private void RefreshPage()
        {
            if (pages == null || pages.Length == 0)
            {
                return;
            }

            currentPage = Mathf.Clamp(
                currentPage,
                0,
                pages.Length - 1
            );

            for (int i = 0; i < pages.Length; i++)
            {
                if (pages[i] == null)
                {
                    continue;
                }

                // 현재 페이지만 활성화
                pages[i].SetActive(i == currentPage);
            }

            // 버튼은 항상 활성화 상태
            if (prevButton != null)
            {
                prevButton.interactable = true;
            }

            if (nextButton != null)
            {
                nextButton.interactable = true;
            }

            Debug.Log(
                $"[RelicShop] 현재 페이지: {currentPage + 1} / {pages.Length}"
            );
        }
    }
}
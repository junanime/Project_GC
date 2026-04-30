using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Vampire
{
    public class ShopItemButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI itemCostText;
        [SerializeField] private Button purchaseButton;

        private MerchantItemBlueprint currentItem;
        private bool isSoldOut = false; // 품절 여부 기억

        private void Awake()
        {
            purchaseButton.onClick.AddListener(OnPurchaseClicked);
        }

        public void Setup(MerchantItemBlueprint item)
        {
            currentItem = item;
            isSoldOut = false; // 상점을 새로 열 때마다 초기화
            purchaseButton.interactable = true; // 버튼 다시 활성화

            itemIcon.sprite = item.itemIcon;
            itemNameText.text = item.itemName;
            itemDescriptionText.text = item.description;
            itemCostText.text = item.cost.ToString() + " G";
            itemCostText.color = Color.black; // 원래 글자색
        }

        private void OnPurchaseClicked()
        {
            if (currentItem == null || isSoldOut) return;

            // 매니저에게 결제를 요청하고, 나 자신(버튼)도 같이 넘겨줍니다!
            MerchantUIManager.Instance.OnClickPurchaseItem(currentItem, this);
        }

        // 성공적으로 구매했을 때 매니저가 호출해 줄 함수
        public void MarkAsSoldOut()
        {
            isSoldOut = true;
            purchaseButton.interactable = false; // 버튼 클릭 금지
            itemCostText.text = "SOLD OUT";
            itemCostText.color = Color.gray; // 글자를 회색으로 변경
        }

        // 돈이 부족할 때 매니저가 호출해 줄 함수
        public void ShowNotEnoughGold()
        {
            itemCostText.text = "돈 부족!";
            itemCostText.color = Color.red; // 빨간색으로 경고

            // 1초 뒤에 원래 가격표로 되돌리는 마법(Invoke)
            Invoke(nameof(ResetPriceText), 1f);
        }

        private void ResetPriceText()
        {
            if (!isSoldOut)
            {
                itemCostText.text = currentItem.cost.ToString() + " G";
                itemCostText.color = Color.black;
            }
        }
    }
}
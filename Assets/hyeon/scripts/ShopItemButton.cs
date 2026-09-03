using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class ShopItemButton : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image itemCardImage;
        [SerializeField] private Image itemIcon;
        [SerializeField] private TextMeshProUGUI itemNameText;
        [SerializeField] private TextMeshProUGUI itemDescriptionText;
        [SerializeField] private TextMeshProUGUI itemCostText;
        [SerializeField] private Button purchaseButton;

        [Header("Card Rarity Sprites")]
        [SerializeField] private Sprite commonCardSprite;
        [SerializeField] private Sprite uncommonCardSprite;
        [SerializeField] private Sprite rareCardSprite;
        [SerializeField] private Sprite legendaryCardSprite;

        private MerchantItemBlueprint currentItem;
        private bool isSoldOut;

        // 로비에서만 사용되는 구매 함수
        private Action<MerchantItemBlueprint, ShopItemButton> customPurchaseHandler;
        private string normalPriceText;

        private void Awake()
        {
            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(OnPurchaseClicked);
        }

        // 기존 인게임 상점용
        public void Setup(MerchantItemBlueprint item)
        {
            SetupInternal(item, $"{item.cost} G", null);
        }

        // 로비 상점용
        public void SetupForLobby(
            MerchantItemBlueprint item,
            int silverCost,
            Action<MerchantItemBlueprint, ShopItemButton> purchaseHandler)
        {
            SetupInternal(item, $"{silverCost} Silver", purchaseHandler);
        }

        private void SetupInternal(
            MerchantItemBlueprint item,
            string priceText,
            Action<MerchantItemBlueprint, ShopItemButton> purchaseHandler)
        {
            currentItem = item;
            isSoldOut = false;
            customPurchaseHandler = purchaseHandler;
            normalPriceText = priceText;

            if (purchaseButton != null)
                purchaseButton.interactable = true;

            if (itemIcon != null)
                itemIcon.sprite = item.itemIcon;

            if (itemNameText != null)
                itemNameText.text = item.itemName;

            if (itemDescriptionText != null)
                itemDescriptionText.text = item.description;

            if (itemCostText != null)
            {
                itemCostText.text = normalPriceText;
                itemCostText.color = Color.black;
            }

            SetCardByRarity(item.itemRarity);
        }

        private void SetCardByRarity(MerchantItemBlueprint.Rarity rarity)
        {
            if (itemCardImage == null)
                return;

            switch (rarity)
            {
                case MerchantItemBlueprint.Rarity.Common:
                    itemCardImage.sprite = commonCardSprite;
                    break;

                case MerchantItemBlueprint.Rarity.Uncommon:
                    itemCardImage.sprite = uncommonCardSprite;
                    break;

                case MerchantItemBlueprint.Rarity.Rare:
                    itemCardImage.sprite = rareCardSprite;
                    break;

                case MerchantItemBlueprint.Rarity.Legendary:
                    itemCardImage.sprite = legendaryCardSprite;
                    break;
            }
        }

        private void OnPurchaseClicked()
        {
            if (currentItem == null || isSoldOut)
                return;

            // 로비에서 생성된 카드면 로비 구매 처리
            if (customPurchaseHandler != null)
            {
                customPurchaseHandler.Invoke(currentItem, this);
                return;
            }

            // 원래 인게임 상점 구매 처리
            if (MerchantUIManager.Instance != null)
            {
                MerchantUIManager.Instance.OnClickPurchaseItem(currentItem, this);
            }
        }

        public void MarkAsSoldOut(string text = "SOLD OUT")
        {
            isSoldOut = true;

            if (purchaseButton != null)
                purchaseButton.interactable = false;

            if (itemCostText != null)
            {
                itemCostText.text = text;
                itemCostText.color = Color.gray;
            }
        }

        public void ShowTemporaryMessage(string message, Color color)
        {
            if (itemCostText == null)
                return;

            itemCostText.text = message;
            itemCostText.color = color;

            CancelInvoke(nameof(ResetPriceText));
            Invoke(nameof(ResetPriceText), 1.2f);
        }

        private void ResetPriceText()
        {
            if (isSoldOut || itemCostText == null)
                return;

            itemCostText.text = normalPriceText;
            itemCostText.color = Color.black;
        }

        public void ShowNotEnoughGold()
        {
            ShowTemporaryMessage("골드 부족!", Color.red);
        }
    }
}
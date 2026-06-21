using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class LobbyShopItemButton : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text costText;
        [SerializeField] private TMP_Text stateText;
        [SerializeField] private Button buyButton;

        private MerchantItemBlueprint item;
        private LobbyShopUIManager owner;

        public void Setup(MerchantItemBlueprint item, LobbyShopUIManager owner)
        {
            this.item = item;
            this.owner = owner;

            if (iconImage != null)
                iconImage.sprite = item.itemIcon;

            if (nameText != null)
                nameText.text = item.itemName;

            if (descriptionText != null)
                descriptionText.text = item.description;

            if (costText != null)
                costText.text = $"{item.silverCost} Silver";

            bool equipped = LobbyLoadoutData.IsEquipped(item);

            if (stateText != null)
                stateText.text = equipped ? "ÀåÂøµÊ" : "";

            if (buyButton != null)
            {
                buyButton.onClick.RemoveAllListeners();
                buyButton.interactable = !equipped;
                buyButton.onClick.AddListener(() => owner.TryBuy(item));
            }
        }
    }
}
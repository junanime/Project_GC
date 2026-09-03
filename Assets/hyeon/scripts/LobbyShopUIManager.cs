using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class LobbyShopUIManager : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button shopIconButton;
        [SerializeField] private Button closeButton;

        [Header("Panel")]
        [SerializeField] private GameObject shopPanel;
        [SerializeField] private Transform itemParent;
        [SerializeField] private LobbyShopItemButton itemButtonPrefab;

        [Header("Shop Items")]
        [SerializeField] private List<MerchantItemBlueprint> shopItems = new List<MerchantItemBlueprint>();

        [Header("Texts")]
        [SerializeField] private TMP_Text silverText;
        [SerializeField] private TMP_Text messageText;

        private void Awake()
        {
            if (shopPanel != null)
                shopPanel.SetActive(false);

            if (shopIconButton != null)
                shopIconButton.onClick.AddListener(OpenShop);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseShop);
        }

        private void OnEnable()
        {
            LobbyLoadoutData.OnChanged += Refresh;
            SilverWallet.OnChanged += HandleSilverChanged;
        }

        private void OnDisable()
        {
            LobbyLoadoutData.OnChanged -= Refresh;
            SilverWallet.OnChanged -= HandleSilverChanged;
        }

        private void HandleSilverChanged(int silver)
        {
            Refresh();
        }

        public void OpenShop()
        {
            shopPanel.SetActive(true);
            Refresh();
        }

        public void CloseShop()
        {
            shopPanel.SetActive(false);
        }

        public void TryBuy(MerchantItemBlueprint item)
        {
            if (item == null)
                return;

            int price = item.silverCost;

            bool success = LobbyLoadoutData.TryBuyAndEquip(item, price, out string message);

            if (messageText != null)
                messageText.text = message;

            if (!success)
                Debug.LogWarning(message);

            Refresh();
        }

        private void Refresh()
        {
            if (silverText != null)
                silverText.text = $"{SilverWallet.Silver} Silver";

            if (itemParent == null || itemButtonPrefab == null)
                return;

            for (int i = itemParent.childCount - 1; i >= 0; i--)
            {
                Destroy(itemParent.GetChild(i).gameObject);
            }

            foreach (var item in shopItems)
            {
                if (item == null)
                    continue;

                if (!item.canBuyInLobby)
                    continue;

                var card = Instantiate(itemButtonPrefab, itemParent);
                card.Setup(item, this);
            }
        }
    }
}
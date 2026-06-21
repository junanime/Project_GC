using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class LobbyMerchantUIManager : MonoBehaviour
    {
        [Header("Open / Close")]
        [SerializeField] private Button shopIconButton;
        [SerializeField] private GameObject shopUIContainer;
        [SerializeField] private Button closeButton;

        [Header("Reroll")]
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollCostText;
        [SerializeField] private int rerollSilverCost = 20;

        [Header("Optional Silver Text")]
        [SerializeField] private TMP_Text silverText;

        [Header("Existing Shop Card UI")]
        [SerializeField] private Transform shopCardsParent;
        [SerializeField] private GameObject shopItemCardPrefab;
        [SerializeField] private Vector2 runtimeCardSize = new Vector2(110f, 150f);

        [Header("Lobby Shop Items")]
        [SerializeField] private List<MerchantItemBlueprint> lobbyItems = new List<MerchantItemBlueprint>();
        [SerializeField] private int displayItemCount = 3;

        private readonly List<ShopItemButton> spawnedCards = new List<ShopItemButton>();
        private readonly List<MerchantItemBlueprint> currentShopItems = new List<MerchantItemBlueprint>();

        private void Awake()
        {
            if (shopIconButton != null)
                shopIconButton.onClick.AddListener(OpenShop);

            if (closeButton != null)
                closeButton.onClick.AddListener(CloseShop);

            if (rerollButton != null)
                rerollButton.onClick.AddListener(Reroll);

            if (shopUIContainer != null)
                shopUIContainer.SetActive(false);

            RefreshSilverText();
            RefreshRerollText();
        }

        private void OnEnable()
        {
            SilverWallet.OnChanged += HandleSilverChanged;
        }

        private void OnDisable()
        {
            SilverWallet.OnChanged -= HandleSilverChanged;
        }

        private void HandleSilverChanged(int silver)
        {
            RefreshSilverText();
        }

        public void OpenShop()
        {
            if (shopUIContainer == null)
                return;

            shopUIContainer.SetActive(true);

            GenerateShopItems();
            DisplayShopItems();
            RefreshSilverText();
        }

        public void CloseShop()
        {
            ClearCards();

            if (shopUIContainer != null)
                shopUIContainer.SetActive(false);
        }

        private void Reroll()
        {
            if (rerollSilverCost > 0 && !SilverWallet.TrySpend(rerollSilverCost))
            {
                Debug.Log("실버가 부족합니다.");
                return;
            }

            GenerateShopItems();
            DisplayShopItems();
            RefreshSilverText();
        }

        private void GenerateShopItems()
        {
            currentShopItems.Clear();

            List<MerchantItemBlueprint> pool = new List<MerchantItemBlueprint>();

            foreach (MerchantItemBlueprint item in lobbyItems)
            {
                if (item == null)
                    continue;

                // 이미 시작 아이템으로 들고 가는 것은 다시 판매하지 않음
                if (LobbyLoadoutData.IsEquipped(item))
                    continue;

                pool.Add(item);
            }

            int targetCount = Mathf.Min(displayItemCount, pool.Count);

            while (currentShopItems.Count < targetCount)
            {
                int index = Random.Range(0, pool.Count);

                currentShopItems.Add(pool[index]);
                pool.RemoveAt(index);
            }
        }

        private void DisplayShopItems()
        {
            ClearCards();

            if (shopCardsParent == null || shopItemCardPrefab == null)
            {
                Debug.LogError("[LobbyMerchantUIManager] 카드 부모 또는 카드 프리팹이 비어 있습니다.");
                return;
            }

            foreach (MerchantItemBlueprint item in currentShopItems)
            {
                GameObject cardObject = Instantiate(shopItemCardPrefab, shopCardsParent);

                RectTransform rect = cardObject.GetComponent<RectTransform>();

                if (rect != null)
                {
                    rect.localScale = Vector3.one;
                    rect.localRotation = Quaternion.identity;
                    rect.anchoredPosition3D = Vector3.zero;
                    rect.sizeDelta = runtimeCardSize;
                }

                LayoutElement layoutElement = cardObject.GetComponent<LayoutElement>();

                if (layoutElement != null)
                {
                    layoutElement.preferredWidth = runtimeCardSize.x;
                    layoutElement.preferredHeight = runtimeCardSize.y;
                    layoutElement.flexibleWidth = 0f;
                    layoutElement.flexibleHeight = 0f;
                }

                ShopItemButton card = cardObject.GetComponent<ShopItemButton>();

                if (card == null)
                {
                    Debug.LogError("상점 카드 프리팹에 ShopItemButton이 없습니다.");
                    Destroy(cardObject);
                    continue;
                }

                card.SetupForLobby(item, item.silverCost, TryBuyItem);

                if (LobbyLoadoutData.IsEquipped(item))
                    card.MarkAsSoldOut("장착됨");

                spawnedCards.Add(card);
            }
        }

        private void TryBuyItem(MerchantItemBlueprint item, ShopItemButton card)
        {
            bool success = LobbyLoadoutData.TryBuyAndEquip(
                item,
                item.silverCost,
                out string message
            );

            if (success)
            {
                card.MarkAsSoldOut("장착됨");
                RefreshSilverText();
                Debug.Log(message);
            }
            else
            {
                card.ShowTemporaryMessage(message, Color.red);
            }
        }

        private void ClearCards()
        {
            foreach (ShopItemButton card in spawnedCards)
            {
                if (card != null)
                    Destroy(card.gameObject);
            }

            spawnedCards.Clear();
        }

        private void RefreshSilverText()
        {
            if (silverText != null)
                silverText.text = $"{SilverWallet.Silver} Silver";
        }

        private void RefreshRerollText()
        {
            if (rerollCostText != null)
                rerollCostText.text = $"{rerollSilverCost} Silver";
        }
    }
}
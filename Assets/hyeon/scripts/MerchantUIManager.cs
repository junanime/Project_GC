using System.Collections.Generic;
using TMPro;
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
        [SerializeField] private Button rerollButton;
        [SerializeField] private TMP_Text rerollCostText;

        [Header("Shop Settings")]
        [SerializeField] private List<ShopItemButton> itemButtons;
        [SerializeField] private List<MerchantItemBlueprint> allAvailableItems;

        [Header("Reroll Settings")]
        [SerializeField] private int baseRerollCost = 50;
        [SerializeField] private int rerollCostIncrease = 25;

        [Header("Sound Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip rerollSound;

        private MerchantNPC currentInteractingNPC;

        private int currentRerollCost;
        private int rerollCount;

        private List<MerchantItemBlueprint> currentShopItems = new List<MerchantItemBlueprint>();
        private bool hasGeneratedShopItems = false;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            shopUIContainer.SetActive(false);

            closeButton.onClick.AddListener(CloseShop);

            if (rerollButton != null)
                rerollButton.onClick.AddListener(OnClickRerollItems);
        }

        public void OpenShop(MerchantNPC npc)
        {
            currentInteractingNPC = npc;
            shopUIContainer.SetActive(true);

            Time.timeScale = 0f;

            if (!hasGeneratedShopItems)
            {
                rerollCount = 0;
                currentRerollCost = baseRerollCost;

                GenerateNewShopItems();
                hasGeneratedShopItems = true;
            }

            UpdateRerollCostText();
            DisplayCurrentShopItems();
        }

        private void GenerateNewShopItems()
        {
            currentShopItems.Clear();

            List<MerchantItemBlueprint> shuffledItems = new List<MerchantItemBlueprint>(allAvailableItems);

            for (int i = 0; i < shuffledItems.Count; i++)
            {
                MerchantItemBlueprint temp = shuffledItems[i];
                int randomIndex = Random.Range(i, shuffledItems.Count);
                shuffledItems[i] = shuffledItems[randomIndex];
                shuffledItems[randomIndex] = temp;
            }

            for (int i = 0; i < itemButtons.Count && i < shuffledItems.Count; i++)
            {
                currentShopItems.Add(shuffledItems[i]);
            }
        }

        private void DisplayCurrentShopItems()
        {
            for (int i = 0; i < itemButtons.Count; i++)
            {
                if (i < currentShopItems.Count)
                {
                    itemButtons[i].gameObject.SetActive(true);
                    itemButtons[i].Setup(currentShopItems[i]);
                }
                else
                {
                    itemButtons[i].gameObject.SetActive(false);
                }
            }
        }

        public void OnClickRerollItems()
        {
            if (ProcessPayment(currentRerollCost))
            {
                GenerateNewShopItems();
                DisplayCurrentShopItems();

                PlayRerollSound();

                rerollCount++;
                currentRerollCost = baseRerollCost + (rerollCostIncrease * rerollCount);
                UpdateRerollCostText();

                Debug.Log($"[상점] 리롤 성공! 다음 리롤 비용: {currentRerollCost}G");
            }
            else
            {
                Debug.LogWarning("[상점] 리롤할 골드가 부족합니다!");
            }
        }

        private void UpdateRerollCostText()
        {
            if (rerollCostText != null)
            {
                rerollCostText.text = $"Reroll - {currentRerollCost}G";
            }
        }

        private void PlayRerollSound()
        {
            if (audioSource != null && rerollSound != null)
            {
                audioSource.PlayOneShot(rerollSound);
            }
        }

        public void CloseShop()
        {
            shopUIContainer.SetActive(false);

            Time.timeScale = 1f;

            if (currentInteractingNPC != null)
            {
                currentInteractingNPC.CloseShopUI();
                currentInteractingNPC = null;
            }
        }

        public void OnClickPurchaseItem(MerchantItemBlueprint itemToBuy, ShopItemButton clickedButton)
        {
            if (ProcessPayment(itemToBuy.cost))
            {
                ShopStatApplier statApplier = FindObjectOfType<ShopStatApplier>();
                if (statApplier != null)
                {
                    statApplier.ApplyStats(itemToBuy);
                }

                MerchantNPC npcToDestroy = currentInteractingNPC;

                hasGeneratedShopItems = false;
                currentShopItems.Clear();

                CloseShop();

                if (npcToDestroy != null)
                {
                    Debug.Log("<color=magenta>[시스템]</color> 거래 완료! 아저씨가 퇴근했습니다.");
                    Destroy(npcToDestroy.gameObject);
                }
            }
        }

        private bool ProcessPayment(int cost)
        {
            StatsManager currentStats = FindObjectOfType<StatsManager>();

            if (currentStats != null)
            {
                if (currentStats.CoinsGained >= cost)
                {
                    currentStats.IncreaseCoinsGained(-cost);
                    return true;
                }
                else
                {
                    Debug.LogWarning("[상점] 골드가 부족합니다!");
                }
            }
            else
            {
                Debug.LogError("[MerchantUIManager] 맵에 StatsManager가 없습니다!");
            }

            return false;
        }
    }
}
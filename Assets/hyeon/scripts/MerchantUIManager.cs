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

        [Header("Base Rarity Chance")]
        [SerializeField] private int commonWeight = 55;
        [SerializeField] private int uncommonWeight = 32;
        [SerializeField] private int rareWeight = 11;
        [SerializeField] private int legendaryWeight = 2;

        [Header("Reroll Rarity Bonus")]
        [SerializeField] private int rareBonusPerReroll = 2;
        [SerializeField] private int legendaryBonusPerReroll = 1;
        [SerializeField] private int maxRareBonus = 8;
        [SerializeField] private int maxLegendaryBonus = 4;
        [SerializeField] private int minCommonWeight = 20;

        [Header("Reroll Settings")]
        [SerializeField] private int baseRerollCost = 10;
        [SerializeField] private int rerollCostIncrease = 5;

        [Header("Sound Settings")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip rerollSound;

        private MerchantNPC currentInteractingNPC;

        // 게임 한 판 전체에서 공유되는 리롤 값
        private int globalRerollCount = 0;
        private int currentRerollCost;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);

            currentRerollCost = baseRerollCost;

            shopUIContainer.SetActive(false);

            closeButton.onClick.AddListener(CloseShop);

            if (rerollButton != null)
                rerollButton.onClick.AddListener(OnClickRerollItems);
        }
        private void Update()
        {
            if (shopUIContainer.activeSelf && Input.GetKeyDown(KeyCode.Escape))
            {
                CloseShop();
            }
        }

        public void OpenShop(MerchantNPC npc)
        {
            currentInteractingNPC = npc;
            shopUIContainer.SetActive(true);

            Time.timeScale = 0f;

            if (!npc.HasGeneratedShopItems())
            {
                List<MerchantItemBlueprint> generatedItems =
                    GenerateNewShopItems(false, globalRerollCount);

                npc.SetShopItems(generatedItems);
            }

            UpdateRerollCostText();
            DisplayCurrentShopItems();
        }

        private List<MerchantItemBlueprint> GenerateNewShopItems(bool useRerollBonus, int rerollCount)
        {
            List<MerchantItemBlueprint> generatedItems = new List<MerchantItemBlueprint>();

            int safetyCount = 0;

            while (generatedItems.Count < itemButtons.Count && safetyCount < 100)
            {
                safetyCount++;

                MerchantItemBlueprint.Rarity selectedRarity = useRerollBonus
                    ? GetRandomRarityWithRerollBonus(rerollCount)
                    : GetRandomRarity();

                MerchantItemBlueprint selectedItem = GetRandomItemByRarity(selectedRarity);

                if (selectedItem != null && !generatedItems.Contains(selectedItem))
                {
                    generatedItems.Add(selectedItem);
                }
            }

            if (generatedItems.Count < itemButtons.Count)
            {
                FillRemainingItemsRandomly(generatedItems);
            }

            return generatedItems;
        }

        private MerchantItemBlueprint.Rarity GetRandomRarity()
        {
            return RollRarity(commonWeight, uncommonWeight, rareWeight, legendaryWeight);
        }

        private MerchantItemBlueprint.Rarity GetRandomRarityWithRerollBonus(int rerollCount)
        {
            int rareBonus = Mathf.Min(rerollCount * rareBonusPerReroll, maxRareBonus);
            int legendaryBonus = Mathf.Min(rerollCount * legendaryBonusPerReroll, maxLegendaryBonus);

            int adjustedCommon = Mathf.Max(minCommonWeight, commonWeight - rareBonus - legendaryBonus);
            int adjustedUncommon = uncommonWeight;
            int adjustedRare = rareWeight + rareBonus;
            int adjustedLegendary = legendaryWeight + legendaryBonus;

            return RollRarity(adjustedCommon, adjustedUncommon, adjustedRare, adjustedLegendary);
        }

        private MerchantItemBlueprint.Rarity RollRarity(int common, int uncommon, int rare, int legendary)
        {
            int total = common + uncommon + rare + legendary;

            if (total <= 0)
                return MerchantItemBlueprint.Rarity.Common;

            int roll = Random.Range(0, total);

            if (roll < common)
                return MerchantItemBlueprint.Rarity.Common;

            roll -= common;

            if (roll < uncommon)
                return MerchantItemBlueprint.Rarity.Uncommon;

            roll -= uncommon;

            if (roll < rare)
                return MerchantItemBlueprint.Rarity.Rare;

            return MerchantItemBlueprint.Rarity.Legendary;
        }

        private MerchantItemBlueprint GetRandomItemByRarity(MerchantItemBlueprint.Rarity rarity)
        {
            List<MerchantItemBlueprint> candidates =
                allAvailableItems.FindAll(item => item.itemRarity == rarity);

            if (candidates.Count == 0)
                return null;

            return candidates[Random.Range(0, candidates.Count)];
        }

        private void FillRemainingItemsRandomly(List<MerchantItemBlueprint> targetItems)
        {
            List<MerchantItemBlueprint> remainingItems = new List<MerchantItemBlueprint>();

            foreach (MerchantItemBlueprint item in allAvailableItems)
            {
                if (!targetItems.Contains(item))
                    remainingItems.Add(item);
            }

            while (targetItems.Count < itemButtons.Count && remainingItems.Count > 0)
            {
                int randomIndex = Random.Range(0, remainingItems.Count);
                targetItems.Add(remainingItems[randomIndex]);
                remainingItems.RemoveAt(randomIndex);
            }
        }

        private void DisplayCurrentShopItems()
        {
            if (currentInteractingNPC == null)
                return;

            List<MerchantItemBlueprint> shopItems = currentInteractingNPC.GetShopItems();

            for (int i = 0; i < itemButtons.Count; i++)
            {
                if (i < shopItems.Count)
                {
                    itemButtons[i].gameObject.SetActive(true);
                    itemButtons[i].Setup(shopItems[i]);
                }
                else
                {
                    itemButtons[i].gameObject.SetActive(false);
                }
            }
        }

        public void OnClickRerollItems()
        {
            if (currentInteractingNPC == null)
                return;

            if (ProcessPayment(currentRerollCost))
            {
                globalRerollCount++;

                List<MerchantItemBlueprint> newItems =
                    GenerateNewShopItems(true, globalRerollCount);

                currentInteractingNPC.SetShopItems(newItems);

                PlayRerollSound();

                currentRerollCost = baseRerollCost + (rerollCostIncrease * globalRerollCount);

                UpdateRerollCostText();
                DisplayCurrentShopItems();

                Debug.Log($"[상점] 리롤 성공! 전체 리롤 횟수: {globalRerollCount}, 다음 리롤 비용: {currentRerollCost}G");
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
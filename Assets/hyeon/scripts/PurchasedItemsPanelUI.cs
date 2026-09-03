using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class PurchasedItemsPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform itemGrid;
        [SerializeField] private PurchasedItemCardUI itemCardPrefab;
        [SerializeField] private GameObject emptyText;

        [Header("Page")]
        [SerializeField] private int itemsPerPage = 4;
        [SerializeField] private int minimumPageCount = 2;
        [SerializeField] private bool loopPages = true;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI pageText;

        private SynergyManager synergyManager;
        private readonly List<PurchasedItemCardUI> spawnedCards = new();

        private readonly List<MerchantItemBlueprint> orderedItems = new();
        private readonly Dictionary<MerchantItemBlueprint, int> itemCounts = new();

        private int currentPage = 0;

        private void Awake()
        {
            SetupButton(prevButton, PrevPage);
            SetupButton(nextButton, NextPage);
        }

        private void OnEnable()
        {
            synergyManager = FindObjectOfType<SynergyManager>();

            if (synergyManager != null)
            {
                synergyManager.ItemsChanged -= Refresh;
                synergyManager.ItemsChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (synergyManager != null)
            {
                synergyManager.ItemsChanged -= Refresh;
            }
        }

        public void NextPage()
        {
            int maxPage = GetMaxPage();

            if (loopPages)
            {
                currentPage = currentPage >= maxPage ? 0 : currentPage + 1;
            }
            else
            {
                currentPage = Mathf.Min(currentPage + 1, maxPage);
            }

            Refresh();
        }

        public void PrevPage()
        {
            int maxPage = GetMaxPage();

            if (loopPages)
            {
                currentPage = currentPage <= 0 ? maxPage : currentPage - 1;
            }
            else
            {
                currentPage = Mathf.Max(currentPage - 1, 0);
            }

            Refresh();
        }

        public void Refresh()
        {
            if (synergyManager == null)
            {
                synergyManager = FindObjectOfType<SynergyManager>();
            }

            if (itemGrid == null || itemCardPrefab == null)
            {
                return;
            }

            ClearCards();
            BuildItemList();

            int totalCount = orderedItems.Count;
            int maxPage = GetMaxPage();

            currentPage = Mathf.Clamp(currentPage, 0, maxPage);

            if (emptyText != null)
            {
                emptyText.SetActive(totalCount == 0);
            }

            int startIndex = currentPage * itemsPerPage;
            int endIndex = Mathf.Min(startIndex + itemsPerPage, totalCount);

            for (int i = startIndex; i < endIndex; i++)
            {
                MerchantItemBlueprint item = orderedItems[i];

                PurchasedItemCardUI card = Instantiate(itemCardPrefab, itemGrid);
                card.Setup(item, itemCounts[item]);
                spawnedCards.Add(card);
            }

            RefreshPageUI(maxPage);
        }

        private void BuildItemList()
        {
            orderedItems.Clear();
            itemCounts.Clear();

            if (synergyManager == null)
            {
                return;
            }

            foreach (MerchantItemBlueprint item in synergyManager.OwnedItems)
            {
                if (item == null)
                {
                    continue;
                }

                if (!itemCounts.ContainsKey(item))
                {
                    itemCounts[item] = 0;
                    orderedItems.Add(item);
                }

                itemCounts[item]++;
            }
        }

        private int GetMaxPage()
        {
            int totalCount = orderedItems.Count;

            int contentPageCount = Mathf.CeilToInt(totalCount / (float)itemsPerPage);
            int pageCount = Mathf.Max(minimumPageCount, contentPageCount, 1);

            return pageCount - 1;
        }

        private void RefreshPageUI(int maxPage)
        {
            if (prevButton != null)
            {
                prevButton.gameObject.SetActive(true);
                prevButton.interactable = true;
            }

            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(true);
                nextButton.interactable = true;
            }

            if (pageText != null)
            {
                pageText.gameObject.SetActive(true);
                pageText.text = $"{currentPage + 1} / {maxPage + 1}";
            }
        }

        private void SetupButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.transition = Selectable.Transition.None;
            button.interactable = true;

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);

            if (button.targetGraphic != null)
            {
                Color color = button.targetGraphic.color;
                color.a = 1f;
                button.targetGraphic.color = color;
            }

            if (button.GetComponent<UIButtonPressEffect>() == null)
            {
                button.gameObject.AddComponent<UIButtonPressEffect>();
            }
        }

        private void ClearCards()
        {
            for (int i = spawnedCards.Count - 1; i >= 0; i--)
            {
                if (spawnedCards[i] != null)
                {
                    Destroy(spawnedCards[i].gameObject);
                }
            }

            spawnedCards.Clear();
        }
    }
}
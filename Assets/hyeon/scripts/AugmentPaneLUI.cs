using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    public class AugmentPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform augmentGrid;
        [SerializeField] private AugmentCardUI augmentCardPrefab;
        [SerializeField] private GameObject emptyText;

        [Header("Page")]
        [SerializeField] private int itemsPerPage = 5;
        [SerializeField] private int minimumPageCount = 2;
        [SerializeField] private bool loopPages = true;
        [SerializeField] private Button prevButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private TextMeshProUGUI pageText;

        private AugmentHistoryManager historyManager;
        private readonly List<AugmentCardUI> spawnedCards = new();

        private int currentPage = 0;

        private void Awake()
        {
            SetupButton(prevButton, PrevPage);
            SetupButton(nextButton, NextPage);
        }

        private void OnEnable()
        {
            historyManager = AugmentHistoryManager.Instance;

            if (historyManager != null)
            {
                historyManager.Changed -= Refresh;
                historyManager.Changed += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (historyManager != null)
            {
                historyManager.Changed -= Refresh;
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
            if (historyManager == null)
            {
                historyManager = AugmentHistoryManager.Instance;
            }

            if (augmentGrid == null || augmentCardPrefab == null)
            {
                return;
            }

            ClearCards();

            int totalCount = historyManager != null ? historyManager.Entries.Count : 0;
            int maxPage = GetMaxPage();

            currentPage = Mathf.Clamp(currentPage, 0, maxPage);

            if (emptyText != null)
            {
                emptyText.SetActive(totalCount == 0);
            }

            if (historyManager != null)
            {
                int startIndex = currentPage * itemsPerPage;
                int endIndex = Mathf.Min(startIndex + itemsPerPage, totalCount);

                for (int i = startIndex; i < endIndex; i++)
                {
                    AugmentHistoryManager.AugmentEntry entry = historyManager.Entries[i];

                    AugmentCardUI card = Instantiate(augmentCardPrefab, augmentGrid);
                    card.Setup(entry);
                    spawnedCards.Add(card);
                }
            }

            RefreshPageUI(maxPage);
        }

        private int GetMaxPage()
        {
            int totalCount = historyManager != null ? historyManager.Entries.Count : 0;

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
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class AugmentPanelUI : MonoBehaviour
    {
        [SerializeField] private Transform augmentGrid;
        [SerializeField] private AugmentCardUI augmentCardPrefab;
        [SerializeField] private GameObject emptyText;

        private AugmentHistoryManager historyManager;
        private readonly List<AugmentCardUI> spawnedCards = new();

        private void OnEnable()
        {
            historyManager = AugmentHistoryManager.Instance;

            if (historyManager != null)
            {
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

        public void Refresh()
        {
            if (historyManager == null)
            {
                historyManager = AugmentHistoryManager.Instance;
            }

            if (historyManager == null ||
                augmentGrid == null ||
                augmentCardPrefab == null)
            {
                return;
            }

            ClearCards();

            bool isEmpty = historyManager.Entries.Count == 0;

            if (emptyText != null)
            {
                emptyText.SetActive(isEmpty);
            }

            foreach (AugmentHistoryManager.AugmentEntry entry in historyManager.Entries)
            {
                AugmentCardUI card =
                    Instantiate(augmentCardPrefab, augmentGrid);

                card.Setup(entry);
                spawnedCards.Add(card);
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
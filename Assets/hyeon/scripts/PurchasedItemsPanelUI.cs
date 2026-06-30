using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class PurchasedItemsPanelUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Transform itemGrid;
        [SerializeField] private PurchasedItemCardUI itemCardPrefab;
        [SerializeField] private GameObject emptyText;

        private SynergyManager synergyManager;
        private readonly List<PurchasedItemCardUI> spawnedCards = new();

        private void OnEnable()
        {
            ResolveAndSubscribe();
            Refresh();
        }

        private void OnDisable()
        {
            if (synergyManager != null)
            {
                synergyManager.ItemsChanged -= Refresh;
            }
        }

        private void ResolveAndSubscribe()
        {
            if (synergyManager != null)
            {
                return;
            }

            synergyManager = SynergyManager.Instance != null
                ? SynergyManager.Instance
                : FindObjectOfType<SynergyManager>();

            if (synergyManager != null)
            {
                synergyManager.ItemsChanged -= Refresh;
                synergyManager.ItemsChanged += Refresh;
            }
        }

        public void Refresh()
        {
            ResolveAndSubscribe();

            if (synergyManager == null || itemGrid == null || itemCardPrefab == null)
            {
                return;
            }

            ClearCards();

            Dictionary<MerchantItemBlueprint, int> counts =
                new Dictionary<MerchantItemBlueprint, int>();

            List<MerchantItemBlueprint> order =
                new List<MerchantItemBlueprint>();

            foreach (MerchantItemBlueprint item in synergyManager.OwnedItems)
            {
                if (item == null)
                {
                    continue;
                }

                if (counts.ContainsKey(item))
                {
                    counts[item]++;
                }
                else
                {
                    counts.Add(item, 1);
                    order.Add(item);
                }
            }

            if (emptyText != null)
            {
                emptyText.SetActive(order.Count == 0);
            }

            foreach (MerchantItemBlueprint item in order)
            {
                PurchasedItemCardUI card =
                    Instantiate(itemCardPrefab, itemGrid);

                card.Setup(item, counts[item]);
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
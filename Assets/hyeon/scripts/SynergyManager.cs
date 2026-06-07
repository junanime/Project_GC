using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class SynergyManager : MonoBehaviour
    {
        public static SynergyManager Instance;

        private List<MerchantItemBlueprint> ownedItems = new List<MerchantItemBlueprint>();
        private HashSet<ItemTag> activatedSynergies = new HashSet<ItemTag>();

        private void Awake()
        {
            Instance = this;
        }

        public void AddItem(MerchantItemBlueprint item)
        {
            ownedItems.Add(item);

            Debug.Log($"[시너지] {item.itemName} 획득! 태그 : {item.itemTag}");

            CheckTagCounts();
        }

        private void CheckTagCounts()
        {
            int supplement = CountTag(ItemTag.영양제);
            int medicine = CountTag(ItemTag.의약품);
            int food = CountTag(ItemTag.음식);
            int hygiene = CountTag(ItemTag.위생);
            int utility = CountTag(ItemTag.유틸리티);

            Debug.Log($"영양제:{supplement} 의약품:{medicine} 음식:{food} 위생:{hygiene} 유틸:{utility}");

            TryActivateSynergy(ItemTag.영양제, supplement, "건강 마니아");
            TryActivateSynergy(ItemTag.의약품, medicine, "약물 과다");
            TryActivateSynergy(ItemTag.음식, food, "자극적인 맛");
            TryActivateSynergy(ItemTag.위생, hygiene, "위생 전문가");
            TryActivateSynergy(ItemTag.유틸리티, utility, "집중 케어");
        }

        private int CountTag(ItemTag tag)
        {
            int count = 0;

            foreach (var item in ownedItems)
            {
                if (item.itemTag == tag)
                {
                    count++;
                }
            }

            return count;
        }

        private void TryActivateSynergy(ItemTag tag, int count, string synergyName)
        {
            if (count >= 3 && !activatedSynergies.Contains(tag))
            {
                activatedSynergies.Add(tag);
                Debug.Log($"<color=magenta>[시너지 발동]</color> {synergyName} 활성화!");
            }
        }
    }
}
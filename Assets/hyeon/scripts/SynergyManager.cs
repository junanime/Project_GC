using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class SynergyManager : MonoBehaviour
    {
        public static SynergyManager Instance;

        private Character player;

        private List<MerchantItemBlueprint> ownedItems = new List<MerchantItemBlueprint>();
        private HashSet<ItemTag> activatedSynergies = new HashSet<ItemTag>();

        private List<string> activeSynergyNames = new List<string>();

        public List<string> ActiveSynergyNames
        {
            get { return activeSynergyNames; }
        }

        private void Awake()
        {
            Instance = this;

            player = GetComponent<Character>();

            if (player == null)
            {
                player = FindObjectOfType<Character>();
            }

            if (player == null)
            {
                Debug.LogWarning("[시너지] Character를 찾지 못했습니다.");
            }
        }

        public void AddItem(MerchantItemBlueprint item)
        {
            if (item == null) return;

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
                activeSynergyNames.Add(synergyName);

                Debug.Log($"<color=magenta>[시너지 발동]</color> {synergyName} 활성화!");

                ApplySynergyEffect(tag);
            }
        }

        private void ApplySynergyEffect(ItemTag tag)
        {
            if (player == null)
            {
                Debug.LogWarning("[시너지] Character가 없어 시너지 효과를 적용하지 못했습니다.");
                return;
            }

            switch (tag)
            {
                case ItemTag.영양제:
                    player.AddMaxHealthBonus(20f);
                    player.GainHealth(20f);
                    Debug.Log("<color=green>[건강 마니아]</color> 최대 체력 +20 적용!");
                    break;

                case ItemTag.의약품:
                    player.AddDamageMultiplier(0.3f);
                    player.AddMoveSpeedBoost(-0.05f);
                    Debug.Log("<color=red>[약물 과다]</color> 공격력 +30%, 이동속도 -0.05 적용!");
                    break;

                case ItemTag.음식:
                    player.AddBurnChance(0.2f);
                    Debug.Log("<color=orange>[자극적인 맛]</color> 화상 확률 +20% 적용!");
                    break;

                case ItemTag.위생:
                    player.EnableShield();
                    Debug.Log("<color=cyan>[위생 전문가]</color> 1회용 보호막 적용!");
                    break;

                case ItemTag.유틸리티:
                    player.AddProjectileSize(0.1f);
                    Debug.Log("<color=blue>[집중 케어]</color> 투사체 크기 +10% 적용!");
                    break;
            }
        }
    }
}
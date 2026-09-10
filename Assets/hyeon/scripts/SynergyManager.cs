using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public class SynergyManager : MonoBehaviour
    {
        public static SynergyManager Instance;


        private Character player;


        // =========================================================
        // Owned Items
        // =========================================================

        private List<MerchantItemBlueprint> ownedItems =
            new List<MerchantItemBlueprint>();

        public IReadOnlyList<MerchantItemBlueprint> OwnedItems =>
            ownedItems;

        public event System.Action ItemsChanged;


        // =========================================================
        // Synergy
        // =========================================================

        // 이미 발동된 시너지 체크용
        private HashSet<ItemTag> activatedSynergies =
            new HashSet<ItemTag>();


        // UI 등에 표시하기 위한 시너지 이름 목록
        private List<string> activeSynergyNames =
            new List<string>();

        public List<string> ActiveSynergyNames
        {
            get { return activeSynergyNames; }
        }


        // 시너지가 발동된 순서를 저장
        // 대표 시너지 동률 처리에 사용
        private List<ItemTag> activeSynergyTags =
            new List<ItemTag>();


        // =========================================================
        // Unity
        // =========================================================

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
                Debug.LogWarning(
                    "[시너지] Character를 찾지 못했습니다."
                );
            }
        }


        // =========================================================
        // Add Item
        // =========================================================

        public void AddItem(MerchantItemBlueprint item)
        {
            if (item == null)
                return;


            ownedItems.Add(item);


            Debug.Log(
                $"[시너지] {item.itemName} 획득! 태그 : {item.itemTag}"
            );


            CheckTagCounts();


            ItemsChanged?.Invoke();
        }


        // =========================================================
        // Tag Count Check
        // =========================================================

        private void CheckTagCounts()
        {
            int supplement =
                CountTag(ItemTag.영양제);

            int medicine =
                CountTag(ItemTag.의약품);

            int food =
                CountTag(ItemTag.음식);

            int hygiene =
                CountTag(ItemTag.위생);

            int utility =
                CountTag(ItemTag.유틸리티);


            Debug.Log(
                $"영양제:{supplement} " +
                $"의약품:{medicine} " +
                $"음식:{food} " +
                $"위생:{hygiene} " +
                $"유틸:{utility}"
            );


            TryActivateSynergy(
                ItemTag.영양제,
                supplement,
                "건강 마니아"
            );

            TryActivateSynergy(
                ItemTag.의약품,
                medicine,
                "약물 과다"
            );

            TryActivateSynergy(
                ItemTag.음식,
                food,
                "자극적인 맛"
            );

            TryActivateSynergy(
                ItemTag.위생,
                hygiene,
                "위생 전문가"
            );

            TryActivateSynergy(
                ItemTag.유틸리티,
                utility,
                "집중 케어"
            );
        }


        // =========================================================
        // Count Tag
        // =========================================================

        private int CountTag(ItemTag tag)
        {
            int count = 0;


            foreach (var item in ownedItems)
            {
                if (item == null)
                    continue;


                if (item.itemTag == tag)
                {
                    count++;
                }
            }


            return count;
        }


        // =========================================================
        // Activate Synergy
        // =========================================================

        private void TryActivateSynergy(
            ItemTag tag,
            int count,
            string synergyName
        )
        {
            if (count < 3)
                return;


            if (activatedSynergies.Contains(tag))
                return;


            activatedSynergies.Add(tag);

            activeSynergyNames.Add(synergyName);

            // 발동 순서 저장
            activeSynergyTags.Add(tag);


            Debug.Log(
                $"<color=magenta>[시너지 발동]</color> " +
                $"{synergyName} 활성화!"
            );


            ApplySynergyEffect(tag);
        }


        // =========================================================
        // Get Dominant Synergy
        //
        // 결과화면의 "최종 빌드 이름"으로 사용
        // =========================================================

        public string GetDominantSynergyName()
        {
            int supplement =
                CountTag(ItemTag.영양제);

            int medicine =
                CountTag(ItemTag.의약품);

            int food =
                CountTag(ItemTag.음식);

            int hygiene =
                CountTag(ItemTag.위생);

            int utility =
                CountTag(ItemTag.유틸리티);


            int maxCount = Mathf.Max(
                supplement,
                medicine,
                food,
                hygiene,
                utility
            );


            // 시너지 발동 기준이 3개이므로
            // 최대 개수가 3 미만이면 시너지 없음
            if (maxCount < 3)
            {
                return "시너지 없음";
            }


            // =====================================================
            // 동률이면 먼저 발동한 시너지 우선
            // =====================================================

            foreach (ItemTag tag in activeSynergyTags)
            {
                int count = CountTag(tag);


                if (count == maxCount)
                {
                    return GetSynergyName(tag);
                }
            }


            // 혹시 발동 기록이 없는 예외 상황이 생긴 경우
            // 현재 개수가 가장 높은 태그를 기준으로 반환
            if (supplement == maxCount)
                return GetSynergyName(ItemTag.영양제);

            if (medicine == maxCount)
                return GetSynergyName(ItemTag.의약품);

            if (food == maxCount)
                return GetSynergyName(ItemTag.음식);

            if (hygiene == maxCount)
                return GetSynergyName(ItemTag.위생);

            if (utility == maxCount)
                return GetSynergyName(ItemTag.유틸리티);


            return "시너지 없음";
        }


        // =========================================================
        // Get Synergy Name
        // =========================================================

        private string GetSynergyName(ItemTag tag)
        {
            switch (tag)
            {
                case ItemTag.영양제:
                    return "건강 마니아";

                case ItemTag.의약품:
                    return "약물 과다";

                case ItemTag.음식:
                    return "자극적인 맛";

                case ItemTag.위생:
                    return "위생 전문가";

                case ItemTag.유틸리티:
                    return "집중 케어";

                default:
                    return "시너지 없음";
            }
        }


        // =========================================================
        // Apply Synergy Effect
        // =========================================================

        private void ApplySynergyEffect(ItemTag tag)
        {
            if (player == null)
            {
                Debug.LogWarning(
                    "[시너지] Character가 없어 " +
                    "시너지 효과를 적용하지 못했습니다."
                );

                return;
            }


            switch (tag)
            {
                // =================================================
                // 영양제
                // =================================================

                case ItemTag.영양제:

                    player.AddMaxHealthBonus(20f);
                    player.GainHealth(20f);

                    Debug.Log(
                        "<color=green>[건강 마니아]</color> " +
                        "최대 체력 +20 적용!"
                    );

                    break;


                // =================================================
                // 의약품
                // =================================================

                case ItemTag.의약품:

                    player.AddDamageMultiplier(0.3f);
                    player.AddMoveSpeedBoost(-0.05f);

                    Debug.Log(
                        "<color=red>[약물 과다]</color> " +
                        "공격력 +30%, 이동속도 -0.05 적용!"
                    );

                    break;


                // =================================================
                // 음식
                // =================================================

                case ItemTag.음식:

                    player.AddBurnChance(0.2f);

                    Debug.Log(
                        "<color=orange>[자극적인 맛]</color> " +
                        "화상 확률 +20% 적용!"
                    );

                    break;


                // =================================================
                // 위생
                // =================================================

                case ItemTag.위생:

                    player.EnableShield();

                    Debug.Log(
                        "<color=cyan>[위생 전문가]</color> " +
                        "1회용 보호막 적용!"
                    );

                    break;


                // =================================================
                // 유틸리티
                // =================================================

                case ItemTag.유틸리티:

                    player.AddProjectileSize(0.1f);

                    Debug.Log(
                        "<color=blue>[집중 케어]</color> " +
                        "투사체 크기 +10% 적용!"
                    );

                    break;
            }
        }
    }
}
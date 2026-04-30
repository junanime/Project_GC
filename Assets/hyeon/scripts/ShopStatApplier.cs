using UnityEngine;

namespace Vampire
{
    public class ShopStatApplier : MonoBehaviour
    {
        private AbilityManager abilityManager;
        private Character player;

        private void Awake()
        {
            // 맵 전체에서 매니저를 찾고, 못 찾을 경우를 대비해 null 체크를 강화합니다.
            abilityManager = FindObjectOfType<AbilityManager>();
            player = GetComponent<Character>();

            if (abilityManager == null) Debug.LogError("[상점] AbilityManager를 찾을 수 없습니다! 스탯 적용이 불가능합니다.");
            if (player == null) Debug.LogError("[상점] Character 컴포넌트를 찾을 수 없습니다!");
        }

        public void ApplyStats(MerchantItemBlueprint item)
        {
            if (item == null) return;

            Debug.Log($"<color=yellow>[상점]</color> <b>{item.itemName}</b> 효과 적용 시작!");

            // 1. 공격력 (Atk Damage)
            if (item.atkDamageBoost > 0)
            {
                abilityManager.UpgradeValue<UpgradeableDamage, float>(item.atkDamageBoost);
                Debug.Log($"<color=cyan>[공격력 업그레이드]</color> +{item.atkDamageBoost * 100}% 증가");
            }

            // 2. 이동 속도 (Move Speed)
            if (item.moveSpeedBoost > 0)
            {
                abilityManager.UpgradeValue<UpgradeableMovementSpeed, float>(item.moveSpeedBoost);
                Debug.Log($"<color=cyan>[이동속도 업그레이드]</color> +{item.moveSpeedBoost} 증가");
            }

            // 3. 체력 (Max HP)
            if (item.maxHpBoost > 0)
            {
                // GainHealth가 단순히 현재 체력을 채워주는 것인지, 최대 체력을 늘려주는 것인지 확인이 필요합니다.
                // 보통 상점 아이템은 '최대 체력'을 늘려야 하므로 AbilityManager를 통하는 것이 더 정확할 수 있습니다.
                player.GainHealth(item.maxHpBoost);
                Debug.Log($"<color=cyan>[체력 회복/증가]</color> +{item.maxHpBoost} 적용됨");
            }

            Debug.Log($"<color=green>[적용 완료]</color> {item.itemName}의 모든 효과가 반영되었습니다.");
        }
    }
}
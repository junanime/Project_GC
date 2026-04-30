using UnityEngine;

namespace Vampire
{
    public class ShopItemReceiver : MonoBehaviour
    {
        private Character player;
        private AbilityManager abilityManager;
        private EntityManager entityManager;

        private void Awake()
        {
            player = GetComponent<Character>();
        }

        private void Start()
        {
            abilityManager = FindObjectOfType<AbilityManager>();
            entityManager = FindObjectOfType<EntityManager>();
        }

        public void ReceiveItem(MerchantItemBlueprint item)
        {
            if (item == null) return;

            // 1. 무기나 특수 능력 프리팹이 있다면 장착!
            if (item.abilityPrefab != null)
            {
                Ability newAbility = Instantiate(item.abilityPrefab, transform).GetComponent<Ability>();
                newAbility.Init(abilityManager, entityManager, player);
                newAbility.Select();
            }

            // 2.  이름 수정 완료: addHp -> maxHpBoost
            if (item.maxHpBoost > 0)
            {
                player.GainHealth(item.maxHpBoost);
                Debug.Log($"[상점] {item.itemName} 효과로 체력 {item.maxHpBoost} 상승!");
            }
        }
    }
}
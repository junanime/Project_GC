using UnityEngine;

namespace Vampire
{
    [CreateAssetMenu(fileName = "New Merchant Item", menuName = "Vampire/Merchant Item")]
    public class MerchantItemBlueprint : ScriptableObject
    {
        public string itemName;
        public enum Rarity { Common, Uncommon, Rare, Legendary }
        public Rarity itemRarity;

        [TextArea] public string description;
        public Sprite itemIcon;
        public int cost;

        //  이 줄이 빠져있어서 에러가 났던 겁니다! 다시 추가해 주세요.
        [Header("Ability Reward (무기나 특수 능력 프리팹)")]
        public GameObject abilityPrefab;

        [Header("Item Stats (직접 수치 입력)")]
        public float atkSpeedBoost;
        public float atkDamageBoost;
        public float maxHpBoost;
        public float moveSpeedBoost;
        public float armorBoost;
        public float luckBoost;
    }
}
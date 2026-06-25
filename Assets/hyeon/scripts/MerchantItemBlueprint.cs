using UnityEngine;

namespace Vampire
{
    public enum ItemTag { 영양제, 의약품, 음식, 위생, 유틸리티 }

    [CreateAssetMenu(fileName = "New Merchant Item", menuName = "Vampire/Merchant Item")]
    public class MerchantItemBlueprint : ScriptableObject
    {
        [Header("기본 정보")]
        public string itemName;
        public enum Rarity { Common, Uncommon, Rare, Legendary }
        public Rarity itemRarity;
        public ItemTag itemTag;

        [TextArea] public string description;
        public Sprite itemIcon;
        public int cost;

        [Header("Lobby Shop")]
        public bool canBuyInLobby = true;
        public int silverCost = 100;

        //  복구됨: 무기나 특수 능력을 부여할 때 쓰는 프리팹
        [Header("Ability Reward (무기/특수 능력 프리팹)")]
        public GameObject abilityPrefab;

        [Header("1. 기초 스탯 (Common / Uncommon)")]
        [Range(-1f, 2f)] public float atkSpeedBoost;
        [Range(-1f, 2f)] public float atkDamageBoost;
        public float maxHpBoost;
        public float moveSpeedBoost;
        [Range(0, 1f)] public float critBoost;
        public float magnetBoost;
        [Range(0, 1f)] public float expBoost;
        [Range(0, 2f)] public float projSpeedBoost;

        //  복구됨: 운 스탯
        [Range(0, 1f)] public float luckBoost;

        [Header("2. 유틸리티 & 무기 스탯 (Uncommon / Rare)")]
        [Range(0, 2f)] public float sizeBoost;
        public float rangeBoost;
        public int pierceCountBoost;
        public int extraProjectiles;

        [Header("3. 상태 이상 & 오라 (시스템 구현 예정)")]
        [Range(0, 1f)] public float burnChance;
        [Range(0, 1f)] public float slowChance;
        public float statusDurationBoost;
        public float auraDamagePerSecond;

        [Header("4. 트리거 & 흡혈 효과 (시스템 구현 예정)")]
        public float healOnKill;
        [Range(0, 1f)] public float summonOnKillChance;
        public float healOnIdlePerSecond;
        [Range(0, 1f)] public float lifeSteal;
        public bool knockbackOnHit;

        [Header("5. 특수 기능 플래그 (Rare / Legendary)")]
        public bool giveShield;
        public int extraRevives;
        public float invincibilityBoost;
        public bool autoCollectItems;
        public bool infiniteDash;
        public bool reflectProjectile;
        public bool explosiveAttacks;
    }
}
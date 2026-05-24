using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Vampire
{
    public abstract class Ability : MonoBehaviour
    {
        public enum Rarity
        {
            Common = 50,
            Uncommon = 25,
            Rare = 15,
            Legendary = 9,
            Exotic = 1
        }

        public enum AugmentTier
        {
            General,
            Special,
            Legendary
        }

        [Header("Ability Details")]
        [SerializeField] protected Sprite image;
        [SerializeField] protected LocalizedString localizedName;
        [SerializeField] protected LocalizedString localizedDescription;
        [SerializeField] protected Rarity rarity = Rarity.Common;

        [Header("Augment Tier")]
        [SerializeField] protected AugmentTier augmentTier = AugmentTier.General;

        [Header("Selection Rules")]
        [Tooltip("이미 보유한 Ability가 레벨업/증강 선택지에 다시 등장할지 여부입니다. 시작 무기처럼 장착만 하고 선택지에는 안 띄우려면 꺼두세요.")]
        [SerializeField] protected bool canAppearAsOwnedUpgrade = true;

        protected AbilityManager abilityManager;
        protected EntityManager entityManager;
        protected Character playerCharacter;
        protected List<IUpgradeableValue> upgradeableValues;

        protected int level = 0;
        protected int maxLevel;
        protected bool owned = false;

        public int Level => level;
        public bool Owned => owned;
        public Sprite Image => image;
        public string Name => localizedName.GetLocalizedString();
        public float DropWeight => (float)rarity;
        public AugmentTier Tier => augmentTier;

        public bool CanAppearAsOwnedUpgrade => canAppearAsOwnedUpgrade;

        public virtual string Description
        {
            get
            {
                if (!owned)
                {
                    return localizedDescription.GetLocalizedString();
                }
                else
                {
                    return GetUpgradeDescriptions();
                }
            }
        }

        public virtual void Init(AbilityManager abilityManager, EntityManager entityManager, Character playerCharacter)
        {
            this.abilityManager = abilityManager;
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;

            upgradeableValues = this.GetType()
                .GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public)
                .Where(fi => typeof(IUpgradeableValue).IsAssignableFrom(fi.FieldType))
                .Select(fi => fi.GetValue(this) as IUpgradeableValue)
                .Where(x => x != null)
                .ToList();

            upgradeableValues.ForEach(x => abilityManager.RegisterUpgradeableValue(x));

            if (upgradeableValues.Count > 0)
            {
                maxLevel = upgradeableValues.Max(x => x.UpgradeCount) + 1;
            }
            else
            {
                maxLevel = 1;
            }
        }

        public virtual void Select()
        {
            if (!owned)
            {
                owned = true;
                Use();
            }
            else
            {
                Upgrade();
            }

            level++;
        }

        protected virtual void Use()
        {
            upgradeableValues.ForEach(x => x.RegisterInUse());
        }

        protected virtual void Upgrade()
        {
            upgradeableValues.ForEach(x => x.Upgrade());
        }

        public virtual bool RequirementsMet()
        {
            return level < maxLevel;
        }

        protected string GetUpgradeDescriptions()
        {
            string description = "";
            upgradeableValues.ForEach(x => description += x.GetUpgradeDescription());
            return description;
        }
    }
}
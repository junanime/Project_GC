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


        // =========================================================
        // Ability Details
        // =========================================================

        [Header("Ability Details")]
        [SerializeField] protected Sprite image;
        [SerializeField] protected LocalizedString localizedName;
        [SerializeField] protected LocalizedString localizedDescription;
        [SerializeField] protected Rarity rarity = Rarity.Common;


        // =========================================================
        // Augment Tier
        // =========================================================

        [Header("Augment Tier")]
        [SerializeField] protected AugmentTier augmentTier = AugmentTier.General;


        // =========================================================
        // Selection Rules
        // =========================================================

        [Header("Selection Rules")]
        [Tooltip(
            "이미 보유한 Ability가 레벨업/증강 선택지에 다시 등장할지 여부입니다. " +
            "시작 무기처럼 장착만 하고 선택지에는 안 띄우려면 꺼두세요."
        )]
        [SerializeField] protected bool canAppearAsOwnedUpgrade = true;


        // =========================================================
        // References
        // =========================================================

        protected AbilityManager abilityManager;
        protected EntityManager entityManager;
        protected Character playerCharacter;

        protected List<IUpgradeableValue> upgradeableValues;


        // =========================================================
        // State
        // =========================================================

        protected int level = 0;
        protected int maxLevel;
        protected bool owned = false;


        // =========================================================
        // Properties
        // =========================================================

        public int Level => level;

        public bool Owned => owned;

        public virtual Sprite Image => image;

        public virtual string Name
        {
            get
            {
                if (localizedName == null || localizedName.IsEmpty)
                {
                    return gameObject.name;
                }

                string result = localizedName.GetLocalizedString();

                if (string.IsNullOrWhiteSpace(result) ||
                    result.StartsWith("No translation found"))
                {
                    return gameObject.name;
                }

                return result;
            }
        }

        public float DropWeight =>
            (float)rarity;

        public AugmentTier Tier =>
            augmentTier;

        public bool CanAppearAsOwnedUpgrade =>
            canAppearAsOwnedUpgrade;


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


        // =========================================================
        // Initialize
        // =========================================================

        public virtual void Init(
            AbilityManager abilityManager,
            EntityManager entityManager,
            Character playerCharacter
        )
        {
            this.abilityManager = abilityManager;
            this.entityManager = entityManager;
            this.playerCharacter = playerCharacter;


            upgradeableValues = this.GetType()
                .GetFields(
                    BindingFlags.NonPublic |
                    BindingFlags.Instance |
                    BindingFlags.Public
                )
                .Where(
                    fi =>
                        typeof(IUpgradeableValue)
                            .IsAssignableFrom(fi.FieldType)
                )
                .Select(
                    fi =>
                        fi.GetValue(this) as IUpgradeableValue
                )
                .Where(x => x != null)
                .ToList();


            upgradeableValues.ForEach(
                x => abilityManager.RegisterUpgradeableValue(x)
            );


            if (upgradeableValues.Count > 0)
            {
                maxLevel =
                    upgradeableValues.Max(
                        x => x.UpgradeCount
                    ) + 1;
            }
            else
            {
                maxLevel = 1;
            }
        }


        // =========================================================
        // Select
        // =========================================================

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


        // =========================================================
        // Use
        // =========================================================

        protected virtual void Use()
        {
            upgradeableValues.ForEach(
                x => x.RegisterInUse()
            );
        }


        // =========================================================
        // Upgrade
        // =========================================================

        protected virtual void Upgrade()
        {
            upgradeableValues.ForEach(
                x => x.Upgrade()
            );
        }


        // =========================================================
        // Requirements
        // =========================================================

        public virtual bool RequirementsMet()
        {
            return level < maxLevel;
        }


        // =========================================================
        // Damage Report
        // =========================================================

        /// <summary>
        /// 이 Ability 자신의 이름으로 피해를 기록합니다.
        ///
        /// 예:
        /// ReportDamage(20);
        ///
        /// → 현재 Ability의 Name으로 20 피해 기록
        /// </summary>
        protected void ReportDamage(float damage)
        {
            ReportDamage(
                Name,
                damage
            );
        }


        /// <summary>
        /// 지정한 증강 이름으로 피해를 기록합니다.
        ///
        /// 예:
        /// ReportDamage("침샷건", 20);
        /// ReportDamage("대물침", 100);
        ///
        /// 1. Character.OnDealDamage를 호출하여
        ///    기존 StatsManager 등의 총 피해량 시스템을 유지합니다.
        ///
        /// 2. AugmentDamageTracker에
        ///    지정한 증강 이름과 피해량을 별도로 기록합니다.
        /// </summary>
        protected void ReportDamage(
            string damageSourceName,
            float damage
        )
        {
            if (damage <= 0f)
            {
                return;
            }


            // =====================================================
            // 기존 전체 피해량 기록
            // =====================================================

            if (playerCharacter != null)
            {
                playerCharacter.OnDealDamage.Invoke(
                    damage
                );
            }


            // =====================================================
            // 증강별 피해량 기록
            // =====================================================

            if (AugmentDamageTracker.Instance != null)
            {
                string sourceName =
                    string.IsNullOrWhiteSpace(damageSourceName)
                        ? Name
                        : damageSourceName;


                AugmentDamageTracker.Instance.RecordDamage(
                    sourceName,
                    damage
                );
            }
        }


        // =========================================================
        // Upgrade Description
        // =========================================================

        protected string GetUpgradeDescriptions()
        {
            string description = "";

            upgradeableValues.ForEach(
                x => description += x.GetUpgradeDescription()
            );

            return description;
        }
    }
}
using System;
using UnityEngine;

namespace Vampire
{
    // Separate from Ability.AugmentTier and Ability.Rarity: Legendary here is
    // numeric upgrade quality, never permission to award a legendary ability.
    public enum AugmentUpgradeGrade
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Original = 3,
        Legendary = 4,
        Supreme = 5
    }

    [Serializable]
    public sealed class AugmentUpgradeOdds
    {
        // Integer basis points avoid rounding the confirmed percentages again.
        [SerializeField, Min(0)] private int common = 3437;
        [SerializeField, Min(0)] private int rare = 2749;
        [SerializeField, Min(0)] private int epic = 1718;
        [SerializeField, Min(0)] private int original = 1400;
        [SerializeField, Min(0)] private int legendary = 516;
        [SerializeField, Min(0)] private int supreme = 180;

        public int Weight(AugmentUpgradeGrade grade)
        {
            switch (grade)
            {
                case AugmentUpgradeGrade.Common: return Math.Max(0, common);
                case AugmentUpgradeGrade.Rare: return Math.Max(0, rare);
                case AugmentUpgradeGrade.Epic: return Math.Max(0, epic);
                case AugmentUpgradeGrade.Original: return Math.Max(0, original);
                case AugmentUpgradeGrade.Legendary: return Math.Max(0, legendary);
                case AugmentUpgradeGrade.Supreme: return Math.Max(0, supreme);
                default: throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }

        // Call once per displayed slot, including rerolls. A false result means
        // no valid reward, not an implicit fallback to an unowned parent or legend.
        public bool TryRoll(double unitSample, Func<AugmentUpgradeGrade, bool> isEligible,
            out AugmentUpgradeGrade grade)
        {
            if (double.IsNaN(unitSample) || unitSample < 0 || unitSample >= 1)
                throw new ArgumentOutOfRangeException(nameof(unitSample), "Expected [0, 1).");
            if (isEligible == null) throw new ArgumentNullException(nameof(isEligible));

            // Evaluate eligibility once, so callbacks cannot change the pool
            // between calculating its total and resolving the chosen interval.
            long total = 0;
            var weights = new int[6];
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = isEligible((AugmentUpgradeGrade)i) ? Weight((AugmentUpgradeGrade)i) : 0;
                total += weights[i];
            }
            grade = AugmentUpgradeGrade.Common;
            if (total == 0) return false;

            double roll = unitSample * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (roll < weights[i])
                {
                    grade = (AugmentUpgradeGrade)i;
                    return true;
                }
                roll -= weights[i];
            }
            throw new InvalidOperationException("Valid weighted sample did not resolve.");
        }

        public static bool IsNumeric(AugmentUpgradeGrade grade)
        {
            return grade >= AugmentUpgradeGrade.Common && grade <= AugmentUpgradeGrade.Supreme
                && grade != AugmentUpgradeGrade.Original;
        }

        public static string DisplayName(AugmentUpgradeGrade grade)
        {
            switch (grade)
            {
                case AugmentUpgradeGrade.Common: return "평범";
                case AugmentUpgradeGrade.Rare: return "희귀";
                case AugmentUpgradeGrade.Epic: return "영웅";
                case AugmentUpgradeGrade.Original: return "오리지널";
                case AugmentUpgradeGrade.Legendary: return "전설";
                case AugmentUpgradeGrade.Supreme: return "지존";
                default: throw new ArgumentOutOfRangeException(nameof(grade));
            }
        }
    }
}

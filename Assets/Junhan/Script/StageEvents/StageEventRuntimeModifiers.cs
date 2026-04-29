using UnityEngine;

namespace Vampire
{
    public static class StageEventRuntimeModifiers
    {
        public static float CoinDropAttemptMultiplier { get; private set; } = 1f;
        public static float CoinValueMultiplier { get; private set; } = 1f;
        public static int MaxCoinDropAttempts { get; private set; } = 1;

        public static float AdditionalCoinDropChance { get; private set; } = 0f;
        public static int AdditionalCoinDropCount { get; private set; } = 0;
        public static CoinType AdditionalCoinType { get; private set; } = CoinType.Bronze1;

        public static bool DebugGoldRush { get; private set; } = false;

        public static bool GoldRushActive
        {
            get
            {
                return CoinDropAttemptMultiplier > 1f
                    || CoinValueMultiplier > 1f
                    || AdditionalCoinDropChance > 0f
                    || AdditionalCoinDropCount > 0;
            }
        }

        public static void ResetCoinModifiers()
        {
            CoinDropAttemptMultiplier = 1f;
            CoinValueMultiplier = 1f;
            MaxCoinDropAttempts = 1;

            AdditionalCoinDropChance = 0f;
            AdditionalCoinDropCount = 0;
            AdditionalCoinType = CoinType.Bronze1;

            DebugGoldRush = false;
        }

        public static void ApplyGoldRushModifier(
            float dropAttemptMultiplier,
            float valueMultiplier,
            int maxDropAttempts,
            float additionalCoinDropChance,
            int additionalCoinDropCount,
            CoinType additionalCoinType,
            bool debugGoldRush)
        {
            CoinDropAttemptMultiplier = Mathf.Max(CoinDropAttemptMultiplier, dropAttemptMultiplier);
            CoinValueMultiplier = Mathf.Max(CoinValueMultiplier, valueMultiplier);
            MaxCoinDropAttempts = Mathf.Max(MaxCoinDropAttempts, maxDropAttempts);

            AdditionalCoinDropChance = Mathf.Max(AdditionalCoinDropChance, additionalCoinDropChance);
            AdditionalCoinDropCount = Mathf.Max(AdditionalCoinDropCount, additionalCoinDropCount);
            AdditionalCoinType = additionalCoinType;

            DebugGoldRush = DebugGoldRush || debugGoldRush;
        }

        public static int GetCoinDropAttemptCount()
        {
            float multiplier = Mathf.Max(1f, CoinDropAttemptMultiplier);

            int guaranteedAttempts = Mathf.FloorToInt(multiplier);
            float fractionalChance = multiplier - guaranteedAttempts;

            int attempts = guaranteedAttempts;

            if (Random.value < fractionalChance)
            {
                attempts++;
            }

            return Mathf.Clamp(attempts, 1, Mathf.Max(1, MaxCoinDropAttempts));
        }

        public static bool ShouldDropAdditionalCoin()
        {
            if (!GoldRushActive)
            {
                return false;
            }

            if (AdditionalCoinDropCount <= 0)
            {
                return false;
            }

            return Random.value <= Mathf.Clamp01(AdditionalCoinDropChance);
        }

        public static int ApplyCoinValueMultiplier(int baseValue)
        {
            float multiplier = Mathf.Max(1f, CoinValueMultiplier);
            return Mathf.Max(1, Mathf.RoundToInt(baseValue * multiplier));
        }
    }
}
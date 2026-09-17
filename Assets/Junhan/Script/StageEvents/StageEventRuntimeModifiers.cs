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

        public static bool ForceGoldRushCoinDrop { get; private set; } = false;
        public static CoinType ForcedGoldRushCoinType { get; private set; } = CoinType.Gold5;
        public static int ForcedGoldRushCoinCount { get; private set; } = 0;
        public static bool ForceGoldRushOnlyNormalMonsters { get; private set; } = true;
        public static bool SuppressOriginalCoinDropsDuringGoldRush { get; private set; } = true;

        public static bool DebugGoldRush { get; private set; } = false;

        public static bool GoldRushActive
        {
            get
            {
                if (MiniStageRuntimeState.IsInsideMiniStage)
                {
                    return false;
                }

                return CoinDropAttemptMultiplier > 1f ||
                       CoinValueMultiplier > 1f ||
                       AdditionalCoinDropChance > 0f ||
                       AdditionalCoinDropCount > 0 ||
                       ForceGoldRushCoinDrop;
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

            ForceGoldRushCoinDrop = false;
            ForcedGoldRushCoinType = CoinType.Gold5;
            ForcedGoldRushCoinCount = 0;
            ForceGoldRushOnlyNormalMonsters = true;
            SuppressOriginalCoinDropsDuringGoldRush = true;

            DebugGoldRush = false;
        }

        public static void ApplyGoldRushForcedCoinDrop(
            CoinType forcedCoinType,
            int forcedCoinCount,
            bool onlyNormalMonsters,
            bool suppressOriginalCoinDrops,
            bool debugGoldRush)
        {
            ForceGoldRushCoinDrop = true;
            ForcedGoldRushCoinType = forcedCoinType;
            ForcedGoldRushCoinCount = Mathf.Max(1, forcedCoinCount);
            ForceGoldRushOnlyNormalMonsters = onlyNormalMonsters;
            SuppressOriginalCoinDropsDuringGoldRush = suppressOriginalCoinDrops;

            DebugGoldRush = DebugGoldRush || debugGoldRush;
        }

        public static bool ShouldForceGoldRushCoinDrop(bool isEliteMonster)
        {
            if (MiniStageRuntimeState.IsInsideMiniStage)
            {
                return false;
            }

            if (!ForceGoldRushCoinDrop)
            {
                return false;
            }

            if (ForcedGoldRushCoinCount <= 0)
            {
                return false;
            }

            if (ForceGoldRushOnlyNormalMonsters && isEliteMonster)
            {
                return false;
            }

            return true;
        }

        public static int GetCoinDropAttemptCount()
        {
            if (MiniStageRuntimeState.IsInsideMiniStage)
            {
                return 1;
            }

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
            if (MiniStageRuntimeState.IsInsideMiniStage)
            {
                return Mathf.Max(1, baseValue);
            }

            float multiplier = Mathf.Max(1f, CoinValueMultiplier);

            return Mathf.Max(1, Mathf.RoundToInt(baseValue * multiplier));
        }
    }
}

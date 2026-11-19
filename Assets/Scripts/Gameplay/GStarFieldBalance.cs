using UnityEngine;

namespace Vampire
{
    public static class GStarFieldBalance
    {
        // Only the six food species in the mouth and the ordinary stomach roster.
        // Several specials share a melee blueprint but use a different actor.
        public static bool IsOrdinary(Monster monster) =>
            monster != null && !monster.IsMiniStageOwned &&
            monster.GetType() == typeof(MeleeMonster) &&
            monster.Blueprint != null && monster.Blueprint.GetType() == typeof(MeleeMonsterBlueprint);

        public static int BonusPerTen(FiniteStomachMap map) =>
            !GStarDemo.Enabled || map == null || map.IsColon ? 0 : map.IsOral ? 3 : 5;

        public static void TuneHealth(Monster monster, FiniteStomachMap map)
        {
            if (!GStarDemo.Enabled || map == null || map.IsColon || !IsOrdinary(monster)) return;
            if (map.IsOral && !OralMonsterRoster.IsBlueprint(monster.Blueprint)) return;
            monster.ScaleHealthCapacity(map.IsOral ? .70f : .65f);
        }

        // Promote a successful loot roll in place. Coin count and unique treasure bags are preserved.
        public static CoinType UpgradeCoin(Monster monster, CoinType original, float roll)
        {
            if ((int)original >= (int)CoinType.Pouch30) return original;
            if (monster is BossMonster || monster is MiniBossMonster)
                return roll < .70f ? CoinType.Bag50 : CoinType.Gold5;
            if (monster.Blueprint is EliteMonsterBlueprint)
                return roll < .40f ? CoinType.Pouch30 : CoinType.Gold5;

            bool special = RunScore.IsSpecial(monster);
            float pouchChance = special ? .14f : .02f;
            float goldChance = special ? .75f : .35f;
            if (roll < pouchChance) return CoinType.Pouch30;
            if (roll < goldChance) return CoinType.Gold5;
            if (roll < .70f && original == CoinType.Bronze1) return CoinType.Silver2;
            return original;
        }

        // Elapsed field time, excluding menus, loading, and mini-stage visits.
        public const int SecondsPerTier = 90, LastTier = 10;
        public static int SummonTier(float fieldSeconds) =>
            Mathf.Clamp(Mathf.FloorToInt(fieldSeconds / SecondsPerTier), 0, LastTier);

        public static float HabitatHealth(float speciesBudget, int tier) =>
            Mathf.Round(speciesBudget * (.30f + .045f * tier));

        public static float KingHealth(int tier) => 6000 + 1500 * tier;
    }
}

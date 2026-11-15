using System;

namespace Vampire
{
    // Currency is credited when earned, never when an inventory/display is refreshed.
    public static class GStarScoreRules
    {
        public const string Version = "gstar_2026_v1";
        public const int MaximumCount = 1000000;
        public const int Normal = 10, Special = 75, Elite = 250, MiniBoss = 1500,
            Boss = 5000, MiniClear = 1500, Overcharge = 1000, StageClear = 10000,
            Gold = 1, Silver = 2;

        public static int Count(int value) => Math.Max(0, Math.Min(MaximumCount, value));
        public static void Add(ref int value, int amount = 1)
        {
            if (amount <= 0) return;
            value = (int)Math.Min(MaximumCount, (long)Count(value) + amount);
        }
        public static int Total(RunScoreData value)
        {
            if (value == null) return 0;
            long total = (long)Count(value.normal) * Normal + (long)Count(value.special) * Special +
                (long)Count(value.elite) * Elite + (long)Count(value.miniBoss) * MiniBoss +
                (long)Count(value.boss) * Boss + (long)Count(value.cleared) * MiniClear +
                (long)Count(value.overcharge) * Overcharge + (long)Count(value.gold) * Gold +
                (long)Count(value.silver) * Silver + (value.success ? StageClear : 0);
            return (int)Math.Min(int.MaxValue, total);
        }
        public static RunScoreData Snapshot(RunScoreData value)
        {
            if (value == null) return null;
            return new RunScoreData
            {
                normal = Count(value.normal), special = Count(value.special), elite = Count(value.elite),
                miniBoss = Count(value.miniBoss), boss = Count(value.boss), entered = Count(value.entered),
                cleared = Count(value.cleared), overcharge = Count(value.overcharge), gold = Count(value.gold),
                silver = Count(value.silver), success = value.success, rules = Version,
                seconds = float.IsNaN(value.seconds) || float.IsInfinity(value.seconds) ? 0 : Math.Max(0, Math.Min(86400, value.seconds))
            };
        }
    }
}

using UnityEngine;

namespace Vampire
{
    public static class RelicSaveData
    {
        private const string EquippedRelicKey = "EquippedRelicId";
        private static string demoEquippedRelic;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetDemoSelection() => demoEquippedRelic = null;

        private static string GetUnlockedKey(string relicId)
        {
            return $"RelicUnlocked_{relicId}";
        }

        public static bool IsUnlocked(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId))
            {
                return false;
            }

            if (GStarDemo.Enabled) return relicId != "lotus_chips";
            return ProgressPrefs.GetInt(GetUnlockedKey(relicId), 0) == 1;
        }

        public static void Unlock(string relicId)
        {
            if (GStarDemo.Enabled) return;
            if (string.IsNullOrWhiteSpace(relicId))
            {
                return;
            }

            ProgressPrefs.SetInt(GetUnlockedKey(relicId), 1);
            ProgressPrefs.Save();
        }

        public static string GetEquippedRelicId()
        {
            if (GStarDemo.Enabled) return demoEquippedRelic ?? string.Empty;
            return ProgressPrefs.GetString(EquippedRelicKey, string.Empty);
        }

        public static bool IsEquipped(string relicId)
        {
            return GetEquippedRelicId() == relicId;
        }

        public static void Equip(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId))
            {
                return;
            }

            if (!IsUnlocked(relicId))
            {
                return;
            }

            if (GStarDemo.Enabled) { demoEquippedRelic = relicId; return; }
            ProgressPrefs.SetString(EquippedRelicKey, relicId);
            ProgressPrefs.Save();
        }

        public static void Unequip()
        {
            if (GStarDemo.Enabled) { demoEquippedRelic = string.Empty; return; }
            ProgressPrefs.DeleteKey(EquippedRelicKey);
            ProgressPrefs.Save();
        }
    }
}

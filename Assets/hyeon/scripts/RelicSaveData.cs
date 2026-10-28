using UnityEngine;

namespace Vampire
{
    public static class RelicSaveData
    {
        private const string EquippedRelicKey = "EquippedRelicId";

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

            return ProgressPrefs.GetInt(GetUnlockedKey(relicId), 0) == 1;
        }

        public static void Unlock(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId))
            {
                return;
            }

            ProgressPrefs.SetInt(GetUnlockedKey(relicId), 1);
            ProgressPrefs.Save();
        }

        public static string GetEquippedRelicId()
        {
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

            ProgressPrefs.SetString(EquippedRelicKey, relicId);
            ProgressPrefs.Save();
        }

        public static void Unequip()
        {
            ProgressPrefs.DeleteKey(EquippedRelicKey);
            ProgressPrefs.Save();
        }
    }
}
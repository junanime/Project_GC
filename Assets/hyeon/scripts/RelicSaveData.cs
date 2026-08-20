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

            return PlayerPrefs.GetInt(GetUnlockedKey(relicId), 0) == 1;
        }

        public static void Unlock(string relicId)
        {
            if (string.IsNullOrWhiteSpace(relicId))
            {
                return;
            }

            PlayerPrefs.SetInt(GetUnlockedKey(relicId), 1);
            PlayerPrefs.Save();
        }

        public static string GetEquippedRelicId()
        {
            return PlayerPrefs.GetString(EquippedRelicKey, string.Empty);
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

            PlayerPrefs.SetString(EquippedRelicKey, relicId);
            PlayerPrefs.Save();
        }

        public static void Unequip()
        {
            PlayerPrefs.DeleteKey(EquippedRelicKey);
            PlayerPrefs.Save();
        }
    }
}
using UnityEngine;

namespace Vampire
{
    public static class LobbyUnlockSave
    {
        private const string UnlockPrefix = "LobbyUnlock.";

        public static bool IsUnlocked(string category, string id, bool defaultUnlocked = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                return defaultUnlocked;
            }

            string key = GetKey(category, id);
            return ProgressPrefs.GetInt(key, defaultUnlocked ? 1 : 0) == 1;
        }

        public static void Unlock(string category, string id)
        {
            if (category == "Needle" && System.Enum.TryParse(id, out SyringeSpecialAugmentAbility.SpecialAugmentType type)
                && !StartingNeedleSelection.Available(type)) return;
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            string key = GetKey(category, id);
            ProgressPrefs.SetInt(key, 1);
            ProgressPrefs.Save();
        }

        public static void Lock(string category, string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            string key = GetKey(category, id);
            ProgressPrefs.DeleteKey(key);
            ProgressPrefs.Save();
        }

        private static string GetKey(string category, string id)
        {
            return UnlockPrefix + category + "." + id;
        }
    }
}
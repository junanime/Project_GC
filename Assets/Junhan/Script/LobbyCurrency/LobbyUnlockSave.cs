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

            if (GStarDemo.Enabled)
            {
                if (category == "Needle")
                    return System.Enum.TryParse(id, out SyringeSpecialAugmentAbility.SpecialAugmentType type)
                        && System.Enum.IsDefined(typeof(SyringeSpecialAugmentAbility.SpecialAugmentType), type)
                        && StartingNeedleSelection.Available(type);
                if (category == "Character") return true;
            }

            string key = GetKey(category, id);
            return ProgressPrefs.GetInt(key, defaultUnlocked ? 1 : 0) == 1;
        }

        public static void Unlock(string category, string id)
        {
            if (GStarDemo.Enabled) return;
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
            if (GStarDemo.Enabled) return;
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

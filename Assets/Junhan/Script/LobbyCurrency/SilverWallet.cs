using System;
using UnityEngine;

namespace Vampire
{
    public static class SilverWallet
    {
        private const string SilverKey = "LobbySilverCoins";

        public static event Action<int> OnChanged;

        public static int Silver
        {
            get
            {
                if (GStarDemo.Enabled) return 0;
                return ProgressPrefs.GetInt(SilverKey, 0);
            }
        }

        public static void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (GStarDemo.Enabled)
            {
                RunScore.Current?.Silver(amount);
                return;
            }
            Set(Silver + amount);
        }

        public static bool CanSpend(int amount)
        {
            return amount >= 0 && (GStarDemo.Enabled || Silver >= amount);
        }

        public static bool TrySpend(int amount)
        {
            if (amount < 0)
            {
                return false;
            }

            if (GStarDemo.Enabled) return true;

            if (Silver < amount)
            {
                return false;
            }

            Set(Silver - amount);
            return true;
        }

        public static void Set(int amount)
        {
            // Booth rewards and purchases never touch the normal player's wallet.
            if (GStarDemo.Enabled) return;
            int safeAmount = Mathf.Max(0, amount);

            ProgressPrefs.SetInt(SilverKey, safeAmount);
            ProgressPrefs.Save();

            OnChanged?.Invoke(safeAmount);
        }

        public static void ResetSilver()
        {
            Set(0);
        }
    }
}

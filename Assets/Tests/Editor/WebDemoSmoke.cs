using System;
using UnityEditor;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public static class WebDemoSmoke
    {
        public static void Run()
        {
            const string key = "LobbySilverCoins";
            bool existed = PlayerPrefs.HasKey(key);
            int previous = PlayerPrefs.GetInt(key);
            int exit = 1;
            try
            {
                WebDemoStartup.ResetSessionForTests();
                SilverWallet.Set(0);
                WebDemoStartup.GrantSessionAllowance();
                Check(SilverWallet.Silver == 10000, "fresh browser gets 10,000 silver");
                SilverWallet.TrySpend(1200);
                WebDemoStartup.GrantSessionAllowance();
                Check(SilverWallet.Silver == 8800, "no repeated grant when returning to lobby");
                WebDemoStartup.ResetSessionForTests();
                WebDemoStartup.GrantSessionAllowance();
                Check(SilverWallet.Silver == 10000, "new browser session tops up a low balance");
                SilverWallet.Set(14500);
                WebDemoStartup.ResetSessionForTests();
                WebDemoStartup.GrantSessionAllowance();
                Check(SilverWallet.Silver == 14500, "earned silver is never reduced");
                Debug.Log("[WebDemoSmoke] PASS 4/4");
                exit = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
                WebDemoStartup.ResetSessionForTests();
            }
            EditorApplication.Exit(exit);
        }

        static void Check(bool valid, string message)
        {
            if (!valid) throw new Exception("Web demo check failed: " + message);
            Debug.Log("[WebDemoSmoke] " + message);
        }
    }
}

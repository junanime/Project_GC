using UnityEngine;

namespace Vampire
{
    // Only the explicitly built Web demo grants this allowance. Desktop saves are untouched.
    public static class WebDemoStartup
    {
        public const int StartingSilver = 10000;

#if (UNITY_WEBGL && PROJECT_GC_WEB_DEMO) || UNITY_EDITOR
        static bool grantedThisSession;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetSession() => grantedThisSession = false;

#if UNITY_WEBGL && PROJECT_GC_WEB_DEMO && !UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot() => GrantSessionAllowance();
#endif

        public static void GrantSessionAllowance()
        {
            if (grantedThisSession) return;
            grantedThisSession = true;
            if (SilverWallet.Silver < StartingSilver) SilverWallet.Set(StartingSilver);
            Debug.Log("[WebDemo] Session starting silver=" + SilverWallet.Silver);
        }

#if UNITY_EDITOR
        public static void ResetSessionForTests() => ResetSession();
#endif
#endif
    }
}

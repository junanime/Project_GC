using System;
using System.IO;
using UnityEngine;

namespace Vampire
{
    // Only the dedicated booth branch ships this resource marker. Normal builds keep their flow.
    public static class GStarDemo
    {
        public const int AttemptLimit = 2;
        public const string EventId = "gstar2026";
        static bool? enabled;
#if UNITY_EDITOR
        public static bool? TestOverride;
        public static string TestDataDirectory;
#endif
        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                if (TestOverride.HasValue) return TestOverride.Value;
#endif
                if (!enabled.HasValue) enabled = Resources.Load<TextAsset>("GStarDemo/enabled") != null;
                return enabled.Value;
            }
        }
        public static string DataDirectory
        {
            get
            {
#if UNITY_EDITOR
                if (!string.IsNullOrEmpty(TestDataDirectory)) return TestDataDirectory;
#endif
#if DEVELOPMENT_BUILD && !UNITY_EDITOR
                // Natural exhibition QA uses a fresh booth ledger beside each run's recordings.
                if (Tests.GStarNaturalBoothPatrol.Enabled) return Tests.GStarNaturalBoothPatrol.VisitorDirectory;
#endif
                return Path.Combine(Application.persistentDataPath, "GStar2026");
            }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            enabled = null;
#if UNITY_EDITOR
            TestOverride = null;
            TestDataDirectory = null;
#endif
        }
    }
}

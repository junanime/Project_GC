using UnityEngine;

namespace Vampire
{
    public static class OralSpicyHazard
    {
        public const float WarningSeconds = 7f, HealthFraction = .10f;
        public const int HotSeconds = 10;
        public static bool IsHot(float age) => age >= WarningSeconds && age < WarningSeconds + HotSeconds;
        public static int PulsesThrough(float age) =>
            Mathf.Clamp(Mathf.FloorToInt(age - WarningSeconds), 0, HotSeconds);
    }
}

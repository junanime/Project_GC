using System;

namespace Vampire
{
    [Serializable]
    public struct SyringeSpecialRuntime
    {
        // Poison
        public bool poisonEnabled;
        public float poisonDuration;
        public float poisonTickInterval;
        public float poisonTickDamage;

        // Explosion
        public bool explosionEnabled;
        public float explosionRadius;
        public float explosionDamage;

        // Homing
        public bool homingEnabled;
        public float homingRange;
        public float homingLerpSpeed;

        // Pierce
        public bool pierceEnabled;
        public int pierceCount;
    }
}
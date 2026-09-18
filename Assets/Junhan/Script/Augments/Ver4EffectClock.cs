using UnityEngine;
namespace Vampire
{
    public sealed class Ver4EffectClock : MonoBehaviour
    {
        private float nextShock, nextHealingGround;
        public bool TryShock() { if(Time.time<nextShock) return false; nextShock=Time.time+.5f; return true; }
        public bool TryHealingGround() { if(Time.time<nextHealingGround) return false; nextHealingGround=Time.time+3f; return true; }
    }
}

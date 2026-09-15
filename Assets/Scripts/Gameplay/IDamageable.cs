using UnityEngine;

namespace Vampire
{
    public abstract class IDamageable : MonoBehaviour
    {
        public abstract void TakeDamage(
            float damage,
            Vector2 knockback = default(Vector2),
            bool isCritical = false
        );

        protected bool SuppressHitFlash { get; private set; }
        public void TakePeriodicDamage(float damage, Vector2 knockback = default(Vector2), bool isCritical = false)
        {
            bool previous = SuppressHitFlash;
            SuppressHitFlash = true;
            try { TakeDamage(damage, knockback, isCritical); }
            finally { SuppressHitFlash = previous; }
        }

        public abstract void Knockback(Vector2 knockback);
    }
}
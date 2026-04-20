using UnityEngine;

namespace Vampire
{
    public class SyringePoisonProjectile : Projectile
    {
        [Header("Poison Settings")]
        [SerializeField] private bool poisonEnabled = true;
        [SerializeField] private float poisonDuration = 3f;
        [SerializeField] private float poisonTickInterval = 0.5f;
        [SerializeField] private float poisonTickDamage = 5f;

        protected override void HitDamageable(IDamageable damageable)
        {
            ApplyPoison(damageable);
            base.HitDamageable(damageable);
        }

        private void ApplyPoison(IDamageable damageable)
        {
            if (!poisonEnabled)
            {
                return;
            }

            Component damageableComponent = damageable as Component;
            if (damageableComponent == null)
            {
                return;
            }

            Monster monster =
                damageableComponent.GetComponent<Monster>() ??
                damageableComponent.GetComponentInParent<Monster>();

            if (monster == null)
            {
                return;
            }

            PoisonStatus poisonStatus = monster.GetComponent<PoisonStatus>();
            if (poisonStatus == null)
            {
                poisonStatus = monster.gameObject.AddComponent<PoisonStatus>();
            }

            poisonStatus.Apply(poisonDuration, poisonTickInterval, poisonTickDamage);
        }
    }
}
using UnityEngine;

namespace Vampire
{
    public static class NeedleTargeting
    {
        public static bool IsValidBossTarget(Component target,LayerMask targets)
        {
            if(target==null||!target.gameObject.activeInHierarchy)return false;
            var damageable=target.GetComponentInParent<IDamageable>();
            if(!(damageable is FoodBossRuntime)&&!(damageable is FoodBossWeakpoint)&&!(damageable is SnailBossRuntime))return false;
            if(Ver4HitEffects.Health(damageable)<=0)return false;
            var c=damageable.GetComponent<Collider2D>();return c!=null&&c.enabled&&(targets.value&(1<<c.gameObject.layer))!=0;
        }
        public static bool IsValid(Monster monster, LayerMask targets)
        {
            if (monster == null || !monster.gameObject.activeInHierarchy || monster.HP <= 0f ||
                monster.IsFieldRuntimeSuspended || (monster is TrapMonster trap && !trap.IsActive)) return false;
            var body = monster.BodyHitbox;
            return body != null && body.enabled && body.gameObject.activeInHierarchy &&
                   (targets.value & (1 << body.gameObject.layer)) != 0;
        }

        public static Vector2 Position(Monster monster) => monster.CenterTransform != null
            ? (Vector2)monster.CenterTransform.position : (Vector2)monster.transform.position;

        // Angular interpolation cannot stall when target and flight point in opposite directions.
        public static Vector2 Curve(Vector2 current, Vector2 desired, float response, float deltaTime)
        {
            if (desired.sqrMagnitude < .0001f || deltaTime <= 0f) return current;
            float angle = Mathf.Atan2(current.y, current.x) * Mathf.Rad2Deg;
            float target = Mathf.Atan2(desired.y, desired.x) * Mathf.Rad2Deg;
            angle += Mathf.DeltaAngle(angle, target) * (1f - Mathf.Exp(-Mathf.Max(0f, response) * deltaTime));
            return new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        }
    }
}

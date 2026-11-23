using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Vampire
{
    public static class NeedleTargeting
    {
        // List overload grows to fit the entire physics result instead of silently
        // dropping later hits in crowded packs. Match the legacy layer/trigger filter.
        public static void FillCircle(Vector2 origin, float range, int layerMask, List<Collider2D> hits)
        {
            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(layerMask);
            Physics2D.OverlapCircle(origin, range, filter, hits);
        }

        internal static PooledObject<List<Collider2D>> QueryCircle(Vector2 origin, float range, int layerMask, out List<Collider2D> hits)
        {
            var lease = ListPool<Collider2D>.Get(out hits);
            FillCircle(origin, range, layerMask, hits);
            return lease;
        }
        // A hitbox can sit under more than one damageable type. The existing attack
        // prioritizes the nearest king, then the nearest colon boss, then a regular monster.
        internal static IDamageable FindResidueTarget(Collider2D hit, List<IDamageable> parents)
        {
            var nearest = hit.GetComponentInParent<IDamageable>();
            if (nearest == null || nearest is ConstipationKingRuntime) return nearest;
            hit.GetComponentsInParent(false, parents);
            ColonBossRuntime colon = null;
            Monster monster = null;
            foreach (var actor in parents)
            {
                if (actor is ConstipationKingRuntime king) return king;
                if (actor is ColonBossRuntime boss && colon == null) colon = boss;
                else if (actor is Monster mob && monster == null) monster = mob;
            }
            return colon != null ? (IDamageable)colon : monster;
        }

        public static bool IsValidBossTarget(Component target,LayerMask targets)
        {
            if(target==null||!target.gameObject.activeInHierarchy)return false;
            var damageable=target.GetComponentInParent<IDamageable>();
            if(!(damageable is FoodBossRuntime)&&!(damageable is FoodBossWeakpoint)&&!(damageable is SnailBossRuntime)&&!(damageable is ColonBossRuntime)&&!(damageable is ConstipationKingRuntime)&&!(damageable is ConstipationKingMinion))return false;
            if(damageable is ConstipationKingRuntime king&&!king.Targetable)return false;
            if(damageable is ConstipationKingMinion minion&&!minion.Targetable)return false;
            if(damageable is ColonBossRuntime colon&&(colon.Dead||colon.Arriving||colon.Hidden))return false;
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

        // Pipe encounters can move the exposed head away from the hidden body's root.
        public static Vector2 Position(Component target)
        {
            if (target == null) return Vector2.zero;
            var king = target.GetComponentInParent<ConstipationKingRuntime>();
            if (king != null) return king.HitCenter;
            var colon = target.GetComponentInParent<ColonBossRuntime>();
            return colon != null ? colon.HitCenter : (Vector2)target.transform.position;
        }

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

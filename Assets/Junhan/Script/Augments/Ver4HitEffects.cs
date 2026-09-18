using System.Collections.Generic;
using UnityEngine;
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire
{
    public static class Ver4HitEffects
    {
        public static float BeforeHit(Component target, SyringeSpecialRuntime runtime)
        {
            if (runtime.ver4 == null || target == null) return 1f;
            float multiplier=1;
            var honey=target.GetComponent<HoneySlowStatus>();
            if (honey!=null && honey.IsActive) multiplier*=runtime.ver4.Factor(P.Honey,2,.08f);
            var status=target.GetComponent<Ver4NeedleStatus>();
            if (runtime.ver4.Has(P.IceNeedle) && status!=null && status.ConsumeFrozen()) multiplier*=1.2f;
            return multiplier;
        }
        public static float ExplosionCenterMultiplier(Component target,Vector2 center,SyringeSpecialRuntime runtime)
        {
            if(runtime.ver4==null || target==null || Vector2.Distance(target.transform.position,center)>runtime.explosionRadius*.5f) return 1;
            return runtime.ver4.Factor(P.Explosion,2,.15f);
        }
        public static void AfterHit(Component target,SyringeSpecialRuntime runtime,Character source,LayerMask layer,bool markConsumed)
        {
            var snapshot=runtime.ver4;
            if(snapshot==null || target==null || source==null) return;
            var bacteria=target.GetComponent<GutBacteriaStatus>();
            if(runtime.gutBacteriaEnabled && bacteria!=null)
            {
                bacteria.ExtraGemChance=.10f*snapshot.Count(P.GutBacteriaNeedle,2);
                if(Random.value<.13f*snapshot.Count(P.GutBacteriaNeedle,1))
                    bacteria.Apply(runtime.gutBacteriaStackDuration,runtime.gutBacteriaRequiredStacks,runtime.gutBacteriaMaxStacks,
                        runtime.gutBacteriaBonusGemCount,runtime.gutBacteriaBonusGemType,runtime.gutBacteriaBonusGemSpawnRadius,false);
            }
            if(markConsumed && Random.value<.10f*snapshot.Count(P.MarkNeedle,2))
                SyringeSpecialHitEffectUtility.ReapplyVer4Mark(target,runtime);
            var status=target.GetComponent<Ver4NeedleStatus>();
            if((snapshot.Has(P.FireNeedle)||snapshot.Has(P.IceNeedle)||snapshot.Has(P.WoodNeedle)) && (status!=null || Health(target)>0))
            {
                if(status==null) status=target.gameObject.AddComponent<Ver4NeedleStatus>();
                status.Hit(runtime,source,layer);
            }
            if(snapshot.Has(P.VibrationNeedle))
            {
                var clock=source.GetComponent<Ver4EffectClock>()??source.gameObject.AddComponent<Ver4EffectClock>();
                if(clock.TryShock()) AreaDamage(target.transform.position,snapshot.Factor(P.VibrationNeedle,0,.12f),
                    runtime.ver4HitDamage*.35f*snapshot.Factor(P.VibrationNeedle,1,.12f),
                    runtime.ver4Knockback*snapshot.Factor(P.VibrationNeedle,2,.15f),source,layer,"진동침");
            }
        }
        public static float Health(Component target)
        {
            if(target is Monster monster) return monster.HP;
            if(target is BossPartDamageTestPart part) return part.CurrentHealth;
            return 0;
        }
        public static float MaxHealth(Component target)
        {
            if(target is Monster monster) return monster.SpawnMaxHealth;
            if(target is BossPartDamageTestPart part) return part.MaxHealth;
            return 0;
        }
        public static bool IsBoss(Component target) => target is BossMonster || target is MiniBossMonster || target is BossPartDamageTestPart;
        public static List<Component> Nearby(Vector2 center,float radius,LayerMask layer,Character source)
        {
            var result=new List<Component>(); var ids=new HashSet<int>();
            foreach(var collider in Physics2D.OverlapCircleAll(center,radius,layer))
                if(SyringeSpecialHitEffectUtility.TryGetValidDamageableTarget(collider,source,out _,out var target,out int id)
                    && ids.Add(id) && Health(target)>0) result.Add(target);
            result.Sort((a,b)=>((Vector2)a.transform.position-center).sqrMagnitude.CompareTo(((Vector2)b.transform.position-center).sqrMagnitude));
            return result;
        }
        public static bool Damage(Component target,float amount,Vector2 knockback,Character source,string label,bool periodic=false)
        {
            if(target==null || !(target is IDamageable damageable) || Health(target)<=0) return false;
            float before=Health(target);
            if(periodic) damageable.TakePeriodicDamage(amount,knockback);
            else damageable.TakeDamage(amount,knockback);
            float dealt=Mathf.Clamp(before-Health(target),0,before);
            source?.OnDealDamage.Invoke(dealt);
            AugmentDamageTracker.Instance?.RecordDamage(label,dealt);
            return Health(target)<=0;
        }
        public static void AreaDamage(Vector2 center,float radius,float amount,float knockback,Character source,LayerMask layer,string label)
        {
            foreach(var target in Nearby(center,radius,layer,source))
                Damage(target,amount,((Vector2)target.transform.position-center).normalized*knockback,source,label);
        }
    }
}

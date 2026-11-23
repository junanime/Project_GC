using System.Collections.Generic;
using UnityEngine;
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire
{
    public static class Ver4HitEffects
    {
        public static float BeforeHit(Component target, SyringeSpecialRuntime runtime)
        {
            if (MonsterNeedleBarrier.Blocks(target)) return 1f;
            if (runtime.ver4 == null || target == null) return 1f;
            float multiplier=1;
            var bacteria=target.GetComponent<GutBacteriaStatus>() ?? target.GetComponentInParent<GutBacteriaStatus>();
            if (runtime.gutBacteriaEnabled && bacteria!=null) bacteria.HarvestRank=runtime.ver4.Count(P.GutBacteriaNeedle,2);
            var honey=target.GetComponent<HoneySlowStatus>();
            if (honey!=null && honey.IsActive)
            {
                multiplier*=runtime.ver4.Factor(P.Honey,2,.08f);
                runtime.ver4.Impact(target.transform.position,target,P.Honey,2);
            }
            var status=target.GetComponent<Ver4NeedleStatus>();
            if (runtime.ver4.Has(P.IceNeedle) && status!=null && status.ConsumeFrozen()) multiplier*=1.2f;
            return multiplier;
        }
        public static float ExplosionCenterMultiplier(Component target,Vector2 center,SyringeSpecialRuntime runtime)
        {
            if(runtime.ver4==null || target==null || Vector2.Distance(target.transform.position,center)>runtime.explosionRadius*.5f) return 1;
            return runtime.ver4.Factor(P.Explosion,2,.15f);
        }
        internal static void ReportExplosionImpact(SyringeSpecialRuntime runtime, Vector2 center, Component victim, float before)
        {
            if (runtime.ver4?.PendingFeedback != true || Health(victim) >= before) return;
            runtime.ver4.Impact(victim.transform.position, victim, P.Explosion, 1);
            if (Vector2.Distance(center, victim.transform.position) <= runtime.explosionRadius * .5f)
                runtime.ver4.Impact(victim.transform.position, victim, P.Explosion, 2);
        }

        public static void AfterHit(Component target,SyringeSpecialRuntime runtime,Character source,LayerMask layer,bool markConsumed)
        {
            if (MonsterNeedleBarrier.Blocks(target)) return;
            FoodBossCombatStatus.Apply(target,runtime,source,markConsumed);
            var snapshot=runtime.ver4;
            if(snapshot==null || target==null || source==null) return;
            int polarityStage = snapshot.Count(P.BipolarNeedle, 2);
            if (polarityStage > 0 && runtime.bipolarPolarity != 0 && Health(target) > 0)
            {
                var polarity = target.GetComponent<BipolarMarkStatus>() ?? target.gameObject.AddComponent<BipolarMarkStatus>();
                polarity.Hit(target, runtime.bipolarPolarity, polarityStage, runtime.ver4HitDamage, source, snapshot);
            }
            var bacteria=target.GetComponent<GutBacteriaStatus>();
            if(runtime.gutBacteriaEnabled && bacteria!=null && Health(target)>0)
            {
                bacteria.HarvestRank=snapshot.Count(P.GutBacteriaNeedle,2);
                if(Random.value<.13f*snapshot.Count(P.GutBacteriaNeedle,1))
                {
                    int beforeBonus = bacteria.CurrentStacks;
                    bacteria.Apply(runtime.gutBacteriaStackDuration,runtime.gutBacteriaRequiredStacks,runtime.gutBacteriaMaxStacks,
                        runtime.gutBacteriaBonusGemCount,runtime.gutBacteriaBonusGemType,runtime.gutBacteriaBonusGemSpawnRadius,false,snapshot);
                    if (bacteria.CurrentStacks > beforeBonus) snapshot.Impact(target.transform.position,target,P.GutBacteriaNeedle,1);
                }
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
                if(clock.TryShock())
                {
                    float radius = snapshot.Factor(P.VibrationNeedle,0,.12f);
                    SyringeAugmentVfx.PlayRadius("VibrationShockwave", target.transform.position, radius, SyringeAugmentVfx.FindTarget(target));
                    snapshot.Impact(target.transform.position, target, P.VibrationNeedle, 0);
                    AreaDamage(target.transform.position, radius,
                        runtime.ver4HitDamage*.35f*snapshot.Factor(P.VibrationNeedle,1,.12f),
                        runtime.ver4Knockback*snapshot.Factor(P.VibrationNeedle,2,.15f),source,layer,"진동침",snapshot);
                }
            }
        }
        public static float Health(Component target)
        {
            if(target is ConstipationKingRuntime king)return king.Health;
            if(target is ConstipationKingMinion tribute)return tribute.Health;
            if(target is ColonBossRuntime colon)return colon.Health;
            if(target is FoodBossRuntime food)return food.Health;
            if(target is FoodBossWeakpoint weak)return weak.Health;
            if(target is CurrencyJar jar)return jar.CanBreak ? 1 : 0;
            if(target is SnailBossRuntime snail)return snail.Health;
            if(target is Monster monster) return monster.HP;
            if(target is BossPartDamageTestPart part) return part.CurrentHealth;
            return 0;
        }
        public static float MaxHealth(Component target)
        {
            if(target is ConstipationKingRuntime king)return king.MaxHealth;
            if(target is ConstipationKingMinion tribute)return tribute.MaxHealth;
            if(target is ColonBossRuntime colon)return colon.MaxHealth;
            if(target is FoodBossRuntime food)return food.MaxHealth;
            if(target is FoodBossWeakpoint weak)return weak.MaxHealth;
            if(target is SnailBossRuntime snail)return snail.settings!=null?snail.settings.maxHealth:1;
            if(target is Monster monster) return monster.SpawnMaxHealth;
            if(target is BossPartDamageTestPart part) return part.MaxHealth;
            return 0;
        }
        public static bool IsBoss(Component target) => target is ConstipationKingRuntime || target is ColonBossRuntime || target is FoodBossRuntime || target is FoodBossWeakpoint || target is SnailBossRuntime || target is BossMonster || target is MiniBossMonster || target is BossPartDamageTestPart;
        public static List<Component> Nearby(Vector2 center,float radius,LayerMask layer,Character source)
        {
            var result=new List<Component>(); var ids=new HashSet<int>();
            foreach(var collider in Physics2D.OverlapCircleAll(center,radius,layer))
                if(SyringeSpecialHitEffectUtility.TryGetValidDamageableTarget(collider,source,out _,out var target,out int id)
                    && ids.Add(id) && Health(target)>0) result.Add(target);
            result.Sort((a,b)=>((Vector2)a.transform.position-center).sqrMagnitude.CompareTo(((Vector2)b.transform.position-center).sqrMagnitude));
            return result;
        }
        public static bool Damage(Component target,float amount,Vector2 knockback,Character source,string label,bool periodic=false,CombatStatusTag? statusBeforeHit=null)
        {
            if(MonsterNeedleBarrier.Blocks(target)) return false;
            if(target is CurrencyJar jar){if(source==null||amount<=0||!jar.CanBreak)return false;jar.TakeDamage(amount);return jar.Broken;}
            if(target==null || !(target is IDamageable damageable) || Health(target)<=0) return false;
            amount*=statusBeforeHit.HasValue ? CombatStatusRules.DamageMultiplier(source,statusBeforeHit.Value) : CombatStatusRules.DamageMultiplier(source,target);
            float before=Health(target);
            if(periodic) damageable.TakePeriodicDamage(amount,knockback);
            else damageable.TakeDamage(amount,knockback);
            float dealt=Mathf.Clamp(before-Health(target),0,before);
            source?.OnDealDamage.Invoke(dealt);
            AugmentDamageTracker.Instance?.RecordDamage(label,dealt);
            return Health(target)<=0;
        }
        public static void AreaDamage(Vector2 center,float radius,float amount,float knockback,Character source,LayerMask layer,string label,Ver4CombatSnapshot snapshot = null,P parent = P.VibrationNeedle)
        {
            foreach(var target in Nearby(center,radius,layer,source))
            {
                float before = snapshot?.PendingFeedback == true ? Health(target) : 0f;
                var impulse = ((Vector2)target.transform.position-center).normalized*knockback;
                var body = before > 0 && parent == P.VibrationNeedle && impulse.sqrMagnitude > 0
                    ? target.GetComponent<Rigidbody2D>() : null;
                var velocity = body != null ? body.velocity : Vector2.zero;
                Damage(target,amount,impulse,source,label);
                if (before > 0 && before > Health(target))
                {
                    snapshot.Impact(target.transform.position, target, parent, 1);
                    if (body != null && body.bodyType == RigidbodyType2D.Dynamic && body.velocity != velocity)
                        snapshot.Impact(target.transform.position, target, parent, 2);
                }
            }
        }
    }
}

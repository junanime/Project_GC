using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Legacy needle effects normally require Monster. Keep the same build rules for the new IDamageable bosses.
    public sealed class FoodBossCombatStatus:MonoBehaviour,ICombatStatus
    {
        Component target;Character source,acidSource;SyringeSpecialRuntime acidSettings,bacteriaSettings;
        float poisonLeft,poisonTick,poisonInterval,poisonDamage,slowLeft,slowFactor=1;
        bool acid,defeated;readonly List<float> bacteria=new List<float>();
        SyringeAugmentVfx poisonVisual,bacteriaVisual;
        public float SpeedMultiplier=>slowLeft>0?slowFactor:1;
        public CombatStatusTag ActiveStatusTags => (poisonLeft>0?CombatStatusTag.Poison:CombatStatusTag.None)|(slowLeft>0?CombatStatusTag.Slow:CombatStatusTag.None)|(acid?CombatStatusTag.DeathMark:CombatStatusTag.None)|(bacteria.Count>0?CombatStatusTag.Infection:CombatStatusTag.None);
        void Awake(){target=GetComponent<IDamageable>();}
        public static bool Supports(Component target)=>target is FoodBossRuntime||target is FoodBossWeakpoint;
        public static void Apply(Component target,SyringeSpecialRuntime runtime,Character source,bool markConsumed)
        {
            if(!Supports(target)||Ver4HitEffects.Health(target)<=0)return;
            var s=target.GetComponent<FoodBossCombatStatus>()??target.gameObject.AddComponent<FoodBossCombatStatus>();s.source=source;
            if(runtime.poisonEnabled)
            {
                bool fresh=s.poisonLeft<=0;s.poisonLeft=Mathf.Max(.05f,runtime.poisonDuration);s.poisonInterval=Mathf.Max(.05f,runtime.poisonTickInterval);s.poisonDamage=runtime.poisonTickDamage;
                if(fresh)s.poisonTick=0;
                if(s.poisonVisual==null)s.poisonVisual=SyringeAugmentVfx.Play("Poison",target.transform.position,SyringeAugmentVfx.FindTarget(target));
            }
            if(runtime.honeyEnabled){s.Slow(runtime.honeyDuration,runtime.honeySlowMultiplier);HoneyTrailEmitter.Touch(SyringeAugmentVfx.FindTarget(target));}
            if(runtime.corrosionEnabled)(target.GetComponent<CorrosionStatus>()??target.gameObject.AddComponent<CorrosionStatus>()).Apply(runtime.corrosionDuration,runtime.corrosionDamageTakenBonusPerStack,runtime.corrosionBossDamageTakenBonusPerStack,runtime.corrosionMaxStacks,true);
            if(runtime.markEnabled&&!markConsumed)(target.GetComponent<NeedleMarkStatus>()??target.gameObject.AddComponent<NeedleMarkStatus>()).Apply(runtime.markDuration);
            if(runtime.digestiveAcidSacEnabled){s.acid=true;s.acidSettings=runtime;s.acidSource=source;}
            if(runtime.gutBacteriaEnabled)
            {
                s.bacteriaSettings=runtime;
                s.bacteria.Add(Mathf.Max(.1f,runtime.gutBacteriaStackDuration));
                int rank=runtime.ver4!=null?runtime.ver4.Count(SyringeSpecialAugmentAbility.SpecialAugmentType.GutBacteriaNeedle,1):0;
                if(Random.value<.13f*rank)s.bacteria.Add(Mathf.Max(.1f,runtime.gutBacteriaStackDuration));
                while(s.bacteria.Count>Mathf.Max(runtime.gutBacteriaRequiredStacks,runtime.gutBacteriaMaxStacks))s.bacteria.RemoveAt(0);
                if(s.bacteriaVisual==null)s.bacteriaVisual=SyringeAugmentVfx.Play("GutBacteriaNeedle",target.transform.position,SyringeAugmentVfx.FindTarget(target));
                if(s.bacteriaVisual!=null)s.bacteriaVisual.SetStrength(Mathf.Lerp(.4f,1,s.bacteria.Count/(float)Mathf.Max(1,runtime.gutBacteriaMaxStacks)));
            }
        }
        public void Slow(float seconds,float multiplier){slowLeft=Mathf.Max(slowLeft,seconds);slowFactor=Mathf.Min(slowFactor,Mathf.Clamp(multiplier,.2f,1));}
        void Update()
        {
            if(FoodBossRuntime.Paused)return;
            if(Ver4HitEffects.Health(target)<=0){SyringeAugmentVfx.ReleaseOwned(ref poisonVisual);SyringeAugmentVfx.ReleaseOwned(ref bacteriaVisual);return;}
            slowLeft-=Time.deltaTime;if(slowLeft<=0)slowFactor=1;
            for(int i=bacteria.Count-1;i>=0;i--){bacteria[i]-=Time.deltaTime;if(bacteria[i]<=0)bacteria.RemoveAt(i);}
            if(bacteria.Count==0)SyringeAugmentVfx.ReleaseOwned(ref bacteriaVisual);
            if(poisonLeft>0)
            {
                poisonLeft-=Time.deltaTime;poisonTick-=Time.deltaTime;
                if(poisonTick<=0){poisonTick+=poisonInterval;Ver4HitEffects.Damage(target,poisonDamage,Vector2.zero,source,"독침",true);}
                if(poisonLeft<=0)SyringeAugmentVfx.ReleaseOwned(ref poisonVisual);
            }
        }
        public void Defeated()
        {
            if(defeated)return;defeated=true;SyringeAugmentVfx.ReleaseOwned(ref poisonVisual);SyringeAugmentVfx.ReleaseOwned(ref bacteriaVisual);
            if(acid){var a=acidSettings;var puddle=new GameObject("Boss digestive acid puddle").AddComponent<DigestiveAcidPuddle>();puddle.transform.position=transform.position;puddle.Init(a.digestiveAcidPuddleLifetime,a.digestiveAcidPuddleRadius,a.digestiveAcidPuddleDamagePerSecond,a.digestiveAcidPuddleTickInterval,a.digestiveAcidPuddleColor,acidSource,"소화액낭침");}
            var last=bacteriaSettings;
            if(bacteria.Count>=Mathf.Max(1,last.gutBacteriaRequiredStacks))
            {
                var manager=FindObjectOfType<EntityManager>();if(manager==null)return;
                int rank=last.ver4!=null?last.ver4.Count(SyringeSpecialAugmentAbility.SpecialAugmentType.GutBacteriaNeedle,2):0;
                int count=last.gutBacteriaBonusGemCount+(Random.value<OriginalCombatRules.BacteriaHarvestChance(rank,bacteria.Count)?1:0);
                for(int i=0;i<count;i++)manager.SpawnExpGem((Vector2)transform.position+Random.insideUnitCircle*last.gutBacteriaBonusGemSpawnRadius,last.gutBacteriaBonusGemType,true);
            }
        }
        void OnDisable(){SyringeAugmentVfx.ReleaseOwned(ref poisonVisual);SyringeAugmentVfx.ReleaseOwned(ref bacteriaVisual);poisonLeft=slowLeft=0;slowFactor=1;bacteria.Clear();acid=false;defeated=false;}
    }
}

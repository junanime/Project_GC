using System.Collections.Generic;
using UnityEngine;
using P = Vampire.SyringeSpecialAugmentAbility.SpecialAugmentType;
namespace Vampire
{
    public sealed class Ver4NeedleStatus : MonoBehaviour
    {
        private struct Burn { public float end, nextTick, damage; public Character source; public int stacks; }
        private readonly List<Burn> burns=new List<Burn>();
        private int burnStacks;
        private Component target;
        private NeuralBlockedMonsterStatus freeze;
        private float freezeCooldown, seedExpiry;
        private int seeds;
        private bool shatterPending;
        private void Awake() { target=GetComponent<IDamageable>(); }
        public bool ConsumeFrozen()
        {
            if(freeze==null || !freeze.IceFrozen) return false;
            freeze.ReleaseIce(); freezeCooldown=Time.time+2; shatterPending=true; return true;
        }
        public void Hit(SyringeSpecialRuntime runtime,Character source,LayerMask layer)
        {
            var s=runtime.ver4;
            if(s==null || target==null) return;
            ResolveShatter(runtime,source,layer);
            if(Ver4HitEffects.Health(target)<=0) return;
            ApplyFire(runtime,source);
            if(s.Has(P.IceNeedle) && Time.time>=freezeCooldown && Random.value<.20f+.05f*s.Count(P.IceNeedle,0))
            {
                if(Ver4HitEffects.IsBoss(target))
                {
                    if(target is Monster)
                    {
                        var slow=GetComponent<HoneySlowStatus>()??gameObject.AddComponent<HoneySlowStatus>(); slow.Apply(1,.8f);
                    }
                    else if(target is BossPartDamageTestPart)
                    {
                        var boss=target.GetComponentInParent<BossController>();
                        if(boss==null)
                        {
                            var root=target.GetComponentInParent<BossPartDamageTestRootController>();
                            if(root!=null) boss=root.GetComponentInChildren<BossController>();
                        }
                        if(boss!=null) boss.ApplyVer4IceChill(1);
                    }
                    freezeCooldown=Time.time+2;
                }
                else
                {
                    freeze=GetComponent<NeuralBlockedMonsterStatus>()??gameObject.AddComponent<NeuralBlockedMonsterStatus>();
                    freeze.ApplyIce(1); freezeCooldown=Time.time+3;
                }
            }
            if(s.Has(P.WoodNeedle))
            {
                if(Time.time>=seedExpiry) seeds=0;
                seedExpiry=Time.time+4;
                if(++seeds>=3)
                {
                    seeds=0; int branches=1+s.Count(P.WoodNeedle,0);
                    foreach(var other in Ver4HitEffects.Nearby(transform.position,2,layer,source))
                    {
                        if(other==target) continue;
                        bool killed=Ver4HitEffects.Damage(other,runtime.ver4HitDamage*.4f*s.Factor(P.WoodNeedle,1,.12f),Vector2.zero,source,"목침");
                        int ground=s.Count(P.WoodNeedle,2);
                        if(killed && ground>0 && !runtime.healingBlocked)
                        {
                            var clock=source.GetComponent<Ver4EffectClock>()??source.gameObject.AddComponent<Ver4EffectClock>();
                            if(clock.TryHealingGround()) Ver4GroundEffect.Create(other.transform.position,1+ground*.5f,true,0,source,layer);
                        }
                        if(--branches<=0) break;
                    }
                }
            }
        }
        public int BurnStacks => burnStacks;
        public void ApplyFire(SyringeSpecialRuntime runtime,Character source)
        {
            var s=runtime.ver4;
            if(s==null || target==null || Ver4HitEffects.Health(target)<=0)return;
            if(s.Has(P.FireNeedle) && Random.value<.25f)
            {
                int cap=s.Count(P.FireNeedle,2)==3 ? int.MaxValue : 3+s.Count(P.FireNeedle,2);
                if(burnStacks>=cap && burns.Count>0)
                {
                    var oldest=burns[0];oldest.stacks--;burnStacks--;
                    if(oldest.stacks==0) burns.RemoveAt(0); else burns[0]=oldest;
                }
                float damage=Ver4HitEffects.MaxHealth(target)*.005f*s.Factor(P.FireNeedle,0,.12f);
                if(Ver4HitEffects.IsBoss(target)) damage=Mathf.Min(damage,runtime.ver4HitDamage*.25f);
                var added=new Burn {end=Time.time+3*s.Factor(P.FireNeedle,1,.12f),nextTick=Time.time+1,damage=damage,source=source,stacks=1};
                int last=burns.Count-1;
                // Equal-timestamp stacks share bookkeeping, not a gameplay cap.
                if(last>=0 && burns[last].end==added.end && burns[last].nextTick==added.nextTick && burns[last].damage==damage && burns[last].source==source)
                { var batch=burns[last];batch.stacks++;burns[last]=batch; }
                else burns.Add(added);
                burnStacks++;
            }
            if(burnStacks>0 && GetComponent<ShiniBurnVisual>()==null) gameObject.AddComponent<ShiniBurnVisual>();
        }
        private void ResolveShatter(SyringeSpecialRuntime runtime,Character source,LayerMask layer)
        {
            if(!shatterPending) return;
            shatterPending=false;
            var s=runtime.ver4;
            if(s.Count(P.IceNeedle,1)>0)
                Ver4HitEffects.AreaDamage(transform.position,1,runtime.ver4HitDamage*.4f*s.Factor(P.IceNeedle,1,.15f),0,source,layer,"빙결침 쇄빙");
            int ground=s.Count(P.IceNeedle,2);
            if(ground>0) Ver4GroundEffect.Create(transform.position,1+ground*.5f,false,runtime.ver4HitDamage*.10f,source,layer);
        }
        private void Update()
        {
            if(target is Monster monster && monster.IsFieldRuntimeSuspended)
            {
                for(int i=0;i<burns.Count;i++)
                { var burn=burns[i]; burn.end+=Time.deltaTime; burn.nextTick+=Time.deltaTime; burns[i]=burn; }
                seedExpiry+=Time.deltaTime; freezeCooldown+=Time.deltaTime;
                return;
            }
            if(Ver4HitEffects.Health(target)<=0) { burns.Clear(); burnStacks=0; return; }
            for(int i=burns.Count-1;i>=0;i--)
            {
                var burn=burns[i];
                while(burn.nextTick<=Time.time && burn.nextTick<=burn.end+.0001f)
                {
                    for(int stack=0;stack<burn.stacks;stack++)
                    {
                        Ver4HitEffects.Damage(target,burn.damage,Vector2.zero,burn.source,"화염침",true);
                        if(!isActiveAndEnabled || Ver4HitEffects.Health(target)<=0) { burns.Clear(); burnStacks=0; return; }
                    }
                    burn.nextTick+=1;
                    // A pooled target may disable itself synchronously in TakeDamage.
                    if(!isActiveAndEnabled || Ver4HitEffects.Health(target)<=0) { burns.Clear(); burnStacks=0; return; }
                }
                if(Time.time>burn.end) {burns.RemoveAt(i);burnStacks-=burn.stacks;} else burns[i]=burn;
            }
        }
        private void OnDisable()
        {
            burns.Clear(); burnStacks=0; seeds=0; seedExpiry=freezeCooldown=0; shatterPending=false;
            if(freeze!=null) freeze.ReleaseIce(); freeze=null;
        }
    }
}

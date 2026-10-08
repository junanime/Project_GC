using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class OctoberItemTargetStatus : MonoBehaviour, ICombatStatus
    {
        public float VulnerableUntil,PoisonUntil,BurnUntil;
        public CombatStatusTag ActiveStatusTags =>
            (Time.time<VulnerableUntil?CombatStatusTag.Vulnerability:CombatStatusTag.None) |
            (Time.time<PoisonUntil?CombatStatusTag.Poison:CombatStatusTag.None) |
            (Time.time<BurnUntil?CombatStatusTag.Burn:CombatStatusTag.None);
        float poisonDamage,burnDamage,nextTick;
        OctoberItemRuntime owner;
        Component target;
        void Awake(){target=GetComponent<IDamageable>();}
        public void Poison(OctoberItemRuntime source,float duration,float damage){owner=source;PoisonUntil=Time.time+duration;poisonDamage=damage;}
        public void Burn(OctoberItemRuntime source,float duration,float damage){owner=source;BurnUntil=Time.time+duration;burnDamage=damage;}
        void Update()
        {
            if(target is Monster m&&m.IsFieldRuntimeSuspended){VulnerableUntil+=Time.deltaTime;PoisonUntil+=Time.deltaTime;BurnUntil+=Time.deltaTime;return;}
            if(owner==null||target==null||Ver4HitEffects.Health(target)<=0||Time.time<nextTick)return;
            nextTick=Time.time+1;
            if(PoisonUntil>Time.time)Ver4HitEffects.Damage(target,poisonDamage,Vector2.zero,owner.Player,"산초 독주머니",true);
            if(BurnUntil>Time.time)Ver4HitEffects.Damage(target,burnDamage,Vector2.zero,owner.Player,"뜸불 화로",true);
        }
        void OnDisable(){VulnerableUntil=PoisonUntil=BurnUntil=0;owner=null;}
    }
    public sealed class OctoberItemZone : MonoBehaviour
    {
        OctoberItemRuntime owner;
        float radius,end,dps,nextTick;
        int item;bool pull,lure;
        SpriteRenderer icon;
        public void Configure(OctoberItemRuntime source,Vector2 p,float r,float duration,float damage,int id,bool pulling,bool luring)
        {
            owner=source;transform.position=p;radius=r;end=Time.time+duration;dps=damage;item=id;pull=pulling;lure=luring;
            icon=source.MakeSprite(id,"지대",lure?.6f:r*.7f);icon.transform.SetParent(transform,true);icon.transform.position=p;icon.color=new Color(1,1,1,.6f);
        }
        void Update()
        {
            if(owner==null||!owner.Player.IsAlive||Time.time>=end){Destroy(gameObject);return;}
            if(Time.timeScale<=0||Time.time<nextTick)return;nextTick=Time.time+.25f;
            foreach(var target in owner.Nearby(transform.position,radius))
            {
                if(dps>0)Ver4HitEffects.Damage(target,dps*.25f,Vector2.zero,owner.Player,OctoberItemRuntime.Name(item),true);
                if(item==16)(target.GetComponent<OctoberItemTargetStatus>()??target.gameObject.AddComponent<OctoberItemTargetStatus>()).Burn(owner,.5f,0);
                if(!pull||Ver4HitEffects.IsBoss(target))continue;
                var body=target.GetComponent<Rigidbody2D>();if(body==null)continue;
                if(lure)(target.GetComponent<NeuralBlockedMonsterStatus>()??target.gameObject.AddComponent<NeuralBlockedMonsterStatus>()).Apply(.28f);
                Vector2 dest=Vector2.MoveTowards(body.position,transform.position,lure?.32f:.5f);
                body.MovePosition(dest);
            }
        }
    }
    public sealed class OctoberEnemyProjectile : MonoBehaviour
    {
        public static readonly HashSet<OctoberEnemyProjectile> Active=new HashSet<OctoberEnemyProjectile>();
        public Projectile pooled;
        public bool hostile;
        public bool IsHostile=>isActiveAndEnabled&&hostile;
        bool sealedProjectile;
        public static void Register(GameObject obj,bool isHostile,Projectile pool=null)
        {
            if(!isHostile)
            {var existing=obj.GetComponent<OctoberEnemyProjectile>();if(existing!=null){existing.hostile=false;Active.Remove(existing);}return;}
            var p=obj.GetComponent<OctoberEnemyProjectile>()??obj.AddComponent<OctoberEnemyProjectile>();p.hostile=isHostile;p.pooled=pool;p.sealedProjectile=false;p.enabled=true;Active.Add(p);
        }
        void OnEnable(){if(hostile)Active.Add(this);}
        void OnDisable(){Active.Remove(this);sealedProjectile=false;}
        public void Remove(){if(pooled!=null)pooled.RemoveForItem();else Destroy(gameObject);}
        public void Seal(float duration){if(!sealedProjectile)StartCoroutine(SealRoutine(duration));}
        IEnumerator SealRoutine(float duration)
        {
            sealedProjectile=true;
            var simple=GetComponent<BossSimpleBullet>();if(simple!=null)simple.enabled=false;
            var snail=GetComponent<SnailBossProjectile>();if(snail!=null)snail.enabled=false;
            if(pooled!=null)pooled.StopAllCoroutines();
            foreach(var col in GetComponents<Collider2D>())col.enabled=false;
            var rb=GetComponent<Rigidbody2D>();if(rb!=null)rb.velocity=Vector2.zero;
            yield return new WaitForSeconds(duration);Remove();
        }
    }
}

using UnityEngine;
namespace Vampire
{
    public sealed class SnailBossMinion : IDamageable
    {
        SnailBossRuntime owner;bool absorb;float hp,speed,life=25,contact;int ingredient;
        public float Health=>hp;
        public static SnailBossMinion Spawn(SnailBossRuntime owner,Vector2 pos,int ingredient,bool absorb)
        {
            var go=new GameObject(absorb?"Absorb mini roll cake":"Summoned mini roll cake");go.layer=LayerMask.NameToLayer("Monster Full");
            go.transform.SetParent(owner.EffectsRoot);go.transform.position=pos;
            var rb=go.AddComponent<Rigidbody2D>();rb.bodyType=RigidbodyType2D.Kinematic;
            var col=go.AddComponent<CircleCollider2D>();col.isTrigger=true;col.radius=.35f;
            var m=go.AddComponent<SnailBossMinion>();m.owner=owner;m.absorb=absorb;m.ingredient=ingredient;
            m.hp=owner.settings.summonHealth*(owner.Chocolate?1.3f:1);
            m.speed=absorb?(owner.Chocolate?owner.settings.chocolateAbsorbSpeed:owner.settings.vanillaAbsorbSpeed):owner.settings.summonSpeed;
            MakeArt(go.transform,owner.Chocolate,ingredient);
            return m;
        }
        public static void MakeArt(Transform root,bool chocolate,int ingredient)
        {
            var visualRoot=new GameObject("Mini snail visual");
            visualRoot.transform.SetParent(root,false);
            var visual=visualRoot.AddComponent<SnailMiniVisual>();
            var fieldActor=root.GetComponentInParent<AcidLeechMonster>();
            visual.Configure(fieldActor!=null?fieldActor.transform:root,chocolate,ingredient);
        }
        void Update()
        {
            if(owner==null||owner.Dead){Destroy(gameObject);return;}if(SnailBossRuntime.Paused)return;
            life-=Time.deltaTime;if(life<=0){Destroy(gameObject);return;}
            Vector2 target=absorb?(Vector2)owner.transform.position:owner.Player!=null?(Vector2)owner.Player.transform.position:(Vector2)transform.position;
            transform.position=Vector2.MoveTowards(transform.position,target,speed*Time.deltaTime);
            if(absorb&&Vector2.Distance(transform.position,target)<.65f)
            {owner.HealFraction(SnailBossRules.AbsorbFraction(owner.Chocolate));SnailBossVfx.Burst(owner,transform.position,owner.Chocolate?"ChocolateSplash":"CreamSplash",3,.3f);Destroy(gameObject);}
            else if(!absorb&&Vector2.Distance(transform.position,target)<.45f&&Time.time>contact)
            {owner.Player?.TakeDamage(5);contact=Time.time+1;}
        }
        public override void TakeDamage(float damage,Vector2 knockback=default,bool isCritical=false)
        {
            if(SnailBossRuntime.Paused||damage<=0)return;hp-=damage;if(hp<=0)Destroy(gameObject);
            // Summoned/absorbing minis never alter field progress or spawn rewards.
        }
        public override void Knockback(Vector2 knockback){transform.position+=(Vector3)(knockback*.02f);}
    }
}

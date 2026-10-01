using UnityEngine;
namespace Vampire
{
    public sealed class SnailBossProjectile : MonoBehaviour
    {
        enum Kind{Linear,Bomb,Homing,Mine}
        Kind kind;SnailBossRuntime owner;Vector2 velocity,start,target;float age,life=8,damage,radius,trailClock;bool chocolate;SpriteRenderer art;string trail;
        public static SnailBossProjectile Linear(SnailBossRuntime owner,string key,Vector2 position,Vector2 direction,float speed,float width,float damage,string trail)
        {
            var p=Create(owner,key,position,width);p.kind=Kind.Linear;p.velocity=direction*speed;p.damage=damage;p.radius=width*.38f;p.trail=trail;return p;
        }
        static SnailBossProjectile Create(SnailBossRuntime owner,string key,Vector2 position,float width)
        {
            var go=new GameObject(key+" projectile");go.transform.SetParent(owner.EffectsRoot);go.transform.position=position;
            var p=go.AddComponent<SnailBossProjectile>();p.owner=owner;p.chocolate=owner.Chocolate;
            p.art=SnailBossArt.Make(go.transform,key,width,520);return p;
        }
        public static void Bomb(SnailBossRuntime owner,Vector2 from,Vector2 to)
        {
            var p=Create(owner,owner.Chocolate?"Kisses":"Strawberry",from,owner.Chocolate?.48f:.8f);
            p.kind=Kind.Bomb;p.start=from;p.target=to;p.life=owner.settings.bombFlight;
            p.radius=owner.Chocolate?owner.settings.kissesRadius:owner.settings.strawberryRadius;
            SnailBossVfx.Warning(owner,to,p.radius,p.life,owner.Chocolate);
        }
        public static void Homing(SnailBossRuntime owner,Vector2 position)
        {
            var p=Create(owner,owner.Chocolate?"Bar":"Mango",position,.8f);p.kind=Kind.Homing;p.life=owner.settings.homingDuration;
        }
        void Update()
        {
            if(owner==null||owner.Dead){Destroy(gameObject);return;}if(SnailBossRuntime.Paused)return;
            age+=Time.deltaTime;Vector2 previous=transform.position;
            if(kind==Kind.Linear)
            {
                transform.position+=(Vector3)velocity*Time.deltaTime;
                if(owner.Player!=null&&SegmentDistance(owner.Player.transform.position,previous,transform.position)<radius+.2f)
                {owner.Player.TakeDamage(damage);Destroy(gameObject);return;}
            }
            else if(kind==Kind.Bomb)
            {
                float t=Mathf.Clamp01(age/life);transform.position=Vector2.Lerp(start,target,t)+Vector2.up*(Mathf.Sin(t*Mathf.PI)*2);
                art.transform.localRotation=Quaternion.Euler(0,0,age*220);
                if(age>=life){Explode();return;}
            }
            else if(kind==Kind.Homing)
            {
                if(owner.Player!=null)transform.position=Vector2.MoveTowards(transform.position,owner.Player.transform.position,2.2f*Time.deltaTime);
                art.transform.localRotation=Quaternion.Euler(0,0,Mathf.Sin(age*8)*12);
                if(age>=life){kind=Kind.Mine;age=0;life=owner.settings.mineLifetime;SnailBossArt.Set(art,chocolate?"Wrapper":"Mango",.85f);art.transform.localRotation=Quaternion.identity;return;}
            }
            else if(kind==Kind.Mine)
            {
                art.color=Color.Lerp(Color.white,new Color(1,.8f,.45f),(.5f+.5f*Mathf.Sin(age*5))*.3f);
                if(owner.IsDashing&&Vector2.Distance(owner.transform.position,transform.position)<1.35f)
                {owner.TriggerTrap();Destroy(gameObject);return;}
            }
            if(trail!=null&&(trailClock-=Time.deltaTime)<=0){trailClock=.12f;SnailBossVfx.Puff(owner,trail,previous,.22f,.35f);}
            if(age>=life)Destroy(gameObject);
        }
        void Explode()
        {
            if(owner.Player!=null&&Vector2.Distance(owner.Player.transform.position,target)<=radius)
                owner.Player.TakeDamage(owner.settings.bombDamage);
            SnailBossVfx.Burst(owner,target,chocolate?"ChocolateSplash":"Pulp",chocolate?8:13,radius);
            if(chocolate)SnailBossPuddle.Circle(owner,target,radius,owner.settings.kissesPuddleDuration,owner.settings.chocolateSlow,owner.settings.kissesSlowTail);
            Destroy(gameObject);
        }
        public static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b)
        {Vector2 d=b-a;float t=d.sqrMagnitude>.0001f?Mathf.Clamp01(Vector2.Dot(p-a,d)/d.sqrMagnitude):0;return Vector2.Distance(p,a+d*t);}
    }
}

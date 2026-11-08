using System.Collections;
using UnityEngine;

namespace Vampire
{
    public static class ColonBossEffects
    {
        public static LineRenderer Warning(ColonBossRuntime boss,Vector2[] outline,Vector2? origin=null)
        {
            var c=boss.Definition.Warning;c.a=.70f;
            // Existing hazard shader adds diagonal hatching and translucent interior, not a decorative outline alone.
            var line=FoodBossEffects.Line(boss.Effects,outline,.075f,c,false,480);
            ColonAttackWall.Mask(line.gameObject,origin??(Vector2)boss.transform.position);return line;
        }
        public static Vector2[] Circle(Vector2 at,float radius)=>FoodBossEffects.Arc(at,radius);
        public static Vector2[] Lane(Vector2 a,Vector2 b,float width)
        {
            var n=Vector2.Perpendicular((b-a).normalized)*width*.5f;
            return new[]{a+n,b+n,b-n,a-n,a+n};
        }
        public static Vector2[] Fan(Vector2 at,Vector2 direction,float radius,float angle)=>FoodBossEffects.FanOutline(at,direction,radius,angle);
        public static void Fade(LineRenderer line,float duration=.2f) { if(line!=null)line.gameObject.AddComponent<FoodBossFade>().Lifetime=duration; }
        public static void Burst(ColonBossRuntime b,Vector2 at,float radius)
        {
            var p=Circle(at,radius);var c=b.Definition.Warning;c.a=.8f;
            var r=FoodBossEffects.Line(b.Effects,p,.17f,c,false,485,true);r.gameObject.AddComponent<FoodBossFade>().Lifetime=.35f;ColonAttackWall.Mask(r.gameObject,at);
            var sprite=Prop(b,"Impact",radius*1.6f,at,522);
            sprite.gameObject.AddComponent<ColonBossImpact>().Initialize(b,ColonBossVisual.Load(b.Definition.Id,"Impact"));
        }
        public static void Stain(ColonBossRuntime b,Vector2 at,float width)
        {
            var r=Prop(b,"Secondary",width,at,482);
            r.gameObject.AddComponent<ColonBossImpact>().Initialize(b,new[]{r.sprite},1.3f);
        }
        public static SpriteRenderer Prop(ColonBossRuntime b,string sheet,float size,Vector2 position,int order=514)
        {
            var art=ColonBossVisual.Load(b.Definition.Id,sheet);
            var r=new GameObject(sheet).AddComponent<SpriteRenderer>();r.transform.SetParent(b.Effects,true);r.transform.position=position;r.sortingOrder=order;
            if(art.Length>0){r.sprite=art[0];r.transform.localScale=Vector3.one*size/r.sprite.bounds.size.x;}return r;
        }
        public static ColonBossProjectile Projectile(ColonBossRuntime b,Vector2 position,Vector2 direction,float speed,float damage,float radius=.20f,string shape="seed")
            =>ColonBossProjectile.Create(b,position,direction,speed,damage,radius,shape);
        public static SpriteRenderer PuddingBall(ColonBossRuntime b,Vector2 point,float size)
        {
            var r=Prop(b,"Projectile",size,point);
            if(r.sprite==null){r.sprite=BombCircleArt.Sprite;r.color=new Color(1,.77f,.34f);r.transform.localScale=Vector3.one*size/r.sprite.bounds.size.x;}
            return r;
        }
        public static IEnumerator Wave(ColonBossRuntime b,Vector2 origin,Vector2 direction,float angle,float range,float duration,float damage)
        {
            float heading=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg;bool hit=false;
            var c=b.Definition.Warning;c.a=.85f;
            var r=FoodBossEffects.Line(b.Effects,new[]{origin,origin},.30f,c,false,486,true);
            var inner=FoodBossEffects.Line(b.Effects,new[]{origin,origin},.07f,new Color(1,.96f,.74f,.9f),false,487,true);
            ColonAttackWall.Mask(r.gameObject,origin);ColonAttackWall.Mask(inner.gameObject,origin);
            yield return b.Tween(duration,q=>{
                float radius=Mathf.Lerp(.25f,range,q);var p=FoodBossEffects.Arc(origin,radius,heading-angle/2,angle,40);
                r.positionCount=inner.positionCount=p.Length;for(int i=0;i<p.Length;i++){r.SetPosition(i,p[i]);inner.SetPosition(i,p[i]);}
                var delta=(Vector2)b.Player.transform.position-origin;
                if(!hit&&ColonAttackWall.Clear(origin,b.Player.transform.position)&&Mathf.Abs(delta.magnitude-radius)<.45f&&Vector2.Angle(direction,delta)<=angle/2){b.TryHit(damage);hit=true;}
            });
            Fade(r);Fade(inner);
        }
        public static IEnumerator Liquid(ColonBossRuntime b,Vector2 a,Vector2 end,float width,float duration,float damage)
        {
            var ribbon=FoodBossFlowRibbon.Create(b.Effects,new Color(.7f,.84f,.36f,.85f),484,1);
            var line=ribbon.gameObject.AddComponent<ColonLiquidZone>();line.Init(b,a,end,width,duration,damage,ribbon);ColonAttackWall.Mask(ribbon.gameObject,a);
            yield return b.Wait(duration);
        }
    }
    public sealed class ColonBossProjectile:MonoBehaviour
    {
        ColonBossRuntime owner;Vector2 velocity;float age,damage,radius;SpriteRenderer sprite;
        public Vector2 Velocity=>velocity;
        public static ColonBossProjectile Create(ColonBossRuntime b,Vector2 at,Vector2 dir,float speed,float damage,float radius,string shape)
        {
            var go=new GameObject("Colon pattern · "+shape);go.transform.SetParent(b.Effects,true);go.transform.position=at;
            var p=go.AddComponent<ColonBossProjectile>();p.owner=b;p.velocity=dir.normalized*speed;p.damage=damage;p.radius=radius;
            p.sprite=new GameObject("Authored material projectile").AddComponent<SpriteRenderer>();p.sprite.transform.SetParent(go.transform,false);p.sprite.sortingOrder=524;
            var frames=ColonBossVisual.Load(b.Definition.Id,shape=="crescent"||shape=="donut"?"Secondary":"Projectile");
            if(frames.Length>0){p.sprite.sprite=frames[0];p.sprite.transform.localScale=Vector3.one*(radius*(shape=="crescent"?4:2.6f))/Mathf.Max(p.sprite.sprite.bounds.size.x,p.sprite.sprite.bounds.size.y);}
            p.sprite.transform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(dir.y,dir.x)*Mathf.Rad2Deg);
            OctoberEnemyProjectile.Register(go,true);return p;
        }
        void Update()
        {
            if(owner==null||owner.Dead){Destroy(gameObject);return;}if(owner.EncounterPaused)return;
            float dt=Time.deltaTime;age+=dt;Vector2 before=transform.position;transform.position+=(Vector3)velocity*dt;
            if(age>6||!ColonAttackWall.Clear(before,transform.position,radius)){Destroy(gameObject);return;}
            if(SnailBossProjectile.SegmentDistance(owner.Player.transform.position,before,transform.position)<radius+.16f){owner.TryHit(damage);ColonBossEffects.Burst(owner,transform.position,radius*1.5f);Destroy(gameObject);}
        }
    }
    public sealed class ColonBossImpact:MonoBehaviour
    {
        ColonBossRuntime owner;Sprite[] frames;SpriteRenderer art;float age,lifetime;
        public void Initialize(ColonBossRuntime b,Sprite[] sprites,float seconds=.3f){owner=b;frames=sprites;art=GetComponent<SpriteRenderer>();lifetime=seconds;}
        void Update(){if(owner==null||owner.Dead){Destroy(gameObject);return;}if(owner.EncounterPaused)return;age+=Time.deltaTime;if(age>=lifetime){Destroy(gameObject);return;}if(frames.Length>0)art.sprite=frames[Mathf.Min(frames.Length-1,(int)(age/lifetime*frames.Length))];art.color=new Color(1,1,1,Mathf.Clamp01((lifetime-age)/Mathf.Min(.3f,lifetime)));}
    }
    public sealed class ColonLiquidZone:MonoBehaviour
    {
        ColonBossRuntime owner;Vector2 start,end;float width,duration,age,damage,next;FoodBossFlowRibbon ribbon;
        public void Init(ColonBossRuntime b,Vector2 a,Vector2 z,float w,float seconds,float hit,FoodBossFlowRibbon r){owner=b;start=a;end=ColonAttackWall.Stop(a,z,w*.5f+.1f);width=w;duration=seconds;damage=hit;ribbon=r;}
        void Update()
        {
            if(owner==null||owner.Dead){Destroy(gameObject);return;}if(owner.EncounterPaused)return;age+=Time.deltaTime;
            if(age>=duration){Destroy(gameObject);return;}
            var p=new Vector2[41];float advance=Mathf.Clamp01(age/1.5f);var normal=Vector2.Perpendicular((end-start).normalized);
            for(int i=0;i<p.Length;i++){float u=i/40f*advance;p[i]=Vector2.Lerp(start,end,u)+normal*Mathf.Sin(u*11-age*6)*.10f*Mathf.Sin(u*Mathf.PI);}
            ribbon.Set(p,width,age,Mathf.Clamp01((duration-age)/.55f));
            if(owner.Player==null||Time.time<next||!ColonAttackWall.Clear(start,owner.Player.transform.position))return;for(int i=1;i<p.Length;i++)if(SnailBossProjectile.SegmentDistance(owner.Player.transform.position,p[i-1],p[i])<width*.5f+.15f){owner.TryHit(damage);next=Time.time+.7f;break;}
        }
    }
    public sealed partial class ColonBossRuntime
    {
        float nextPatternHit;
        public bool TryHit(float damage)
        {
            if(Dead||Arriving||EncounterPaused||damage<=0||float.IsNaN(damage)||float.IsInfinity(damage)||Time.time<nextPatternHit)return false;
            Player.TakeDamage(damage);nextPatternHit=Time.time+.30f;return true;
        }
    }
}

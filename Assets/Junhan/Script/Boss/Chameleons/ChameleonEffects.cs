using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    public sealed class ChameleonProjectile : MonoBehaviour
    {
        Character player;SpriteRenderer art;Vector2 velocity;float age,damage;
        public static ChameleonProjectile Fire(ChameleonKind kind,Vector2 start,Vector2 direction,float speed,float damage,Character player)
        {
            var go=new GameObject(kind+" projectile");go.transform.position=start;
            var p=go.AddComponent<ChameleonProjectile>();p.player=player;p.damage=damage;p.velocity=direction.normalized*speed;
            p.art=new GameObject("Centered projectile art").AddComponent<SpriteRenderer>();p.art.transform.SetParent(go.transform,false);p.art.sprite=ChameleonArt.Projectile(kind);p.art.transform.localPosition=-p.art.sprite.bounds.center;p.art.sortingOrder=520;
            go.transform.localScale=Vector3.one*(.42f/ChameleonArt.Width);OctoberEnemyProjectile.Register(go,true);return p;
        }
        void Update()
        {
            if(MiniStageRuntimeState.IsInsideMiniStage){Destroy(gameObject);return;}if(ChameleonTime.Paused)return;
            age+=Time.deltaTime;if(age>8){Destroy(gameObject);return;}
            Vector2 before=transform.position;transform.position+=(Vector3)velocity*Time.deltaTime;
            if(player!=null&&player.IsAlive&&SnailBossProjectile.SegmentDistance(player.transform.position,before,transform.position)<.32f){player.TakeDamage(damage);Destroy(gameObject);}
        }
    }
    // Exactly one moving sprite/one hit region, never a tiled or stretched multi-part jet.
    public sealed class ChameleonSodaWave : MonoBehaviour
    {
        Vector2 from,to;float age,duration,damage,radius,cooldown;Character player;SpriteRenderer art;bool monsters;
        float nextHit;readonly Dictionary<Monster,float> hits=new Dictionary<Monster,float>();
        public static ChameleonSodaWave Fire(Vector2 from,Vector2 to,float duration,float height,float damage,Character player,bool monsters=false,float cooldown=.6f)
        {
            var go=new GameObject("Single Fanta wave");go.transform.position=from;
            var w=go.AddComponent<ChameleonSodaWave>();w.from=from;w.to=to;w.duration=Mathf.Max(.2f,duration);w.damage=damage;w.player=player;w.monsters=monsters;w.cooldown=cooldown;
            w.art=new GameObject("Centered wave art").AddComponent<SpriteRenderer>();w.art.transform.SetParent(go.transform,false);w.art.sprite=ChameleonArt.Fx(4);w.art.transform.localPosition=-w.art.sprite.bounds.center;w.art.sortingOrder=515;
            if(w.art.sprite!=null)go.transform.localScale=Vector3.one*(height/w.art.sprite.bounds.size.y);
            w.radius=height*.5f;w.art.flipX=to.x<from.x;return w;
        }
        void Update()
        {
            art.enabled=!MiniStageRuntimeState.IsInsideMiniStage;if(ChameleonTime.Paused)return;
            Vector2 before=transform.position;age+=Time.deltaTime;float t=Mathf.Clamp01(age/duration);transform.position=Vector2.Lerp(from,to,t);
            if(player!=null&&player.IsAlive&&age>=nextHit&&SnailBossProjectile.SegmentDistance(player.transform.position,before,transform.position)<radius+.18f)
            {player.TakeDamage(damage,(to-from).normalized*1.3f);nextHit=age+cooldown;}
            if(monsters)
            {
                var manager=Object.FindObjectOfType<EntityManager>();
                if(manager!=null)foreach(var m in new List<Monster>(manager.LivingMonsters))
                    if(m!=null&&!(m is AcidToadMonster)&&m.HP>0&&!m.IsFieldRuntimeSuspended&&(!hits.TryGetValue(m,out float at)||age>=at)&&SnailBossProjectile.SegmentDistance(m.Position,before,transform.position)<radius+.2f)
                    {hits[m]=age+cooldown;m.TakeDamage(damage);}
            }
            if(t>=1)Destroy(gameObject);
        }
    }
    public sealed class ChameleonDriftPulse : MonoBehaviour
    {
        float remaining;Vector2 direction;Character player;EntityManager manager;
        public static ChameleonDriftPulse Create(Transform owner,Character player,Vector2 direction,float seconds)
        {
            var p=new GameObject("Miniboss peristaltic pulse").AddComponent<ChameleonDriftPulse>();p.transform.SetParent(owner,false);p.remaining=seconds;p.direction=direction;p.player=player;p.manager=Object.FindObjectOfType<EntityManager>();
            ToadCameraShake.Play(.28f,.08f);return p;
        }
        void Update(){if(ChameleonTime.Paused)return;remaining-=Time.deltaTime;if(remaining<=0)Destroy(gameObject);}
        void FixedUpdate()
        {
            if(ChameleonTime.Paused)return;
            if(player!=null)Push(player.GetComponent<Rigidbody2D>(),direction,7,4);
            if(manager!=null)foreach(var m in manager.LivingMonsters)if(m!=null&&m.HP>0&&!m.IsMiniStageOwned&&!m.IsFieldRuntimeSuspended)Push(m.GetComponent<Rigidbody2D>(),direction,5,4);
        }
        public static void Push(Rigidbody2D rb,Vector2 direction,float force,float max)
        {if(rb==null||!rb.simulated)return;rb.velocity+=direction*force*Time.fixedDeltaTime;float speed=Vector2.Dot(rb.velocity,direction);if(speed>max)rb.velocity-=direction*(speed-max);}
    }
    public sealed class ChameleonFoamSkill : MonoBehaviour
    {
        readonly List<AntacidBubbleZone> zones=new List<AntacidBubbleZone>();Character player;float age,tick;Transform owner;
        public static ChameleonFoamSkill Create(Transform owner,Character player)
        {
            var p=new GameObject("Miniboss antacid zones").AddComponent<ChameleonFoamSkill>();p.owner=owner;p.player=player;
            var prefab=ChameleonSettings.Current.foamPrefab;
            for(int i=0;i<3;i++)
            {
                float a=i*Mathf.PI*2/3;Vector2 center=(Vector2)player.transform.position+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*2.2f;
                var obj=Instantiate(prefab,center,Quaternion.identity);obj.transform.SetParent(p.transform,true);
                var zone=obj.GetComponent<AntacidBubbleZone>();zone.Init(1.8f,6,.35f,Color.white,false);p.zones.Add(zone);
            }
            GameAudioManager.PlaySfx(GameAudioManager.GameSfxId.AntacidBubbleSpawn);return p;
        }
        void Update()
        {
            if(owner==null){Destroy(gameObject);return;}
            if(ChameleonTime.Paused)return;age+=Time.deltaTime;if(age>=6){Destroy(gameObject);return;}
            if(player==null||!player.IsAlive)return;
            Vector2 at=player.CenterTransform!=null?player.CenterTransform.position:player.transform.position;
            foreach(var z in zones)if(z!=null&&z.ContainsPoint(at)){tick=0;return;}
            if(age<1)return;tick+=Time.deltaTime;if(tick>=.5f){tick=0;player.TakePeriodicDamage(3,Vector2.zero,false);}
        }
    }
    public sealed class ChameleonCoffeeFountain : MonoBehaviour
    {
        LineRenderer flow,crema;SpriteRenderer[] drops;float age;Vector3 origin;bool applied;AcidToadMonster source;Character player;
        public static ChameleonCoffeeFountain Create(AcidToadMonster source,Vector3 mouth,Character player)
        {
            var f=new GameObject("Latte upward fountain and rain").AddComponent<ChameleonCoffeeFountain>();f.transform.SetParent(source.transform,true);f.origin=mouth;f.source=source;f.player=player;
            f.flow=AcidJet.Line(f.transform,new Color(.58f,.32f,.13f),.36f,518);f.flow.endColor=new Color(.84f,.59f,.31f);f.flow.endWidth=.2f;
            f.crema=AcidJet.Line(f.transform,new Color(1,.89f,.69f),.065f,519);f.crema.endWidth=.025f;
            f.drops=new SpriteRenderer[9];for(int i=0;i<f.drops.Length;i++){var r=new GameObject("Falling latte drop").AddComponent<SpriteRenderer>();r.transform.SetParent(f.transform,false);r.sprite=ChameleonArt.Projectile(ChameleonKind.Latte);r.sortingOrder=519;r.transform.localScale=Vector3.one*(.18f/ChameleonArt.Width);r.enabled=false;f.drops[i]=r;}return f;
        }
        void Update()
        {
            flow.enabled=crema.enabled=!MiniStageRuntimeState.IsInsideMiniStage;if(ChameleonTime.Paused)return;
            age+=Time.deltaTime;float launch=Mathf.Clamp01(age/.8f),fall=Mathf.Clamp01((age-.8f)/.8f);
            for(int i=0;i<20;i++){float t=i/19f;Vector3 p=origin+new Vector3(Mathf.Sin(t*Mathf.PI)*fall*3+Mathf.Sin(t*9+age*8)*.04f*t,(t*4*launch-fall*t*t*5),0);flow.SetPosition(i,p);crema.SetPosition(i,p+Vector3.left*.06f);}
            for(int i=0;i<drops.Length;i++){float t=Mathf.Clamp01((age-.6f-i*.035f)/1.1f);drops[i].enabled=age>.6f+i*.035f;drops[i].transform.position=origin+new Vector3((i-4)*.43f*t,3.6f-5*t*t,0);}
            if(age>=1.6f&&!applied)
            {
                applied=true;var manager=Object.FindObjectOfType<EntityManager>();
                if(manager!=null)foreach(var m in manager.LivingMonsters)if(m!=null&&m!=source&&m.HP>0&&!m.IsMiniStageOwned&&!m.IsFieldRuntimeSuspended)
                    (m.GetComponent<CoffeeMonsterBuffRuntime>()??m.gameObject.AddComponent<CoffeeMonsterBuffRuntime>()).ApplySkill(player,ChameleonSettings.Current.coffeeSeconds,2.5f,2);
            }
            if(age>1.9f)Destroy(gameObject);
        }
    }
}

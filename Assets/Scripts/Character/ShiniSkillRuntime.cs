using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(500)]
    public sealed class ShiniSkillRuntime : MonoBehaviour
    {
        public const float SpawnInterval=2, PoolLifetime=6, SummonDuration=.6f, HitDelay=.6f, EruptionDuration=1.6f;
        sealed class Pool
        {
            public Vector3 position; public float born, radius, eruption=-1; public bool hit;
            public SpriteRenderer ground, tornado;
        }
        Character owner; CharacterSkillRuntime skill; SpriteRenderer body; SyringeDartAbility needle;
        readonly List<Pool> pools=new List<Pool>();
        readonly Dictionary<Component,float> nextBurn=new Dictionary<Component,float>();
        readonly List<Component> stale=new List<Component>();
        readonly HashSet<Component> hitWave=new HashSet<Component>();
        readonly Dictionary<Sprite,Sprite> forward=new Dictionary<Sprite,Sprite>(), reverse=new Dictionary<Sprite,Sprite>();
        Vector3 previous; float movedSeconds, bodyWidth;
        public int PoolCount => pools.Count;
        public float CurrentSpawnInterval => skill.Active ? Mathf.Clamp(skill.Definition.shiniActivePoolInterval,.1f,SpawnInterval) : SpawnInterval;
        public void Bind(Character character,CharacterSkillRuntime runtime)
        {
            owner=character;skill=runtime;previous=transform.position;
            body=GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if(!skill.IsShini)return;
            bodyWidth=body!=null?body.bounds.size.x:.6f;
            Map(owner.Blueprint.idleSpriteSequence,skill.Definition.burningIdle);
            Map(owner.Blueprint.walkSpriteSequence,skill.Definition.burningWalk);
            Map(owner.Blueprint.dashSpriteSequence,skill.Definition.burningDash);
        }
        void Map(Sprite[] from,Sprite[] to)
        { if(from==null||to==null)return;for(int i=0;i<Mathf.Min(from.Length,to.Length);i++){forward[from[i]]=to[i];reverse[to[i]]=from[i];} }
        Vector3 Feet => body!=null ? body.transform.position-Vector3.up*(.27f*Mathf.Abs(body.transform.lossyScale.y)) : transform.position-Vector3.up*.27f;
        void LateUpdate()
        {
            if(skill==null||!skill.IsShini)return;
            if(!owner.IsAlive){Clear();return;}
            if(body!=null&&body.sprite!=null)
            {
                Sprite original=reverse.TryGetValue(body.sprite,out var normal)?normal:body.sprite;
                body.sprite=skill.Active&&!skill.IsSummoning&&forward.TryGetValue(original,out var burning)?burning:original;
            }
            if(Time.deltaTime<=0)return;
            UpdatePools(Time.time);
        }
        void FixedUpdate()
        {
            if(skill==null||!skill.IsShini||!owner.IsAlive)return;
            Vector3 position=transform.position;
            // Physics ticks avoid undercounting movement on render frames between physics updates.
            AdvanceMovement((position-previous).sqrMagnitude>.000001f,Time.fixedDeltaTime);previous=position;
        }
        public void AdvanceMovement(bool moving,float delta)
        {
            if(!moving||delta<=0)return;
            movedSeconds+=delta;
            float interval=CurrentSpawnInterval;
            if(movedSeconds>=interval){movedSeconds%=interval;SpawnPool(Feet,Time.time);}
        }
        SpriteRenderer Make(string name,Vector3 position)
        {
            var renderer=new GameObject(name).AddComponent<SpriteRenderer>();
            renderer.transform.position=position;renderer.sortingLayerName="GroundEffects";
            return renderer;
        }
        public void SpawnPool(Vector3 position,float now)
        {
            var p=new Pool {position=position,born=now,radius=Mathf.Max(.2f,bodyWidth),ground=Make("Shini lava pool",position),tornado=Make("Shini fire tornado",position)};
            p.tornado.sortingOrder=1;p.tornado.enabled=false;pools.Add(p);
            if(skill.Active)p.eruption=now;
            UpdateVisual(p,now);
        }
        public void ActivatePools()
        {
            if(skill==null || !skill.IsShini || !owner.IsAlive || owner.IsTrapBound)return;
            // Active activation and successful dashes share the existing field pools.
            // Do not restart an in-flight eruption or create an extra pool on dash.
            foreach(var p in pools)
                if(Time.time-p.born<PoolLifetime && (p.eruption<0 || Time.time-p.eruption>=EruptionDuration))
                {p.eruption=Time.time;p.hit=false;UpdateVisual(p,Time.time);}
        }
        void UpdateVisual(Pool p,float now)
        {
            var frames=skill.Definition.lavaFrames;
            if(frames==null||frames.Length!=24)return;
            p.ground.sprite=frames[(int)((now-p.born)*6)%6];
            // All imported frames share an identical, stationary footprint and foot pivot.
            float scale=p.radius*2/(190f/100);
            p.ground.transform.localScale=Vector3.one*scale;
            p.ground.enabled=now-p.born<PoolLifetime;
            float t=now-p.eruption;
            p.tornado.enabled=p.eruption>=0&&t<EruptionDuration;
            if(!p.tornado.enabled)return;
            int frame=t<.2f?6: t<.6f?7+Mathf.Min(4,(int)((t-.2f)/.4f*5)):t<1.2f?12+Mathf.Min(5,(int)((t-.6f)/.6f*6)):18+Mathf.Min(5,(int)((t-1.2f)/.4f*6));
            p.tornado.sprite=frames[frame];
            // A narrow upright column; the lower footprint remains rendered on the ground.
            float height=Camera.main!=null?Camera.main.orthographicSize*2*.267f:3;
            p.tornado.transform.localScale=new Vector3(scale,Mathf.Max(scale,height/2.05f),1);
        }
        public void UpdatePools(float now)
        {
            if(needle==null)needle=FindObjectOfType<SyringeDartAbility>();
            stale.Clear();foreach(var pair in nextBurn)if(pair.Key==null||pair.Value<=now)stale.Add(pair.Key);
            foreach(var key in stale)nextBurn.Remove(key);
            hitWave.Clear();
            foreach(var p in pools)
            {
                UpdateVisual(p,now);
                if(needle==null)continue;
                bool eruption=p.eruption>=0&&!p.hit&&now-p.eruption>=HitDelay;
                if(eruption)p.hit=true;
                if(now-p.born>=PoolLifetime&&!eruption)continue;
                foreach(var collider in Physics2D.OverlapCircleAll(p.position,p.radius,needle.SkillTargetLayer))
                {
                    if(!SyringeSpecialHitEffectUtility.TryGetValidDamageableTarget(collider,owner,out _,out var target,out _) || target is Monster m&&m.IsFieldRuntimeSuspended)continue;
                    if(now-p.born<PoolLifetime&&!nextBurn.ContainsKey(target))
                    {
                        nextBurn[target]=now+1;
                        var runtime=needle.GetCurrentSpecialRuntime();runtime.ver4HitDamage=NonCriticalDamage(target);
                        (target.GetComponent<Ver4NeedleStatus>()??target.gameObject.AddComponent<Ver4NeedleStatus>()).ApplyFire(runtime,owner,true);
                    }
                    if(eruption&&hitWave.Add(target))Detonate(target);
                }
            }
            for(int i=pools.Count-1;i>=0;i--)
            {
                var p=pools[i];
                if(now-p.born>=PoolLifetime&&(p.eruption<0||now-p.eruption>=EruptionDuration))
                {Destroy(p.ground.gameObject);Destroy(p.tornado.gameObject);pools.RemoveAt(i);}
            }
        }
        float NonCriticalDamage(Component target)
        {
            float damage=needle.GetEffectiveDamage();var stats=PlayerGeneralStatRuntime.GetOrCreate(owner);
            if(stats!=null)damage=stats.ApplyBossDamageBonus(target,damage);
            var corrosion=target.GetComponent<CorrosionStatus>();if(corrosion!=null)damage*=corrosion.GetDamageTakenMultiplier();
            return damage;
        }
        public void Detonate(Component target)
        {
            if(needle==null)needle=FindObjectOfType<SyringeDartAbility>();
            if(needle==null||Ver4HitEffects.Health(target)<=0)return;
            var status=target.GetComponent<Ver4NeedleStatus>();int stacks=0;
            float remaining=status!=null?status.ConsumeBurn(out stacks):0;
            Ver4HitEffects.Damage(target,NonCriticalDamage(target)*(1+1.5f*stacks)+remaining,Vector2.zero,owner,"불꽃 토네이도");
        }
        void Clear(){foreach(var p in pools){if(p.ground!=null)Destroy(p.ground.gameObject);if(p.tornado!=null)Destroy(p.tornado.gameObject);}pools.Clear();nextBurn.Clear();movedSeconds=0;previous=transform.position;}
        void OnDisable(){Clear();}
    }
    public sealed class ShiniBurnVisual : MonoBehaviour
    {
        SpriteRenderer visual,body,overlay;Ver4NeedleStatus status;CharacterSkillDefinition art;float started;
        Material tintMaterial;
        public bool TintVisible => overlay!=null&&overlay.enabled;
        void Awake()
        {
            status=GetComponent<Ver4NeedleStatus>();body=SyringeAugmentVfx.FindTarget(this);
            art=Resources.Load<CharacterSkillDefinition>("ShiniSkills");started=Time.time;
            visual=new GameObject("Body burn flames").AddComponent<SpriteRenderer>();visual.transform.SetParent(transform,false);
            overlay=new GameObject("Burn orange silhouette").AddComponent<SpriteRenderer>();
            // Exact body transform/sprite mask: preserve the existing silhouette and pixel edges.
            overlay.transform.SetParent(body!=null?body.transform:transform,false);
            tintMaterial=new Material(Shader.Find("Vampire/IceChillTint"));
            overlay.sharedMaterial=tintMaterial;overlay.enabled=false;
        }
        void LateUpdate()
        {
            if(visual==null)return;
            bool burning=status!=null&&status.BurnStacks>0&&body!=null&&body.enabled&&SyringeAugmentVfx.IsLiving(this);
            overlay.enabled=burning;
            if(burning)
            {
                overlay.sprite=body.sprite;overlay.flipX=body.flipX;overlay.flipY=body.flipY;
                overlay.color=new Color(1,.62f,.28f,.25f);
                overlay.sortingLayerID=body.sortingLayerID;overlay.sortingOrder=body.sortingOrder+1;
            }
            visual.enabled=burning&&art!=null&&art.burnVfx.Length>0;
            if(!visual.enabled)return;
            visual.sprite=art.burnVfx[(int)(Time.time-started)%art.burnVfx.Length];
            visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder+2;
            visual.transform.position=body.bounds.center;
            Vector3 size=body.bounds.size,scale=transform.lossyScale;
            visual.transform.localScale=new Vector3(size.x/visual.sprite.bounds.size.x/Mathf.Max(.001f,Mathf.Abs(scale.x)),size.y/visual.sprite.bounds.size.y/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
        }
        void OnDisable(){if(visual!=null)visual.enabled=false;if(overlay!=null)overlay.enabled=false;started=Time.time;}
        void OnDestroy(){if(tintMaterial!=null)Destroy(tintMaterial);if(overlay!=null)Destroy(overlay.gameObject);if(visual!=null)Destroy(visual.gameObject);}
    }
}

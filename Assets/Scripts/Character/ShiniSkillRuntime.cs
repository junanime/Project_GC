using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Samples resolved positions, never the intended dash destination.
    [DefaultExecutionOrder(500)]
    public sealed class ShiniSkillRuntime : MonoBehaviour
    {
        sealed class Stroke
        {
            public readonly List<Vector3> points=new List<Vector3>();
            public readonly List<float> times=new List<float>();
            public MeshRenderer renderer; public Mesh mesh;
        }
        Character owner; CharacterSkillRuntime skill; SpriteRenderer body;
        SyringeDartAbility needle;
        readonly List<Stroke> strokes=new List<Stroke>();
        readonly Dictionary<Component,float> nextHit=new Dictionary<Component,float>();
        readonly List<Component> stale=new List<Component>();
        readonly Dictionary<Sprite,Sprite> forward=new Dictionary<Sprite,Sprite>(), reverse=new Dictionary<Sprite,Sprite>();
        Stroke current; Vector3 previous; bool emitting, wasDashing; float nextScan;
        Material material;
        public int SegmentCount { get { int n=0;foreach(var s in strokes)n+=Mathf.Max(0,s.points.Count-1);return n; } }
        public void Bind(Character character,CharacterSkillRuntime runtime)
        {
            owner=character;skill=runtime;previous=transform.position;
            body=GetComponentInChildren<SpriteAnimator>()?.GetComponent<SpriteRenderer>();
            if(!skill.IsShini)return;
            Map(owner.Blueprint.idleSpriteSequence,skill.Definition.burningIdle);
            Map(owner.Blueprint.walkSpriteSequence,skill.Definition.burningWalk);
            Map(owner.Blueprint.dashSpriteSequence,skill.Definition.burningDash);
            material=new Material(Shader.Find("Vampire/ShiniFireTrail"));

        }
        void Map(Sprite[] from,Sprite[] to)
        { if(from==null||to==null)return;for(int i=0;i<Mathf.Min(from.Length,to.Length);i++){forward[from[i]]=to[i];reverse[to[i]]=from[i];} }
        void LateUpdate()
        {
            if(skill==null||!skill.IsShini)return;
            if(!owner.IsAlive){Clear();return;}
            if(body!=null && body.sprite!=null)
            {
                Sprite original=reverse.TryGetValue(body.sprite,out var normal)?normal:body.sprite;
                if(skill.Active && forward.TryGetValue(original,out var burning))body.sprite=burning;
                else if(!skill.Active)body.sprite=original;
            }
            Vector3 position=transform.position;
            bool emit=owner.IsDashing||wasDashing||skill.Active;
            // A pause must neither age the trail nor consume its movement sample.
            if(Time.deltaTime<=0)return;
            Sample(previous,position,emit,Time.time);previous=position;wasDashing=owner.IsDashing;
            Expire(Time.time);
            var frames=skill.Definition.fireTrail;
            if(material!=null && frames!=null && frames.Length>0)
                material.mainTexture=frames[(int)(Time.time*8)%frames.Length].texture;
            if(needle==null)needle=FindObjectOfType<SyringeDartAbility>();
            if(needle!=null && Time.time>=nextScan){nextScan=Time.time+.05f;DamageTrail();}
        }
        public void Sample(Vector3 from,Vector3 to,bool emit,float now)
        {
            if(!emit){current=null;emitting=false;return;}
            if((to-from).sqrMagnitude<.000001f)return;
            if(!emitting||current==null)
            {
                current=new Stroke();strokes.Add(current);
                var go=new GameObject("Shini fire path",typeof(MeshFilter),typeof(MeshRenderer));
                current.renderer=go.GetComponent<MeshRenderer>();current.renderer.sharedMaterial=material;
                current.mesh=new Mesh();current.mesh.MarkDynamic();go.GetComponent<MeshFilter>().sharedMesh=current.mesh;
                if(body!=null){current.renderer.sortingLayerID=body.sortingLayerID;current.renderer.sortingOrder=body.sortingOrder-1;}
                current.points.Add(from);current.times.Add(now);
            }
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.15f));
            for(int i=1;i<=steps;i++){current.points.Add(Vector3.Lerp(from,to,(float)i/steps));current.times.Add(now);}
            emitting=true;Refresh(current);
        }
        Vector3 FootOffset => body!=null ? body.transform.position-transform.position-Vector3.up*(.27f*Mathf.Abs(body.transform.lossyScale.y)) : Vector3.down*.27f;
        void Refresh(Stroke s)
        {
            // Billboard ribbons always rise in world +Y, even when moving left or vertically.
            // The original sprite pivot is exactly .27 world units above the feet.
            Vector3 footOffset=FootOffset;
            int segments=s.points.Count-1;
            var vertices=new Vector3[segments*4];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
            float length=0;for(int i=1;i<s.points.Count;i++)length+=Vector3.Distance(s.points[i-1],s.points[i]);
            float walked=0;
            for(int i=0;i<segments;i++)
            {
                Vector3 a=s.points[i]+footOffset,b=s.points[i+1]+footOffset;
                float distance=Vector3.Distance(a,b),u0=walked/Mathf.Max(.01f,length),u1=(walked+distance)/Mathf.Max(.01f,length);walked+=distance;
                if(a.x>b.x){var swap=a;a=b;b=swap;float u=u0;u0=u1;u1=u;}
                float padding=Mathf.Abs(b.x-a.x)<Mathf.Abs(b.y-a.y)*.3f?.12f:0;
                a.x-=padding;b.x+=padding;
                // Exported strip has a small transparent bottom margin; sink only that margin.
                a.y-=.04f;b.y-=.04f;
                int v=i*4,t=i*6;vertices[v]=a;vertices[v+1]=b;vertices[v+2]=a+Vector3.up*.6f;vertices[v+3]=b+Vector3.up*.6f;
                uv[v]=new Vector2(u0,0);uv[v+1]=new Vector2(u1,0);uv[v+2]=new Vector2(u0,1);uv[v+3]=new Vector2(u1,1);
                triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
            }
            s.mesh.Clear();s.mesh.vertices=vertices;s.mesh.uv=uv;s.mesh.triangles=triangles;s.mesh.RecalculateBounds();
        }
        void Remove(Stroke s){if(s.renderer!=null)Destroy(s.renderer.gameObject);if(s.mesh!=null)Destroy(s.mesh);}
        public void Expire(float now)
        {
            for(int i=strokes.Count-1;i>=0;i--)
            {
                var s=strokes[i];
                // Segment lifetime belongs to its endpoint's creation time.
                while(s.points.Count>1 && now-s.times[1]>=3){s.points.RemoveAt(0);s.times.RemoveAt(0);}
                if(s.points.Count<2){if(s==current){current=null;emitting=false;}Remove(s);strokes.RemoveAt(i);}
                else Refresh(s);
            }
        }
        void DamageTrail()
        {
            stale.Clear();foreach(var pair in nextHit)if(pair.Key==null||pair.Value<=Time.time)stale.Add(pair.Key);
            foreach(var key in stale)nextHit.Remove(key);
            foreach(var s in strokes)for(int i=1;i<s.points.Count;i++)
            {
                Vector2 a=s.points[i-1]+FootOffset,b=s.points[i]+FootOffset,delta=b-a;
                foreach(var c in Physics2D.OverlapCapsuleAll((a+b)*.5f,new Vector2(delta.magnitude+.24f,.24f),CapsuleDirection2D.Horizontal,Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg,needle.SkillTargetLayer))
                {
                    if(!SyringeSpecialHitEffectUtility.TryGetValidDamageableTarget(c,owner,out _,out var target,out _) || nextHit.ContainsKey(target))continue;
                    if(target is Monster m && m.IsFieldRuntimeSuspended)continue;
                    nextHit[target]=Time.time+needle.GetEffectiveCooldown();
                    Hit(target);
                }
            }
        }
        public void Hit(Component target)
        {
            if(needle==null)needle=FindObjectOfType<SyringeDartAbility>();
            if(needle==null||Ver4HitEffects.Health(target)<=0)return;
            float damage=needle.GetEffectiveDamage();
            var stats=PlayerGeneralStatRuntime.GetOrCreate(owner);
            if(stats!=null)damage=stats.CalculateOffensiveDamage(owner,target,damage,out _);
            var corrosion=target.GetComponent<CorrosionStatus>();if(corrosion!=null)damage*=corrosion.GetDamageTakenMultiplier();
            var runtime=needle.GetCurrentSpecialRuntime();runtime.ver4HitDamage=damage;
            Ver4HitEffects.Damage(target,damage,Vector2.zero,owner,"불꽃길");
            if(Ver4HitEffects.Health(target)>0)
                (target.GetComponent<Ver4NeedleStatus>()??target.gameObject.AddComponent<Ver4NeedleStatus>()).ApplyFire(runtime,owner);
        }
        void Clear(){foreach(var s in strokes)Remove(s);strokes.Clear();nextHit.Clear();current=null;emitting=false;}
        void OnDisable(){Clear();}
        void OnDestroy(){if(material!=null)Destroy(material);}
    }
    public sealed class ShiniBurnVisual : MonoBehaviour
    {
        SpriteRenderer visual,body;Ver4NeedleStatus status;CharacterSkillDefinition art;float started;
        void Awake()
        {
            status=GetComponent<Ver4NeedleStatus>();body=GetComponentInChildren<SpriteRenderer>();
            art=Resources.Load<CharacterSkillDefinition>("ShiniSkills");started=Time.time;
            visual=new GameObject("Body burn flames").AddComponent<SpriteRenderer>();visual.transform.SetParent(transform,false);
        }
        void LateUpdate()
        {
            if(visual==null)return;
            visual.enabled=status!=null&&status.BurnStacks>0&&body!=null&&body.enabled&&art!=null&&art.burnVfx.Length>0;
            if(!visual.enabled)return;
            visual.sprite=art.burnVfx[(int)(Time.time-started)%art.burnVfx.Length];
            visual.sortingLayerID=body.sortingLayerID;visual.sortingOrder=body.sortingOrder+1;
            visual.transform.position=body.bounds.center;
            Vector3 size=body.bounds.size,scale=transform.lossyScale;
            visual.transform.localScale=new Vector3(size.x/visual.sprite.bounds.size.x/Mathf.Max(.001f,Mathf.Abs(scale.x)),size.y/visual.sprite.bounds.size.y/Mathf.Max(.001f,Mathf.Abs(scale.y)),1);
        }
        void OnDisable(){if(visual!=null)visual.enabled=false;started=Time.time;}
    }
}

using UnityEngine;
namespace Vampire
{
    // A radial grid gives juice a moving front, a narrow mouth and a continuous draining tail.
    public sealed class FoodBossFlowFan:MonoBehaviour
    {
        FoodBossRuntime boss;Vector2 origin,direction;float length,angle,duration,damage,age,next;
        Mesh mesh;Material material;FoodBossFlowRibbon crest;
        const int Sides=32,Rows=18;
        readonly Vector3[] vertices=new Vector3[(Sides+1)*(Rows+1)];
        readonly Vector2[] uv=new Vector2[(Sides+1)*(Rows+1)];
        readonly Color[] colors=new Color[(Sides+1)*(Rows+1)];
        public float Reach {get;private set;}
        public void Init(FoodBossRuntime b,Vector2 o,Vector2 d,float l,float a,float seconds,float hit)
        {
            boss=b;origin=o;direction=d.normalized;length=l;angle=a;duration=seconds;damage=hit;
            mesh=new Mesh();mesh.MarkDynamic();gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=gameObject.AddComponent<MeshRenderer>();material=new Material(Resources.Load<Shader>("FoodBosses/SoftFlow"));material.SetFloat("_Mode",1);r.sharedMaterial=material;r.sortingOrder=490;
            var triangles=new int[Sides*Rows*6];int n=0;
            for(int row=0;row<Rows;row++)for(int side=0;side<Sides;side++)
            {int x=row*(Sides+1)+side,y=x+Sides+1;triangles[n++]=x;triangles[n++]=y;triangles[n++]=x+1;triangles[n++]=x+1;triangles[n++]=y;triangles[n++]=y+1;}
            mesh.vertices=vertices;mesh.triangles=triangles;
            crest=FoodBossFlowRibbon.Create(transform,new Color(1,1,.68f,.88f),491,1);
        }
        float Front(float side)
        {return Reach*(.97f+.025f*Mathf.Sin(side*21-age*11)+.005f*Mathf.Sin(side*39+age*7));}
        void Update()
        {
            if(boss==null||boss.Dead){Destroy(gameObject);return;}if(FoodBossRuntime.Paused)return;age+=Time.deltaTime;
            if(age>duration+.26f){Destroy(gameObject);return;}
            Reach=length*Mathf.SmoothStep(0,1,age/.38f);float fade=Mathf.Clamp01((duration+.26f-age)/.26f);
            float drain=Mathf.SmoothStep(0,1,(age-duration)/.26f),startAngle=Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg-angle*.5f;
            var edge=new Vector2[Sides+1];
            for(int side=0;side<=Sides;side++)
            {
                float s=side/(float)Sides;Vector2 ray=FoodBossRuntime.Dir(startAngle+angle*s);
                float outer=Front(s);edge[side]=origin+ray*outer;
                for(int row=0;row<=Rows;row++)
                {
                    float u=Mathf.Lerp(drain,1,row/(float)Rows),d=u*outer;int index=row*(Sides+1)+side;
                    vertices[index]=transform.InverseTransformPoint(origin+ray*d);
                    uv[index]=new Vector2(d,s);colors[index]=new Color(1,.87f,.20f,fade*.88f*Mathf.Lerp(.78f,1,u));
                }
            }
            material.SetFloat("_Clock",age);mesh.vertices=vertices;mesh.uv=uv;mesh.colors=colors;mesh.RecalculateBounds();
            crest.Set(edge,.10f,age,fade*Mathf.Clamp01(age/.1f));
            var p=boss.Player;if(p==null||!p.IsAlive||age>duration||Time.time<next)return;
            Vector2 delta=(Vector2)p.transform.position-origin;float signed=Vector2.SignedAngle(direction,delta);
            float max=Front(Mathf.Clamp01(signed/angle+.5f));
            if(delta.magnitude<=max+.15f&&Mathf.Abs(signed)<=angle*.5f){p.TakeDamage(damage);next=Time.time+.7f;}
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }

    // The filled impact and its travelling pressure crests share the telegraphed sector.
    public sealed class FoodBossImpactWave:MonoBehaviour
    {
        FoodBossRuntime boss;Vector2 center;float radius,start,sweep,age;LineRenderer fill;
        FoodBossFlowRibbon[] rings;
        public static void Create(FoodBossRuntime b,Vector2 center,float radius,float start=0,float sweep=360)
        {
            var p=new GameObject("Ground pressure impact").AddComponent<FoodBossImpactWave>();p.transform.SetParent(b.Effects,false);
            p.boss=b;p.center=center;p.radius=radius;p.start=start;p.sweep=sweep;
            // Keep a restrained transparent full area underneath the expanding crest.
            p.fill=FoodBossEffects.Ring(b,center,radius,start,sweep);p.fill.transform.SetParent(p.transform,true);
            p.fill.sortingOrder=484;
            p.rings=new FoodBossFlowRibbon[2];
            Color c=b.Kind==FoodBossKind.Rolpangi?new Color(1,.91f,.71f):b.Kind==FoodBossKind.Maribi?new Color(1,.92f,.40f):b.Kind==FoodBossKind.Hurumi?new Color(1,.91f,.73f):b.Kind==FoodBossKind.Tangdengi?new Color(.76f,1,.52f):new Color(1,.74f,.36f);
            for(int i=0;i<2;i++)p.rings[i]=FoodBossFlowRibbon.Create(p.transform,c,487+i,1);
        }
        void Update()
        {
            if(boss==null||boss.Dead){Destroy(gameObject);return;}if(FoodBossRuntime.Paused)return;
            age+=Time.deltaTime;if(age>.55f){Destroy(gameObject);return;}
            float fade=1-age/.55f;
            var col=fill.startColor;col.a=fade*.22f;fill.startColor=fill.endColor=col;
            foreach(var f in fill.GetComponentsInChildren<FoodBossHazardFill>())f.Fade(fade*.40f);
            for(int i=0;i<2;i++)
            {
                float u=Mathf.Clamp01((age-i*.09f)/.38f),r=radius*Mathf.SmoothStep(0,1,u);
                rings[i].Set(FoodBossEffects.Arc(center,r,start,sweep),Mathf.Lerp(.24f,.06f,u),age,fade*(age>i*.09f?1:0));
            }
        }
    }
}

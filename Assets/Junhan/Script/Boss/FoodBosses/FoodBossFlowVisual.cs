using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    public sealed class FoodBossSurfaceFlow:MonoBehaviour
    {
        FoodBossFlowRibbon ribbon;Vector2[] points;float width,duration,age;
        public void Init(FoodBossFlowRibbon r,Vector2[] p,float w,float seconds){ribbon=r;points=p;width=w;duration=seconds;}
        void Update(){if(FoodBossRuntime.Paused)return;age+=Time.deltaTime;ribbon.Set(points,width,age,Mathf.Clamp01((duration-age)/.35f));}
    }
    // Arc-length UVs, a shaded cross section and round end caps form one unbroken tube.
    public sealed class FoodBossFlowRibbon:MonoBehaviour
    {
        Mesh mesh;Material material;MeshRenderer rendererComponent;Color tint;
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        const int Bands=8;
        public static FoodBossFlowRibbon Create(Transform parent,Color color,int order,int mode=0)
        {
            var r=new GameObject("Continuous shaped ribbon").AddComponent<FoodBossFlowRibbon>();
            r.transform.SetParent(parent,false);r.tint=color;r.mesh=new Mesh();r.mesh.MarkDynamic();
            r.gameObject.AddComponent<MeshFilter>().sharedMesh=r.mesh;
            r.rendererComponent=r.gameObject.AddComponent<MeshRenderer>();
            r.material=new Material(Resources.Load<Shader>("FoodBosses/SoftFlow"));r.material.SetFloat("_Mode",mode);
            r.rendererComponent.sharedMaterial=r.material;r.rendererComponent.sortingOrder=order;
            return r;
        }
        public void Set(IList<Vector2> points,float width,float clock,float alpha=1,bool swell=true,bool taper=false,bool advancing=false)
        {
            rendererComponent.enabled=points!=null&&points.Count>=2&&alpha>.005f;
            if(!rendererComponent.enabled)return;
            material.SetFloat("_Clock",clock);vertices.Clear();uv.Clear();colors.Clear();triangles.Clear();
            float distance=0;Vector2 firstSide=Vector2.up,lastSide=Vector2.up;float firstRadius=width*.5f,lastRadius=firstRadius;
            for(int i=0;i<points.Count;i++)
            {
                if(i>0)distance+=Vector2.Distance(points[i-1],points[i]);
                Vector2 tangent=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];
                if(tangent.sqrMagnitude<.00001f)tangent=Vector2.right;
                Vector2 side=new Vector2(-tangent.y,tangent.x).normalized;
                float bulge=swell?1+.11f*Mathf.Sin(distance*4.2f-clock*9)+.025f*Mathf.Sin(distance*7.1f-clock*6):1;
                float radius=width*.5f*bulge*(taper?Mathf.Lerp(1,.38f,Mathf.Pow(i/(float)(points.Count-1),3)):1);
                if(advancing)radius*=1+.30f*Mathf.Exp(-Mathf.Pow((1-i/(float)(points.Count-1))*12,2));
                if(i==0){firstSide=side;firstRadius=radius;}lastSide=side;lastRadius=radius;
                for(int j=0;j<=Bands;j++)Add(points[i]+side*((j/(float)Bands*2-1)*radius),new Vector2(distance,j/(float)Bands),alpha);
                if(i>0)for(int j=0;j<Bands;j++)
                {
                    int a=(i-1)*(Bands+1)+j,b=i*(Bands+1)+j;
                    triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
                }
            }
            Cap(points[0],firstSide,firstRadius,0,alpha,true);
            Cap(points[points.Count-1],lastSide,lastRadius,distance,alpha,false);
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        void Add(Vector2 p,Vector2 texture,float alpha)
        {vertices.Add(transform.InverseTransformPoint(p));uv.Add(texture);var c=tint;c.a*=alpha;colors.Add(c);}
        void Cap(Vector2 p,Vector2 side,float radius,float distance,float alpha,bool start)
        {
            int center=vertices.Count;Add(p,new Vector2(distance,.5f),alpha);
            Vector2 tangent=new Vector2(side.y,-side.x)*(start?-1:1);
            for(int i=0;i<=12;i++)
            {
                float a=Mathf.PI*i/12;Vector2 delta=side*Mathf.Cos(a)*radius+tangent*Mathf.Sin(a)*radius;
                Add(p+delta,new Vector2(distance+(start?-1:1)*Mathf.Sin(a)*radius,.5f+.5f*Mathf.Cos(a)),alpha);
                if(i>0){triangles.Add(center);triangles.Add(center+i);triangles.Add(center+i+1);}
            }
        }
        void OnDestroy(){if(mesh!=null)Destroy(mesh);if(material!=null)Destroy(material);}
    }

    public sealed class FoodBossCreamTrail:MonoBehaviour
    {
        public const float ExtrusionSeconds=1.5f;
        public float Age {get;private set;}
        public float Reveal=>Mathf.Clamp01(Age/ExtrusionSeconds);
        public bool ReachedEnd=>Age>=ExtrusionSeconds;
        FoodBossRuntime boss;Vector2[] path;bool[] gaps;float[] distances;float length;
        readonly List<FoodBossFlowRibbon> ribbons=new List<FoodBossFlowRibbon>();
        readonly List<List<Vector2>> visible=new List<List<Vector2>>();
        FoodBossFlowRibbon nozzle;SnailSlowStatus slow;
        public static FoodBossCreamTrail Create(FoodBossRuntime b,Vector2[] points,bool[] skipped)
        {
            var f=new GameObject("Mouth extruded spiral cream").AddComponent<FoodBossCreamTrail>();f.transform.SetParent(b.Effects,false);
            f.boss=b;f.path=points;f.gaps=skipped;f.distances=new float[points.Length];
            for(int i=1;i<points.Length;i++)f.distances[i]=f.distances[i-1]+Vector2.Distance(points[i-1],points[i]);
            f.length=f.distances[f.distances.Length-1];
            f.nozzle=FoodBossFlowRibbon.Create(f.transform,new Color(1,.93f,.76f),514);
            return f;
        }
        void Update()
        {
            if(boss==null||boss.Dead){Destroy(gameObject);return;}if(FoodBossRuntime.Paused)return;
            Age+=Time.deltaTime;if(Age>=ExtrusionSeconds+6){Destroy(gameObject);return;}
            float reach=length*Reveal,fade=Mathf.Clamp01((ExtrusionSeconds+6-Age)/.45f);
            visible.Clear();List<Vector2> current=null;
            for(int i=1;i<path.Length;i++)
            {
                if(distances[i-1]>reach)break;
                if(gaps[i]||gaps[i-1]){current=null;continue;}
                if(current==null){current=new List<Vector2>();visible.Add(current);current.Add(path[i-1]);}
                float t=Mathf.Clamp01((reach-distances[i-1])/Mathf.Max(.00001f,distances[i]-distances[i-1]));
                current.Add(Vector2.Lerp(path[i-1],path[i],t));
                if(t<1)break;
            }
            while(ribbons.Count<visible.Count)ribbons.Add(FoodBossFlowRibbon.Create(transform,new Color(1,.92f,.74f),486));
            for(int i=0;i<ribbons.Count;i++)ribbons[i].Set(i<visible.Count?visible[i]:null,.55f,Age,fade,true,false,i==visible.Count-1&&!ReachedEnd);
            // A short foreground lip joins the mouth to the ground ribbon, never to the body's center.
            float lipAlpha=Age<ExtrusionSeconds?Mathf.Clamp01(Age/.06f)*Mathf.Clamp01((ExtrusionSeconds-Age)/.15f):0;
            nozzle.Set(new[]{boss.Visual.SnailMouth,path[0],path[Mathf.Min(4,path.Length-1)]},.38f,Age,lipAlpha);
            var player=boss.Player;if(player==null||!player.IsAlive)return;
            bool inside=Contains(player.transform.position);
            if(inside&&fade>.20f)
            {
                slow=player.GetComponent<SnailSlowStatus>()??player.gameObject.AddComponent<SnailSlowStatus>();slow.Enter(GetInstanceID(),.3f);
                var rb=player.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=FiniteStomachMap.ClampDash(rb.position,rb.position+((Vector2)boss.transform.position-rb.position).normalized*.38f*Time.deltaTime,.2f);
            }
            else if(slow!=null)slow.Exit(GetInstanceID(),0);
        }
        public bool Contains(Vector2 at)
        {
            foreach(var segment in visible)for(int i=1;i<segment.Count;i++)
                if(SnailBossProjectile.SegmentDistance(at,segment[i-1],segment[i])<.455f)return true;
            return false;
        }
        void OnDestroy(){if(slow!=null)slow.Exit(GetInstanceID(),0);}
    }
}

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
        Mesh mesh;Material material;MeshRenderer rendererComponent;Color tint;int mode;
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<Vector2> sections=new List<Vector2>();
        readonly List<Vector2> normals=new List<Vector2>();
        readonly List<Color> colors=new List<Color>();
        readonly List<int> triangles=new List<int>();
        const int Bands=12,CapSteps=16;
        public static FoodBossFlowRibbon Create(Transform parent,Color color,int order,int mode=0)
        {
            var r=new GameObject("Continuous shaped ribbon").AddComponent<FoodBossFlowRibbon>();
            r.transform.SetParent(parent,false);r.tint=color;r.mode=mode;r.mesh=new Mesh();r.mesh.MarkDynamic();
            r.gameObject.AddComponent<MeshFilter>().sharedMesh=r.mesh;
            r.rendererComponent=r.gameObject.AddComponent<MeshRenderer>();
            // Energy beams keep their existing material; shared Stage2 wind is untouched.
            r.material=new Material(Resources.Load<Shader>(mode==2?"FoodBosses/SoftFlow":"FoodBosses/ViscousFlow"));r.material.SetFloat("_Mode",mode);
            r.rendererComponent.sharedMaterial=r.material;r.rendererComponent.sortingOrder=order;
            return r;
        }
        public void Set(IList<Vector2> points,float width,float clock,float alpha=1,bool swell=true,bool taper=false,bool advancing=false,IList<float> widths=null,bool groundedEnds=false)
        {
            rendererComponent.enabled=points!=null&&points.Count>=2&&alpha>.005f;
            if(!rendererComponent.enabled)return;
            material.SetFloat("_Clock",clock);vertices.Clear();uv.Clear();sections.Clear();normals.Clear();colors.Clear();triangles.Clear();
            float distance=0,total=0;Vector2 firstSide=Vector2.up,lastSide=Vector2.up;float firstRadius=width*.5f,lastRadius=firstRadius;
            if(groundedEnds)for(int i=1;i<points.Count;i++)total+=Vector2.Distance(points[i-1],points[i]);
            for(int i=0;i<points.Count;i++)
            {
                if(i>0)distance+=Vector2.Distance(points[i-1],points[i]);
                Vector2 tangent=points[Mathf.Min(i+1,points.Count-1)]-points[Mathf.Max(0,i-1)];
                if(tangent.sqrMagnitude<.00001f)tangent=Vector2.right;
                Vector2 side=new Vector2(-tangent.y,tangent.x).normalized;
                float bulge=swell?(mode==2?1+.11f*Mathf.Sin(distance*4.2f-clock*9)+.025f*Mathf.Sin(distance*7.1f-clock*6):1+.035f*Mathf.Sin(distance*4.2f-clock*6)+.012f*Mathf.Sin(distance*7.1f-clock*4)):1;
                float radius=width*.5f*bulge*(taper?Mathf.Lerp(1,.38f,Mathf.Pow(i/(float)(points.Count-1),3)):1);
                if(widths!=null&&i<widths.Count)radius*=widths[i];
                if(advancing)radius*=1+.08f*Mathf.Exp(-Mathf.Pow((1-i/(float)(points.Count-1))*12,2));
                // Cream ends come to rest on the floor over the last short section,
                // instead of keeping the lifted, blunt end of a rubber hose.
                float lift=groundedEnds?Mathf.Lerp(.25f,1,Mathf.SmoothStep(0,1,Mathf.Min(distance,total-distance)/.30f)):1;
                if(i==0){firstSide=side;firstRadius=radius;}lastSide=side;lastRadius=radius;
                for(int j=0;j<=Bands;j++)
                {
                    float cross=j/(float)Bands*2-1;
                    Add(points[i]+side*(cross*radius),new Vector2(distance,j/(float)Bands),new Vector2(cross,0),side*cross,radius,alpha,lift);
                }
                if(i>0)for(int j=0;j<Bands;j++)
                {
                    int a=(i-1)*(Bands+1)+j,b=i*(Bands+1)+j;
                    triangles.Add(a);triangles.Add(b);triangles.Add(a+1);triangles.Add(a+1);triangles.Add(b);triangles.Add(b+1);
                }
            }
            Cap(points[0],firstSide,firstRadius,0,alpha,true,groundedEnds?.25f:1);
            Cap(points[points.Count-1],lastSide,lastRadius,distance,alpha,false,groundedEnds?.25f:1);
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetUVs(1,sections);mesh.SetUVs(2,normals);mesh.SetColors(colors);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();
        }
        void Add(Vector2 p,Vector2 texture,Vector2 section,Vector2 normal,float radius,float alpha,float lift=1)
        {
            // A small raised centre catches light like a piped mound. It remains inside
            // the original telegraph/collision margin; beam geometry is unchanged.
            float height=mode==0?.23f:mode==1?.10f:0;
            p+=Vector2.up*(radius*height*lift*Mathf.Sqrt(Mathf.Max(0,1-section.sqrMagnitude)));
            vertices.Add(transform.InverseTransformPoint(p));uv.Add(texture);sections.Add(section);normals.Add(normal);
            var c=tint;c.a*=alpha;colors.Add(c);
        }
        void Cap(Vector2 p,Vector2 side,float radius,float distance,float alpha,bool start,float lift)
        {
            int center=vertices.Count;Add(p,new Vector2(distance,.5f),Vector2.zero,Vector2.zero,radius,alpha,lift);
            Vector2 tangent=new Vector2(side.y,-side.x)*(start?-1:1);
            // Radial rows match the strip's cross-section samples at the seam.
            // A single fan would leave a hairline hole below the raised strip centre.
            for(int row=1;row<=Bands/2;row++)for(int i=0;i<=CapSteps;i++)
            {
                float a=Mathf.PI*i/CapSteps;Vector2 section=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(row/(Bands*.5f));Vector2 normal=side*section.x+tangent*section.y;
                Add(p+normal*radius,new Vector2(distance+(start?-1:1)*section.y*radius,.5f+.5f*section.x),section,normal,radius,alpha,lift);
                if(i==0)continue;
                int b=center+1+(row-1)*(CapSteps+1)+i;
                if(row==1){triangles.Add(center);triangles.Add(b-1);triangles.Add(b);}
                else{int a0=b-(CapSteps+1);triangles.Add(a0-1);triangles.Add(b-1);triangles.Add(a0);triangles.Add(a0);triangles.Add(b-1);triangles.Add(b);}
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
        readonly List<List<float>> widths=new List<List<float>>();
        readonly Vector2[] lip=new Vector2[17];
        readonly float[] lipWidths=new float[17];
        int activeSegments;
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
            activeSegments=0;foreach(var segment in visible)segment.Clear();foreach(var profile in widths)profile.Clear();
            List<Vector2> current=null;List<float> currentWidths=null;
            for(int i=1;i<path.Length;i++)
            {
                if(distances[i-1]>reach)break;
                if(gaps[i]||gaps[i-1]){current=null;continue;}
                if(current==null)
                {
                    if(activeSegments==visible.Count){visible.Add(new List<Vector2>());widths.Add(new List<float>());}
                    current=visible[activeSegments];currentWidths=widths[activeSegments++];current.Add(path[i-1]);currentWidths.Add(DepositedWidth(distances[i-1]));
                }
                float t=Mathf.Clamp01((reach-distances[i-1])/Mathf.Max(.00001f,distances[i]-distances[i-1]));
                current.Add(Vector2.Lerp(path[i-1],path[i],t));
                currentWidths.Add(DepositedWidth(Mathf.Lerp(distances[i-1],distances[i],t)));
                if(t<1)break;
            }
            while(ribbons.Count<activeSegments)ribbons.Add(FoodBossFlowRibbon.Create(transform,new Color(1,.92f,.74f),486));
            // Deposited cream settles locally, instead of breathing along the entire path forever.
            float flowClock=Mathf.Min(Age,ExtrusionSeconds+.35f);
            for(int i=0;i<ribbons.Count;i++)ribbons[i].Set(i<activeSegments?visible[i]:null,.55f,flowClock,fade,false,false,i==activeSegments-1&&!ReachedEnd,i<activeSegments?widths[i]:null,true);
            // A short foreground lip joins the mouth to the ground ribbon, never to the body's center.
            float lipAlpha=Age<ExtrusionSeconds?Mathf.Clamp01(Age/.06f)*Mathf.Clamp01((ExtrusionSeconds-Age)/.15f):0;
            Vector2 mouth=boss.Visual.SnailMouth,end=PointAtDistance(Mathf.Min(reach,.85f));
            Vector2 axis=end-mouth;if(axis.sqrMagnitude<.00001f)axis=Vector2.right*boss.Visual.Facing;else axis.Normalize();
            Vector2 c1=mouth+axis*.22f+Vector2.up*.09f,c2=end-axis*.18f+Vector2.up*.10f;
            for(int i=0;i<lip.Length;i++)
            {
                float u=i/(float)(lip.Length-1),v=1-u;
                lip[i]=v*v*v*mouth+3*v*v*u*c1+3*v*u*u*c2+u*u*u*end;
                lipWidths[i]=Mathf.Lerp(.60f,1,Mathf.SmoothStep(0,1,u));
            }
            nozzle.Set(lip,.55f,Age,lipAlpha,false,false,false,lipWidths);
            var player=boss.Player;if(player==null||!player.IsAlive)return;
            bool inside=Contains(player.transform.position);
            if(inside&&fade>.20f)
            {
                slow=player.GetComponent<SnailSlowStatus>()??player.gameObject.AddComponent<SnailSlowStatus>();slow.Enter(GetInstanceID(),.3f);
                var rb=player.GetComponent<Rigidbody2D>();if(rb!=null)rb.position=FiniteStomachMap.ClampDash(rb.position,rb.position+((Vector2)boss.transform.position-rb.position).normalized*.38f*Time.deltaTime,.2f);
            }
            else if(slow!=null)slow.Exit(GetInstanceID(),0);
        }
        float DepositedWidth(float distance)
        {
            float settled=Mathf.Max(0,Age-distance/Mathf.Max(.001f,length)*ExtrusionSeconds);
            return 1+.08f*Mathf.Sin(Mathf.PI*Mathf.Clamp01(settled/.32f))*Mathf.Exp(-settled*4);
        }
        Vector2 PointAtDistance(float distance)
        {
            for(int i=1;i<path.Length;i++)if(distances[i]>=distance)
                return Vector2.Lerp(path[i-1],path[i],Mathf.InverseLerp(distances[i-1],distances[i],distance));
            return path[path.Length-1];
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

using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    [DefaultExecutionOrder(200)]
    public sealed class FoodBossPhaseVisual:MonoBehaviour
    {
        sealed class Layer
        {
            public Mesh mesh;public Material material;public MeshRenderer renderer;
            public Vector3[] rest,posed;public Vector2[] normalized;
            public bool incoming;public int part;public float width,height,scale;
        }
        readonly List<Layer> layers=new List<Layer>();
        readonly List<LineRenderer> cracks=new List<LineRenderer>();
        readonly List<Vector3[]> crackPoints=new List<Vector3[]>();
        FoodBossRuntime boss;Transform rig;LineRenderer[] glints;float progress;bool active;int cueIndex;
        public float Progress=>progress;
        public int EmittedCues {get;private set;}
        static readonly float[] durations={3.2f,4.8f,4.6f,5.2f,3.8f};
        public static float Duration(FoodBossKind kind)=>durations[(int)kind];
        public void Initialize(FoodBossRuntime owner)
        {
            boss=owner;rig=new GameObject("Phase anatomy rig").transform;rig.SetParent(owner.Visual.transform,false);
            switch(boss.Kind)
            {
                case FoodBossKind.Maribi:Add(false,1);Add(true,0);Add(false,2);break;
                case FoodBossKind.Denikani:
                    Add(false,0);Add(true,10);
                    for(int i=13;i<=16;i++)Add(false,i);
                    Crack(new[]{new Vector2(165,32),new Vector2(150,63),new Vector2(177,84),new Vector2(155,115)});
                    Crack(new[]{new Vector2(177,84),new Vector2(211,78),new Vector2(239,106)});
                    Crack(new[]{new Vector2(150,63),new Vector2(123,84),new Vector2(105,111)});
                    break;
                case FoodBossKind.Tangdengi:
                    Add(false,0);Add(true,20);
                    for(int i=30;i<=35;i++)Add(false,i);
                    Crack(new[]{new Vector2(217,126),new Vector2(212,139),new Vector2(230,148),new Vector2(216,163),new Vector2(246,177)});
                    Crack(new[]{new Vector2(230,148),new Vector2(253,142),new Vector2(268,162)});
                    Crack(new[]{new Vector2(216,163),new Vector2(201,169)});
                    glints=new LineRenderer[3];for(int i=0;i<3;i++){glints[i]=LocalLine("Candy contact glint",.035f,new Color(1,.94f,.65f),518);glints[i].positionCount=2;}
                    break;
                default:Add(false,0);Add(true,0);break;
            }
            rig.gameObject.SetActive(false);
        }
        Layer Add(bool incoming,int part)
        {
            var sprite=FoodBossArt.Walk(boss.Kind,incoming,0);var rect=sprite.rect;
            var l=new Layer{incoming=incoming,part=part,width=rect.width,height=rect.height,scale=FoodBossArt.WalkScale(boss.Kind,incoming)/100};
            var go=new GameObject($"Phase {(incoming?2:1)} anatomy {part}");go.transform.SetParent(rig,false);
            l.mesh=new Mesh{name=go.name};go.AddComponent<MeshFilter>().sharedMesh=l.mesh;
            l.renderer=go.AddComponent<MeshRenderer>();l.renderer.sortingOrder=part==2||part>=13&&!incoming?515:incoming?512:511;
            l.material=new Material(Resources.Load<Shader>("FoodBosses/PhaseArt"));l.material.mainTexture=sprite.texture;l.material.SetFloat("_Part",part);l.material.SetVector("_Size",new Vector4(rect.width,rect.height,0,0));l.renderer.sharedMaterial=l.material;
            const int grid=48;l.rest=new Vector3[(grid+1)*(grid+1)];l.posed=new Vector3[l.rest.Length];l.normalized=new Vector2[l.rest.Length];var uv=new Vector2[l.rest.Length];
            for(int y=0;y<=grid;y++)for(int x=0;x<=grid;x++)
            {
                int n=y*(grid+1)+x;var q=new Vector2(x/(float)grid,y/(float)grid);l.normalized[n]=q;
                l.rest[n]=new Vector3((q.x-.5f)*rect.width,q.y*rect.height-4,0)*l.scale;
                uv[n]=new Vector2((rect.x+q.x*rect.width)/sprite.texture.width,(rect.y+q.y*rect.height)/sprite.texture.height);
            }
            var triangles=new int[grid*grid*6];int t=0;
            for(int y=0;y<grid;y++)for(int x=0;x<grid;x++){int n=y*(grid+1)+x;triangles[t++]=n;triangles[t++]=n+grid+1;triangles[t++]=n+1;triangles[t++]=n+1;triangles[t++]=n+grid+1;triangles[t++]=n+grid+2;}
            l.mesh.vertices=l.rest;l.mesh.uv=uv;l.mesh.uv2=l.normalized;l.mesh.triangles=triangles;l.mesh.MarkDynamic();l.mesh.RecalculateBounds();layers.Add(l);return l;
        }
        static Vector3 Pixel(Layer l,float x,float y)=>new Vector3(x-l.width*.5f,l.height-y-4,0)*l.scale;
        LineRenderer LocalLine(string name,float width,Color color,int order)
        {
            var r=FoodBossEffects.Line(rig,new[]{Vector2.zero,Vector2.zero},width,color,false,order);
            r.name=name;r.useWorldSpace=false;r.transform.localPosition=Vector3.zero;r.transform.localRotation=Quaternion.identity;r.transform.localScale=Vector3.one;return r;
        }
        void Crack(Vector2[] points)
        {
            var r=LocalLine("Opening carapace crack",.055f,new Color(.20f,.055f,.025f),516);var p=new Vector3[points.Length];for(int i=0;i<p.Length;i++)p[i]=Pixel(layers[0],points[i].x,points[i].y);cracks.Add(r);crackPoints.Add(p);
        }
        public void Begin(){progress=0;cueIndex=0;EmittedCues=0;active=true;rig.localScale=new Vector3(-boss.Visual.Facing,1,1);rig.gameObject.SetActive(true);Sample();}
        public void SetProgress(float value)
        {
            progress=Mathf.Clamp01(value);float time=progress*Duration(boss.Kind);var cues=FoodBossPhaseCues.For(boss.Kind);
            while(cueIndex<cues.Length&&time>=cues[cueIndex].time){var cue=cues[cueIndex++];if(boss.Audio.PlayTransition(cue.clip,cue.gain))EmittedCues++;}
        }
        public void End(){active=false;if(rig!=null)rig.gameObject.SetActive(false);if(boss!=null&&boss.Visual!=null)boss.Visual.Body.enabled=true;}
        void LateUpdate(){if(active){boss.Visual.Body.enabled=false;Sample();}}
        static float Ease(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        static Vector3 Turn(Vector3 v,Vector3 pivot,float degrees)=>pivot+Quaternion.Euler(0,0,degrees)*(v-pivot);
        static float Strike(float t,float at)=>Ease(at-.18f,at,t)*(1-Ease(at,at+.22f,t));
        void Sample()
        {
            float t=progress*Duration(boss.Kind),p=progress;
            float breakAt=boss.Kind==FoodBossKind.Tangdengi?1.35f:1.50f;
            for(int k=0;k<cracks.Count;k++)
            {
                var line=cracks[k];var pts=crackPoints[k];float spread=Ease(.25f+k*.14f,breakAt-.10f,t);line.enabled=t<breakAt&&spread>0;line.positionCount=pts.Length;
                for(int j=0;j<pts.Length;j++){float index=spread*(pts.Length-1);int a=Mathf.Min(j,Mathf.FloorToInt(index));int b=Mathf.Min(a+1,pts.Length-1);line.SetPosition(j,j<=index?pts[j]:Vector3.Lerp(pts[a],pts[b],index-a));}
                line.startWidth=line.endWidth=Mathf.Lerp(.015f,.065f,spread);
            }
            float contact=Strike(t,3.20f)+Strike(t,4.15f);
            if(glints!=null)for(int i=0;i<glints.Length;i++)
            {
                var g=glints[i];float age=Mathf.Min(Mathf.Abs(t-3.20f),Mathf.Abs(t-4.15f));g.enabled=age<.08f;
                var center=Pixel(layers[1],44,137);Vector3 dir=new Vector3(Mathf.Cos(i*Mathf.PI/3),Mathf.Sin(i*Mathf.PI/3),0);g.SetPosition(0,center-dir*.16f);g.SetPosition(1,center+dir*.16f);
            }
            foreach(var l in layers)
            {
                float alpha=1;int part=l.part;
                switch(boss.Kind)
                {
                    case FoodBossKind.Rolpangi:
                        l.material.SetFloat("_Radial",Mathf.Lerp(0,.85f,Ease(.65f,2.5f,t)));l.material.SetFloat("_Incoming",l.incoming?1:0);alpha=l.incoming&&t<.65f?0:1;break;
                    case FoodBossKind.Denikani:
                        if(part==0)alpha=t<1.5f?1:0;
                        else if(l.incoming)alpha=t>=1.5f?1:0;
                        else if(part>=17)alpha=t>=1.5f?1-Ease(1.8f,2.4f,t):0;
                        else alpha=t>=1.5f?1-Ease(3.0f,3.6f,t):0;
                        if(part==11||part==12)alpha=t>=2.4f?1:0;
                        break;
                    case FoodBossKind.Maribi:
                        alpha=part==2?1-Ease(1.9f,2.45f,t):l.incoming?(t>=1.0f?1:0):(t<1.0f?1:0);
                        if(l.incoming)l.material.SetFloat("_Embarrassed",1-Ease(2.65f,3.0f,t));break;
                    case FoodBossKind.Tangdengi:
                        alpha=part==0?(t<1.35f?1:0):part>=30?(t>=1.35f?1-Ease(2.0f,2.7f,t):0):(t>=1.35f?1:0);break;
                    case FoodBossKind.Hurumi:alpha=l.incoming?(t>=1.25f?1:0):(t<1.25f?1:0);break;
                }
                l.material.SetFloat("_Opacity",alpha);l.renderer.enabled=alpha>.001f;if(!l.renderer.enabled)continue;
                for(int n=0;n<l.rest.Length;n++)
                {
                    Vector3 v=l.rest[n];var q=l.normalized[n];
                    switch(boss.Kind)
                    {
                        case FoodBossKind.Rolpangi:
                            float press=Ease(0,.55f,t)*(1-Ease(1.8f,2.8f,t));v.y*=1-.075f*press;
                            float bounce=t>2.35f?Mathf.Sin((t-2.35f)*12)*(1-Ease(2.35f,3.2f,t)):0;v.y+=Ease(.56f,.84f,q.y)*bounce*.10f;break;
                        case FoodBossKind.Denikani:
                            if(part>=17){float u=t-1.5f;int strip=part-17;v=Turn(v,Pixel(l,150,135),(strip-1)*u*12);v+=new Vector3((strip-1)*u*.30f,u*.20f,0);}
                            else if(part>=13&&!l.incoming){float u=Mathf.Max(0,t-2.4f);int half=(part-13)%2;int side=(part-13)/2;var pivot=Pixel(l,side==0?46:148,side==0?197:214);v=Turn(v,pivot,(half==0?-1:1)*u*40);v+=new Vector3((half==0?-1:1)*u*.35f,.20f*u-.9f*u*u,0);}
                            else if(part==11||part==12){var pivot=Pixel(l,part==11?94:191,part==11?163:197);float grow=Ease(2.4f,3.5f,t);v=pivot+(v-pivot)*Mathf.Lerp(.70f,1,grow);v+=new Vector3(-Mathf.Sin(grow*Mathf.PI)*.18f,.07f*Mathf.Sin(grow*Mathf.PI),0);}
                                                        else if(l.incoming){
                                float rear=Ease(.45f,.78f,q.y)*Ease(.10f,.35f,q.x);v=Vector3.Lerp(v,Turn(v,new Vector3(.35f,.9f,0),-12*(1-Ease(1.5f,2.35f,t))),rear);
                                var pixel=new Vector2(q.x*l.width,(1-q.y)*l.height);float grow=Ease(2.4f,3.5f,t);
                                for(int side=0;side<2;side++){
                                    var at=side==0?new Vector2(59,201):new Vector2(168,226);var radius=side==0?new Vector2(59,68):new Vector2(50,49);
                                    var delta=pixel-at;float weight=1-Ease(.55f,1.30f,new Vector2(delta.x/radius.x,delta.y/radius.y).magnitude);
                                    var pivot=Pixel(l,side==0?94:191,side==0?163:197);v=Vector3.Lerp(v,pivot+(v-pivot)*Mathf.Lerp(.78f,1,grow),weight);
                                }
                            }
                            else v.x+=Mathf.Sin(t*24)*.012f*Ease(.4f,1.3f,t);
                            break;
                        case FoodBossKind.Maribi:
                            if(part==2){float u=Ease(.65f,2.1f,t);float shake=t<.65f?Mathf.Sin(t*24)*2*Ease(0,.5f,t):0;var pivot=Pixel(l,139,148);v=Turn(v,pivot,shake-95*u);v+=new Vector3(-2.8f*u,.8f*Mathf.Sin(u*Mathf.PI)-1.3f*u,0);}
                            else{float shy=Ease(1.1f,1.5f,t)*(1-Ease(2.65f,3.1f,t));v.y*=1-.055f*shy;v=Turn(v,new Vector3(0,.3f,0),-3*shy);v.y+=Mathf.Sin(Ease(3.05f,3.7f,t)*Mathf.PI)*.23f;}
                            break;
                        case FoodBossKind.Tangdengi:
                            if(part>=30){float u=t-1.35f;int k=part-30;var pivot=Pixel(l,210+(k%3)*30,k<3?130:164);v=Turn(v,pivot,(k%2==0?-1:1)*u*(70+k*9));v+=new Vector3((k%3-1)*u*(.8f+k*.12f),(.6f+(k%2)*.8f)*u-1.3f*u*u,0);}
                            else if(l.incoming){float rise=Ease(1.35f,2.65f,t);float compression=Mathf.Lerp(.84f,1,rise);v.y*=compression;
                                float px=q.x*l.width,py=(1-q.y)*l.height,edge=137;
                                bool upper=py<=edge&&px<=190,lower=py>edge&&px<=178&&py<=197;
                                if(upper||lower){var pivot=Pixel(l,upper?181:169,upper?144:183);pivot.y*=compression;
                                    float joint=1-Ease(upper?125:110,upper?190:178,px);if(lower)joint*=1-Ease(183,197,py);
                                    v=Vector3.Lerp(v,Turn(v,pivot,(upper?5:-4)*contact),joint);
                                }
                                v.y-=Mathf.Sin(Ease(1.35f,2.8f,t)*Mathf.PI)*.09f;
                            }else{v.y*=1-.045f*Ease(.1f,1.2f,t);v.x+=Mathf.Sin(t*35)*.013f*Ease(.4f,1.25f,t);}
                            break;
                        case FoodBossKind.Hurumi:
                            float noodle=Ease(.58f,.74f,q.y),delay=Mathf.Abs(q.x-.5f)*.35f;
                            float unfold=Ease(1.25f+delay,2.85f+delay,t);float fold=l.incoming?1-unfold:Mathf.Sin(Ease(0,1.5f,t)*Mathf.PI)*.2f;
                            v.y-=Mathf.Max(0,v.y-2.60f)*fold*.65f;v.x+=(.5f-q.x)*fold*noodle;
                            v.x+=Mathf.Sin((t-2.4f)*10+q.x*6)*.06f*Ease(2.3f,2.7f,t)*(1-Ease(3.1f,3.8f,t))*noodle;
                            v.y-=Mathf.Sin(Ease(0,3.2f,t)*Mathf.PI)*.14f;break;
                    }
                    l.posed[n]=v;
                }
                l.mesh.vertices=l.posed;l.mesh.RecalculateBounds();
            }
        }
        void OnDestroy(){foreach(var l in layers){Destroy(l.mesh);Destroy(l.material);}if(rig!=null)Destroy(rig.gameObject);}
    }
    public static class FoodBossPhaseCues
    {
        public struct Cue{public float time,gain;public string clip;public Cue(float time,string clip,float gain=1){this.time=time;this.clip=clip;this.gain=gain;}}
        static readonly Cue[][] all={
            new[]{new Cue(.15f,"CreamPress"),new Cue(.75f,"CreamSpread"),new Cue(2.5f,"ToppingSettle")},
            new[]{new Cue(.35f,"Crack",.75f),new Cue(1.05f,"Crack"),new Cue(1.5f,"ShellOpen"),new Cue(2.4f,"Molt"),new Cue(3.65f,"ClawSettle")},
            new[]{new Cue(.15f,"LidRattle"),new Cue(.65f,"LidPop"),new Cue(1.45f,"Shy"),new Cue(3.05f,"Angry")},
            new[]{new Cue(.3f,"Stress",.7f),new Cue(.95f,"Crack"),new Cue(1.35f,"Shatter"),new Cue(3.2f,"CandyClash"),new Cue(4.15f,"CandyClash")},
            new[]{new Cue(.2f,"Dip"),new Cue(1.25f,"Unfurl"),new Cue(2.1f,"Unfurl",.75f),new Cue(3.15f,"NoodleSettle")}
        };
        public static Cue[] For(FoodBossKind kind)=>all[(int)kind];
    }
}

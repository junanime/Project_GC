using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Dedicated, independently animated terrain pieces. No actor or shared materials are edited.
    [DefaultExecutionOrder(1200)]
    public sealed class OralMapVisual : MonoBehaviour
    {
        OralStageRuntime runtime; FiniteStomachMap map; Transform root;
        Material surface, spriteMaterial, tongueMaterial, fluidMaterial, backdrop;
        Mesh tongue; Vector3[] tongueBase, tongueVertices; MeshRenderer fluid;
        Sprite[] toothArt, foodArt; SpriteRenderer[] lower, upper, food, shards;
        Vector2[] toothPositions; LineRenderer[] warnings, bubbleRings, wind, jumpArcs;
        readonly List<Mesh> meshes=new List<Mesh>(); readonly List<Sprite> sprites=new List<Sprite>();
        readonly List<LineRenderer> cavityProgress=new List<LineRenderer>();
        readonly List<SpriteRenderer> cavities=new List<SpriteRenderer>();
        LineRenderer tongueOutline, tongueHighlight; GameObject hud; Text title, hint;
        LineRenderer foodWarning; MeshRenderer[] bubbleDiscs;Material bubbleMaterial; Sprite[] fragments;
        float clock; bool configured; Vector2[] cavityCrown;
        LineRenderer lowerGum,upperGum,upperGumEdge;
        Material gumMaterial,southWallMaterial;
        public bool Ready=>configured;
        public Transform VisualRoot=>root;
        public void Configure(OralStageRuntime owner)
        {
            if(configured)return;runtime=owner;map=owner.Map;
            root=new GameObject("구강 · 독립 지형과 움직임").transform;root.SetParent(transform,false);
            var shader=Resources.Load<Shader>("OralMap/Surface");
            surface=new Material(shader);surface.SetFloat("_Mode",0);
            surface.mainTexture=Resources.Load<Texture2D>("OralMap/Tissue");
            backdrop=new Material(shader);backdrop.SetFloat("_Mode",4);backdrop.SetColor("_Color",new Color(.115f,.023f,.065f,1));
            var backdropRect=map.WorldRect;backdropRect.xMin-=32;backdropRect.xMax+=32;backdropRect.yMin-=32;backdropRect.yMax+=32;
            Quad("입안 바깥의 어둠",backdropRect,backdrop,-930);
            spriteMaterial=new Material(shader);spriteMaterial.SetFloat("_Mode",1);
            tongueMaterial=new Material(spriteMaterial);tongueMaterial.mainTexture=Resources.Load<Texture2D>("OralMap/Tongue");
            tongueMaterial.SetTexture("_SecondTex",Resources.Load<Texture2D>("OralMap/Palate"));
            tongueMaterial.SetTexture("_DetailTex",Resources.Load<Texture2D>("OralMap/TongueDetail"));
            tongueMaterial.SetFloat("_SolidFloor",1);
            fluidMaterial=new Material(shader);fluidMaterial.SetFloat("_Mode",2);
            Quad("깊은 협곡과 잇몸",map.WorldRect,surface,-920);
            var wallMaterial=new Material(shader);wallMaterial.SetFloat("_Mode",6);
            wallMaterial.mainTexture=Resources.Load<Texture2D>("ColonMap/WallTissue");
            southWallMaterial=wallMaterial;
            Quad("혀 뿌리 남쪽 외곽 벽",new Rect(map.WorldRect.xMin,map.WorldRect.yMin,map.WorldRect.width,OralMapGeometry.SouthWallWidth),wallMaterial,-750);
            var wallEdge=Line("남쪽 벽 앞 경계",.22f,new Color(.36f,.08f,.16f),-749,true);
            wallEdge.enabled=true;wallEdge.positionCount=2;
            wallEdge.SetPosition(0,new Vector2(map.WorldRect.xMin,OralMapGeometry.SouthFloor(map)));
            wallEdge.SetPosition(1,new Vector2(map.WorldRect.xMax,OralMapGeometry.SouthFloor(map)));
            fluidMaterial.SetFloat("_SouthFloor",OralMapGeometry.SouthWallWidth/map.WorldRect.height);
            BuildTongue();
            toothArt=Atlas("OralMap/Teeth");foodArt=Atlas("OralMap/Food");
            BuildCrown(); toothPositions=OralMapGeometry.ToothCenters(map);int n=toothPositions.Length;
            lowerGum=Line("아랫잇몸 조직",19,new Color(.65f,.19f,.28f),-860,true);
            upperGumEdge=Line("윗잇몸 윤곽",20,new Color(.30f,.055f,.12f),-836,true);
            upperGum=Line("윗잇몸 조직",18.8f,new Color(.80f,.29f,.38f),-835,true);
            gumMaterial=new Material(shader);gumMaterial.SetFloat("_Mode",5);gumMaterial.mainTexture=surface.mainTexture;
            foreach(var gum in new[]{lowerGum,upperGum}){gum.sharedMaterial=gumMaterial;gum.numCapVertices=12;gum.textureMode=LineTextureMode.Stretch;}
            lower=new SpriteRenderer[n];upper=new SpriteRenderer[n];warnings=new LineRenderer[n];
            for(int i=0;i<n;i++)
            {
                int type=0;
                lower[i]=Sprite("아랫니 "+i,toothArt[type],-850);Set(lower[i],toothPositions[i],OralMapGeometry.ToothScale);
                upper[i]=Sprite("윗니 "+i,toothArt[type],-830);Set(upper[i],toothPositions[i]+Vector2.up*9f,OralMapGeometry.ToothScale);
                upper[i].color=new Color(.91f,.79f,.82f,.30f);
                warnings[i]=Line("압착 예고 "+i,.13f,new Color(1,.65f,.2f),16);warnings[i].enabled=false;
            }
            fluid=Quad("흐르는 음료",map.WorldRect,fluidMaterial,-740);fluid.enabled=false;
            food=new SpriteRenderer[4];shards=new SpriteRenderer[12];
            for(int i=0;i<food.Length;i++){food[i]=Sprite("낙하 음식 "+i,foodArt[i],12);food[i].enabled=false;GroundVisualSorting.Apply(food[i],12);}
            fragments=new Sprite[4];var foodTexture=foodArt[0].texture;var foodRect=foodArt[0].rect;
            for(int i=0;i<4;i++){fragments[i]=UnityEngine.Sprite.Create(foodTexture,new Rect(foodRect.x+(i%2)*foodRect.width*.5f,foodRect.y+(i/2)*foodRect.height*.5f,foodRect.width*.5f,foodRect.height*.5f),Vector2.one*.5f,foodRect.width*.5f);sprites.Add(fragments[i]);}
            for(int i=0;i<shards.Length;i++){shards[i]=Sprite("부서진 음식 "+i,fragments[i%4],8);shards[i].enabled=false;GroundVisualSorting.Apply(shards[i],8);}
            foodWarning=Line("음식 낙하와 충격",.12f,new Color(1,.77f,.23f,.9f),7);
            bubbleRings=new LineRenderer[20];for(int i=0;i<20;i++)bubbleRings[i]=Line("탄산 기포 "+i,.08f,new Color(1,.93f,.63f,.8f),18);
            bubbleMaterial=new Material(shader);bubbleMaterial.SetFloat("_Mode",3);bubbleMaterial.SetColor("_Color",new Color(1,.75f,.25f,.85f));bubbleDiscs=new MeshRenderer[20];
            for(int i=0;i<20;i++){bubbleDiscs[i]=Quad("기포 표면 "+i,new Rect(-1,-1,2,2),bubbleMaterial,0);GroundVisualSorting.Apply(bubbleDiscs[i],17);bubbleDiscs[i].enabled=false;}
            wind=new LineRenderer[32];for(int i=0;i<wind.Length;i++)wind[i]=Line("흐름과 재채기 "+i,.07f,new Color(.72f,.96f,1,.7f),20);
            jumpArcs=new LineRenderer[3];for(int i=0;i<3;i++)jumpArcs[i]=Line("협곡 점프 안내 "+i,.075f,new Color(.45f,1,.88f,.8f),5);
            BuildHud();configured=true;
        }
        MeshRenderer Quad(string name,Rect rect,Material material,int order)
        {
            var mesh=new Mesh{name=name};mesh.vertices=new[]{new Vector3(rect.xMin,rect.yMin),new Vector3(rect.xMax,rect.yMin),new Vector3(rect.xMax,rect.yMax),new Vector3(rect.xMin,rect.yMax)};
            mesh.uv=new[]{Vector2.zero,Vector2.right,Vector2.one,Vector2.up};mesh.triangles=new[]{0,2,1,0,3,2};mesh.RecalculateBounds();meshes.Add(mesh);
            return Render(name,mesh,material,order);
        }
        MeshRenderer Render(string name,Mesh mesh,Material mat,int order)
        {
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(root,false);
            go.GetComponent<MeshFilter>().sharedMesh=mesh;var r=go.GetComponent<MeshRenderer>();r.sharedMaterial=mat;
            r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;GroundVisualSorting.ApplyBackground(r,order);return r;
        }
        void BuildTongue()
        {
            const int rings=20,segments=128;var c=OralMapGeometry.TongueCenter(map);var r=OralMapGeometry.TongueRadii(map);
            var tongueUv=JsonUtility.FromJson<SurfaceUV>(Resources.Load<TextAsset>("OralMap/TongueUV").text);
            var palateUv=JsonUtility.FromJson<SurfaceUV>(Resources.Load<TextAsset>("OralMap/PalateUV").text);
            tongueBase=new Vector3[1+rings*segments];var uv=new Vector2[tongueBase.Length];var roofUv=new Vector2[tongueBase.Length];tongueBase[0]=c;uv[0]=tongueUv.Map(Vector2.zero);roofUv[0]=palateUv.Map(Vector2.zero);
            for(int j=1;j<=rings;j++)for(int i=0;i<segments;i++)
            {float a=i*Mathf.PI*2/segments;var q=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*j/rings;int k=1+(j-1)*segments+i;
                tongueBase[k]=OralMapGeometry.TonguePoint(map,q);uv[k]=tongueUv.Map(q);roofUv[k]=palateUv.Map(q);}
            var triangles=new List<int>();for(int i=0;i<segments;i++){triangles.Add(0);triangles.Add(1+(i+1)%segments);triangles.Add(1+i);}
            for(int j=1;j<rings;j++)for(int i=0;i<segments;i++){int a=1+(j-1)*segments+i,b=1+(j-1)*segments+(i+1)%segments,cc=a+segments,d=b+segments;triangles.Add(a);triangles.Add(b);triangles.Add(cc);triangles.Add(b);triangles.Add(d);triangles.Add(cc);}
            tongue=new Mesh{name="혀 · 2560개 정점의 연속 변형"};tongue.MarkDynamic();tongueVertices=(Vector3[])tongueBase.Clone();tongue.vertices=tongueVertices;tongue.uv=uv;tongue.uv2=roofUv;tongue.triangles=triangles.ToArray();tongue.RecalculateBounds();meshes.Add(tongue);
            Render("혀 표면",tongue,tongueMaterial,-880);
            tongueOutline=Line("움직이는 혀 외곽선",.24f,new Color(.43f,.10f,.20f),-870,true);
            tongueHighlight=Line("혀 안쪽 윤곽",.09f,new Color(1,.59f,.66f,.8f),-869,true);
        }
        [System.Serializable] sealed class SurfaceUV
        {
            // Sampled from each source alpha contour; independently maps tongue and
            // palate silhouettes onto the exact navigable ellipse without empty lobes.
            public float bottom,top;public float[] left,right;
            public Vector2 Map(Vector2 q)
            {
                float v=Mathf.Clamp01(q.y*.5f+.5f),row=v*(left.Length-1);
                int a=Mathf.Min(Mathf.FloorToInt(row),left.Length-1),b=Mathf.Min(a+1,left.Length-1);
                float lo=Mathf.Lerp(left[a],left[b],row-a),hi=Mathf.Lerp(right[a],right[b],row-a);
                float across=q.x/Mathf.Max(.0001f,Mathf.Sqrt(Mathf.Max(0,1-q.y*q.y)));
                return new Vector2(Mathf.Lerp(lo,hi,Mathf.Clamp01(across*.5f+.5f)),Mathf.Lerp(bottom,top,v));
            }
        }
        Sprite[] Atlas(string resource)
        {
            var tex=Resources.Load<Texture2D>(resource);var result=new Sprite[4];if(tex==null)return result;
            int w=tex.width/2,h=tex.height/2;
            for(int i=0;i<4;i++){result[i]=UnityEngine.Sprite.Create(tex,new Rect(i%2*w,(1-i/2)*h,w,h),Vector2.one*.5f,w);sprites.Add(result[i]);}return result;
        }
        SpriteRenderer Sprite(string name,Sprite art,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);var sr=go.AddComponent<SpriteRenderer>();sr.sprite=art;
            // Sprites/Default carries per-sprite atlas UVs, texture and alpha correctly.
            GroundVisualSorting.ApplyBackground(sr,order);return sr;
        }
        static void Set(SpriteRenderer sr,Vector2 p,float width){sr.transform.position=p;sr.transform.localScale=Vector3.one*width;}
        LineRenderer Line(string name,float width,Color color,int order,bool background=false)
        {
            var go=new GameObject(name);go.transform.SetParent(root,false);var line=go.AddComponent<LineRenderer>();
            line.sharedMaterial=LineMaterial;line.useWorldSpace=true;line.startWidth=line.endWidth=width;line.startColor=line.endColor=color;
            line.numCapVertices=4;line.numCornerVertices=3;line.enabled=false;if(background)GroundVisualSorting.ApplyBackground(line,order);else GroundVisualSorting.Apply(line,order);return line;
        }
        Material lineMaterial; Material LineMaterial=>lineMaterial??(lineMaterial=new Material(Shader.Find("Sprites/Default")));
        static void Circle(LineRenderer line,Vector2 c,Vector2 radii,float arc=1)
        {line.enabled=true;line.loop=arc>=.999f;const int count=65;line.positionCount=count;for(int i=0;i<count;i++){float a=i/(float)(count-1)*Mathf.PI*2*arc;line.SetPosition(i,c+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),radii));}}
        void BuildCrown() { OralMapGeometry.Crown=SampleCrown(toothArt[0]);cavityCrown=SampleCrown(toothArt[3]); }
        Vector2[] SampleCrown(Sprite art) {
            var tex=art.texture;var rect=art.rect;
            var points=new Vector2[128];
            for(int i=0;i<points.Length;i++) {
                float angle=i*Mathf.PI*2/points.Length;var dir=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
                float last=0;
                for(float r=0;r<rect.width*.7f;r+=1) {
                    var p=rect.center+dir*r;
                    if(p.x<rect.xMin||p.x>=rect.xMax||p.y<rect.yMin||p.y>=rect.yMax)break;
                    if(tex.GetPixel((int)p.x,(int)p.y).a>.5f)last=r;
                }
                points[i]=dir*(last/rect.width*OralMapGeometry.ToothScale);
            }
            return points;
        }
        static void TraceCrown(LineRenderer line,Vector2 center,float progress,Vector2[] contour=null) {
            var points=contour??OralMapGeometry.Crown;line.enabled=true;line.loop=progress>=1;
            int count=Mathf.Max(2,Mathf.CeilToInt(points.Length*progress));line.positionCount=count;
            for(int i=0;i<count;i++)line.SetPosition(i,center+points[i%points.Length]);
        }
        void BuildHud()
        {
            hud=new GameObject("구강 이벤트 안내",typeof(Canvas),typeof(CanvasScaler));hud.transform.SetParent(root,false);var canvas=hud.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=40;
            var scaler=hud.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);
            title=Label("진행 중인 이벤트",22,new Vector2(.5f,0),new Vector2(0,195),new Vector2(1080,35));
            hint=Label("조작 안내",16,new Vector2(.5f,0),new Vector2(0,164),new Vector2(1080,28));hint.color=new Color(1,.91f,.72f);
        }
        Text Label(string name,int size,Vector2 anchor,Vector2 pos,Vector2 dimensions)
        {var go=new GameObject(name,typeof(RectTransform),typeof(Text),typeof(Outline));go.transform.SetParent(hud.transform,false);var text=go.GetComponent<Text>();text.font=Resources.Load<Font>("TrainingUI/Cafe24Ssurround");text.fontSize=size;text.alignment=TextAnchor.MiddleCenter;text.color=Color.white;text.raycastTarget=false;var rt=text.rectTransform;rt.anchorMin=rt.anchorMax=anchor;rt.anchoredPosition=pos;rt.sizeDelta=dimensions;go.GetComponent<Outline>().effectColor=new Color(.13f,.04f,.08f,.95f);go.GetComponent<Outline>().effectDistance=new Vector2(2,-2);return text;}
        void LateUpdate()
        {
            if(!configured||runtime==null||map==null)return;
            bool visible=map.IsOral&&!MiniStageRuntimeState.IsInsideMiniStage;root.gameObject.SetActive(visible);if(!visible)return;
            if(!runtime.Level.IsRunFlowPaused)clock+=Time.deltaTime;
            var events=GetComponent<OralFieldEvents>();var kind=events!=null?events.CurrentEvent:OralEventKind.None;
            float elapsed=events!=null?events.Elapsed:0;bool ceiling=runtime.OnCeiling;
            AnimateTongue(kind,elapsed,ceiling);
            AnimateTeeth(events,kind,elapsed);AnimateFood(events,kind,elapsed);AnimateFluid(kind,elapsed);AnimateWind(kind,elapsed);AnimateBubbles(events,kind);AnimateCavities(events);AnimateJump();
            bool showHud=!StageEntryLoading.IsLoading&&!runtime.Level.IsLevelEnded&&(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page=="hud");
            hud.SetActive(showHud);
            title.text=events!=null&&kind!=OralEventKind.None&&elapsed>3?events.Banner:"";
            hint.text=ceiling?"거꾸로 붙어 있어도 조작 방향은 같아요":kind==OralEventKind.Jaw?"윗니가 내려옵니다! 혀로 점프해 피하세요":kind==OralEventKind.Coffee?"커피수혈 · 커피콩이 도는 몬스터를 주의하세요":"혀 가장자리에서 바깥 방향 대쉬 → 치아로 점프  |  충치 가까이에서 상호작용";
        }
        void AnimateTongue(OralEventKind kind,float elapsed,bool ceiling)
        {
            var c=OralMapGeometry.TongueCenter(map);var r=OralMapGeometry.TongueRadii(map);
            float flick=kind==OralEventKind.Ceiling?Mathf.Sin(Mathf.Clamp01(elapsed/2.5f)*Mathf.PI):0;
            for(int i=0;i<tongueVertices.Length;i++)
            {var p=tongueBase[i];float y=(p.y-c.y)/r.y,x=(p.x-c.x)/r.x;
                float belly=(1-x*x)*Mathf.Max(0,1-y*y);p.y+=belly*(Mathf.Sin(clock*1.4f+y*3)*.10f+flick*3.2f*Mathf.Sin(y*2+elapsed*4));tongueVertices[i]=p;}
            tongue.vertices=tongueVertices;tongue.RecalculateBounds();
            var e=GetComponent<OralFieldEvents>();float ceilingBlend=ceiling?1:0;
            if(e!=null&&kind==OralEventKind.Ceiling){if(e.CeilingPhase==1||e.CeilingPhase==3)ceilingBlend=e.CeilingTravel01;}
            tongueMaterial.SetFloat("_Ceiling",ceilingBlend);
            foreach(var outline in new[]{tongueOutline,tongueHighlight}) {
                outline.enabled=true;outline.loop=true;outline.positionCount=128;
                for(int i=0;i<128;i++){float a=i*Mathf.PI*2/128;var q=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                    outline.SetPosition(i,OralMapGeometry.TonguePoint(map,q*(outline==tongueHighlight?.99f:1)));}
            }
        }
        void AnimateTeeth(OralFieldEvents events,OralEventKind kind,float t)
        {
            float close=events!=null?events.JawAmount:0;
            foreach(var line in new[]{lowerGum,upperGum,upperGumEdge}) {
                line.enabled=true;
                if(line!=lowerGum)GroundVisualSorting.ApplyBackground(line,kind==OralEventKind.Jaw?(line==upperGum?-835:-836):(line==upperGum?-905:-906));
                line.positionCount=toothPositions.Length;
                for(int j=0;j<toothPositions.Length;j++)line.SetPosition(j,toothPositions[j]+(line==lowerGum?Vector2.zero:Vector2.up*Mathf.Lerp(9,0,close)));
            }
            for(int i=0;i<lower.Length;i++)
            {
                float shake=kind==OralEventKind.Jaw&&t<2?Mathf.Sin(t*40)*.075f:0;
                upper[i].transform.position=toothPositions[i]+new Vector2(shake,Mathf.Lerp(9f,0,close));
                GroundVisualSorting.ApplyBackground(upper[i],kind==OralEventKind.Jaw?-830:-900);
                upper[i].color=kind==OralEventKind.Jaw?Color.Lerp(new Color(.76f,.64f,.61f),Color.white,close):new Color(.76f,.64f,.61f);
                upper[i].transform.localScale=new Vector3(OralMapGeometry.ToothScale,OralMapGeometry.ToothScale,1);
                if(kind==OralEventKind.Jaw){TraceCrown(warnings[i],toothPositions[i],1);warnings[i].startColor=warnings[i].endColor=new Color(1,.45f+.25f*Mathf.Sin(t*10),.16f,.8f);}
                else warnings[i].enabled=false;
            }
        }
        void AnimateFood(OralFieldEvents e,OralEventKind kind,float t)
        {
            for(int i=0;i<food.Length;i++)food[i].enabled=false;
            for(int i=0;i<shards.Length;i++)shards[i].enabled=false;
            foodWarning.enabled=false;
            if(kind!=OralEventKind.Food||e==null)return;
            Vector2 target=e.TargetPosition;int foodIndex=0;
            if(t<2.2f){food[foodIndex].enabled=true;float p=Mathf.Clamp01(t/2.2f);Set(food[foodIndex],target+Vector2.up*(12*(1-p*p)),5.8f);food[foodIndex].transform.rotation=Quaternion.Euler(0,0,Mathf.Sin(p*5)*12);Circle(foodWarning,target,Vector2.one*3f);}
            else for(int i=0;i<shards.Length;i++)
            {float p=Mathf.Clamp01((t-2.2f)/1.1f),a=i*2.39996f;shards[i].enabled=true;var point=target+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*(1.2f+(i%3)*.9f)*p;point.y+=Mathf.Sin(p*Mathf.PI)*1.6f;Set(shards[i],point,.5f+(i%3)*.25f);shards[i].transform.rotation=Quaternion.Euler(0,0,i*37+p*90);shards[i].color=new Color(1,1,1,1-Mathf.InverseLerp(4.8f,6.5f,t));}
        }
        void AnimateFluid(OralEventKind kind,float t)
        {bool liquid=kind==OralEventKind.Water||kind==OralEventKind.Coffee||kind==OralEventKind.Soda;float pour=6.8f;fluid.enabled=liquid&&t>1.2f&&t<1.2f+pour;if(!fluid.enabled)return;
            var events=GetComponent<OralFieldEvents>();fluidMaterial.SetFloat("_Slant",events!=null?events.FlowDirection.x:0);
            fluidMaterial.SetFloat("_Clock",t);fluidMaterial.SetFloat("_Progress",Mathf.Clamp01((t-1.2f)/pour));fluidMaterial.SetFloat("_Fluid",kind==OralEventKind.Water?0:kind==OralEventKind.Coffee?1:2);}
        void AnimateWind(OralEventKind kind,float t)
        {bool active=kind==OralEventKind.Sneeze;var c=OralMapGeometry.TongueCenter(map);var r=OralMapGeometry.TongueRadii(map);
            var events=GetComponent<OralFieldEvents>();Vector2 direction=events!=null?events.FlowDirection:Vector2.down;var across=new Vector2(-direction.y,direction.x);
            for(int i=0;i<wind.Length;i++){var l=wind[i];l.enabled=active;if(!active)continue;float p=Mathf.Repeat(t*(t<1.6f?.23f:1.2f)+i*.133f,1);float row=(i%8)/7f;Vector2 head=c+direction*Mathf.Lerp(-r.y,r.y,p)+across*Mathf.Lerp(-r.x*.7f,r.x*.7f,row);l.positionCount=12;l.startColor=l.endColor=new Color(.74f,.96f,1,t<1.6f?.32f:.8f);for(int j=0;j<12;j++){float u=j/11f;l.SetPosition(j,head-direction*u*(t<1.6f?1.2f:5)+across*(Mathf.Sin(u*4+i)*.2f));}}
        }
        void AnimateBubbles(OralFieldEvents events,OralEventKind kind)
        {for(int i=0;i<bubbleRings.Length;i++){bubbleRings[i].enabled=false;bubbleDiscs[i].enabled=false;}if(events==null||kind!=OralEventKind.Soda)return;
            int n=Mathf.Min(events.Bubbles.Count,bubbleRings.Length);for(int i=0;i<n;i++){var b=events.Bubbles[i];float p=Mathf.Clamp01(b.Age/b.Lifetime);float size=b.Radius*(.35f+.65f*p);Circle(bubbleRings[i],b.Position,Vector2.one*b.Radius);bubbleRings[i].startColor=bubbleRings[i].endColor=new Color(1,.94f,.65f,b.Burst?.3f:.6f);var disc=bubbleDiscs[i];disc.enabled=!b.Burst;disc.transform.position=b.Position+Vector2.up*(size*.2f);disc.transform.localScale=new Vector3(size,size*.8f,1);}}
        void AnimateCavities(OralFieldEvents events)
        {
            int liveCount=events!=null?events.Cavities.Count:0;
            if(liveCount==0)foreach(var tooth in lower)if(tooth!=null)tooth.enabled=true;
            for(int i=liveCount;i<cavities.Count;i++)
            {if(cavities[i]!=null)cavities[i].enabled=false;if(cavityProgress[i]!=null)cavityProgress[i].enabled=false;}
            if(events==null)return;
            while(cavities.Count<events.Cavities.Count){int i=cavities.Count;var sr=Sprite("충치와 치료 "+i,toothArt[3],-840);var state=events.Cavities[i];if(state.Anchor!=null)sr.transform.SetParent(state.Anchor,true);cavities.Add(sr);cavityProgress.Add(Line("치료 진행 "+i,.16f,new Color(.45f,1,.69f),22));}
            for(int i=0;i<liveCount;i++){var state=events.Cavities[i];var sr=cavities[i];if(sr==null)continue;sr.enabled=true;sr.sprite=state.Healed?toothArt[0]:toothArt[3];Set(sr,state.Position,OralMapGeometry.ToothScale);sr.color=state.Healed?new Color(.83f,1,.90f):Color.white;lower[state.ToothIndex].enabled=false;
                var ring=cavityProgress[i];if(state.Progress>0&&!state.Healed)TraceCrown(ring,state.Position,state.Progress,cavityCrown);else ring.enabled=false;}
        }
        void AnimateJump()
        {
            var player=runtime.Level.PlayerCharacter;var c=OralMapGeometry.TongueCenter(map);var p=(Vector2)player.transform.position;var direction=(p-c).normalized;
            if(OralMapGeometry.ZoneAt(map,p)==OralZone.Teeth)direction=-direction;
            Vector2 landing=p;
            bool show=!runtime.OnCeiling&&OralMapGeometry.TryJump(map,p,direction,out landing);
            for(int i=0;i<jumpArcs.Length;i++){var l=jumpArcs[i];l.enabled=show;if(!show)continue;l.positionCount=8;for(int j=0;j<8;j++){float u=(i/3f)+(j/7f)*.23f;var q=Vector2.Lerp(p,landing,u)+Vector2.up*(Mathf.Sin(u*Mathf.PI)*1.1f);l.SetPosition(j,q);}}
        }
        void OnDestroy()
        {if(root!=null)Destroy(root.gameObject);foreach(var m in meshes)if(m!=null)Destroy(m);foreach(var s in sprites)if(s!=null)Destroy(s);
            foreach(var m in new[]{surface,spriteMaterial,tongueMaterial,fluidMaterial,lineMaterial,bubbleMaterial,backdrop,gumMaterial,southWallMaterial})if(m!=null)Destroy(m);}
    }
}

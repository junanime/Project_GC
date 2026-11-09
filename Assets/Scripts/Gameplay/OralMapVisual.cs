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
        Sprite[] toothArt, foodArt; SpriteRenderer[] food, shards;
        Vector2[] toothPositions; LineRenderer[] warnings, bubbleRings, wind, jumpArcs;
        readonly List<Mesh> meshes=new List<Mesh>(); readonly List<Sprite> sprites=new List<Sprite>();
        readonly List<LineRenderer> cavityProgress=new List<LineRenderer>();
        readonly List<SpriteRenderer> cavities=new List<SpriteRenderer>();
        LineRenderer tongueOutline, tongueHighlight; GameObject hud; Text title, hint;
        LineRenderer foodWarning; MeshRenderer[] bubbleDiscs;Material bubbleMaterial; Sprite[] fragments;
        float clock; bool configured; Vector2[][] toothContours; Sprite cavityHole;

        Material cavityMaterial,approvedFloorMaterial,upperJawMaterial;
        Material toothSurfaceLightingMaterial;
        MeshRenderer toothSurfaceLighting;
        public MeshRenderer ToothSurfaceLighting=>toothSurfaceLighting;
        public Material ToothSurfaceLightingMaterial=>toothSurfaceLightingMaterial;
        MeshRenderer upperJaw;
        MeshRenderer[] warningFills;Material warningFillMaterial;float warningFillOpacity;
        static readonly int WarningClockId=Shader.PropertyToID("_Clock"),WarningColorId=Shader.PropertyToID("_Color");
        public MeshRenderer[] JawWarningFills=>warningFills;
        public Material JawWarningFillMaterial=>warningFillMaterial;
        public float JawWarningFillOpacity=>warningFillOpacity;
        public int JawWarningFillCount {get {int count=0;if(warningFills!=null)foreach(var fill in warningFills)if(fill!=null&&fill.enabled)count++;return count;}}
        public bool UpperJawVisible=>upperJaw!=null&&upperJaw.enabled;
        public int JawWarningCount=>warnings==null?0:System.Array.FindAll(warnings,w=>w.enabled).Length;
        public LineRenderer[] JawWarningLines=>warnings;
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
            approvedFloorMaterial=new Material(spriteMaterial);approvedFloorMaterial.mainTexture=Resources.Load<Texture2D>("OralMap/ApprovedFloor");
            Quad("승인된 구강 · 아랫니와 협곡",map.WorldRect,approvedFloorMaterial,-920);
            BuildToothSurfaceLighting(shader);
            tongueMaterial=new Material(spriteMaterial);tongueMaterial.SetFloat("_Mode",8);
            tongueMaterial.mainTexture=approvedFloorMaterial.mainTexture;
            tongueMaterial.SetTexture("_SecondTex",Resources.Load<Texture2D>("OralMap/Palate"));
            fluidMaterial=new Material(shader);fluidMaterial.SetFloat("_Mode",2);
            var tongueCenter=OralMapGeometry.TongueCenter(map);var tongueRadii=OralMapGeometry.TongueRadii(map);
            fluidMaterial.SetVector("_TongueCenter",new Vector4((tongueCenter.x-map.WorldRect.xMin)/map.WorldRect.width,(tongueCenter.y-map.WorldRect.yMin)/map.WorldRect.height,0,0));
            fluidMaterial.SetVector("_TongueRadii",new Vector4(tongueRadii.x/map.WorldRect.width,tongueRadii.y/map.WorldRect.height,0,0));
            fluidMaterial.SetFloat("_SouthFloor",(OralMapGeometry.SouthFloor(map)-map.WorldRect.yMin)/map.WorldRect.height);
            BuildTongue();
            toothArt=Atlas("OralMap/Teeth");foodArt=Atlas("OralMap/Food");
            BuildCrown();toothPositions=OralMapGeometry.ToothCenters(map);int n=toothPositions.Length;
            toothContours=new Vector2[n][];warnings=new LineRenderer[n];
            for(int i=0;i<n;i++) {
                var absolute=OralMapGeometry.ToothWalkPolygon(map,i);toothContours[i]=new Vector2[absolute.Length];
                for(int j=0;j<absolute.Length;j++)toothContours[i][j]=absolute[j]-toothPositions[i];
                warnings[i]=Line("압착 예고 "+i,.10f,new Color(1,.65f,.2f),16);
            }
            BuildWarningFills(shader);
            // Same superior view as the lower jaw. The smooth crown sides hang
            // below the upper gum; no reversed lower-tooth sprites or occlusal faces.
            upperJawMaterial=new Material(spriteMaterial);upperJawMaterial.mainTexture=Resources.Load<Texture2D>("OralMap/UpperJawTop");
            var jawRect=map.WorldRect;jawRect.yMin=map.WorldRect.yMax-(1121*1.327f-317)/1121*map.WorldRect.height;jawRect.height=map.WorldRect.height*1.327f;
            upperJaw=Quad("윗잇몸과 윗니 · 위에서 내려다본 시점",jawRect,upperJawMaterial,-800);upperJaw.enabled=false;
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
        void BuildWarningFills(Shader shader)
        {
            warningFillMaterial=new Material(shader){name="치아별 반투명 RGB 압착 경고"};
            warningFillMaterial.SetFloat("_Mode",9);
            warningFillMaterial.SetFloat("_StripeWidth",.85f);
            warningFillMaterial.SetFloat("_HatchContrast",.75f);
            warningFillMaterial.SetColor("_Color",new Color(1,1,1,.22f));
            warningFills=new MeshRenderer[OralMapGeometry.ToothCount];
            for(int tooth=0;tooth<warningFills.Length;tooth++)
            {
                // Use the exact damage contour, not a bounding box or a triangle
                // fan: both would paint over the concave tooth edges / safe seams.
                var polygon=OralMapGeometry.ToothDamagePolygon(map,tooth);
                var vertices=new Vector3[polygon.Length];var colors=new Color[polygon.Length];
                for(int i=0;i<polygon.Length;i++){vertices[i]=polygon[i];colors[i]=Color.white;}
                var mesh=new Mesh{name="치아 "+tooth+" · 실제 피해 영역 빗금"};
                mesh.vertices=vertices;mesh.colors=colors;mesh.triangles=TriangulateWarningPolygon(polygon);mesh.RecalculateBounds();meshes.Add(mesh);
                var fill=Render("압착 위험면 "+tooth,mesh,warningFillMaterial,15);
                GroundVisualSorting.Apply(fill,15);fill.enabled=false;warningFills[tooth]=fill;
            }
        }
        void BuildToothSurfaceLighting(Shader shader)
        {
            // A: diffuse ivory light on the actual walkable top. C: a narrow,
            // soft apricot shadow on the inward slope. E: faint reflected light
            // through the safe contacts. Keep the approved painting and its UVs;
            // no rescaled concept-board art or separate navigation outline.
            toothSurfaceLightingMaterial=new Material(shader){name="치아 윗면 조명 · 비탈 그림자 · 은은한 연결"};
            toothSurfaceLightingMaterial.SetFloat("_Mode",10);
            toothSurfaceLightingMaterial.mainTexture=approvedFloorMaterial.mainTexture;
            float[] rows={0,.08f,.2f,.5f,.8f,.92f,1,1.15f,1.4f,1.7f,2};
            int sections=OralMapGeometry.ToothCount*OralMapGeometry.ToothSurfaceSamplesPerTooth;
            var vertices=new Vector3[(sections+1)*rows.Length];
            var uv=new Vector2[vertices.Length];var surfaceUv=new Vector2[vertices.Length];
            var colors=new Color[vertices.Length];var triangles=new int[sections*(rows.Length-1)*6];
            float minSlope=map.WorldRect.height*12/1121f,maxSlope=map.WorldRect.height*22/1121f;
            for(int section=0;section<=sections;section++)
            {
                float toothT=section/(float)OralMapGeometry.ToothSurfaceSamplesPerTooth;
                var outer=OralMapGeometry.ToothSurfacePoint(map,toothT,0);
                var inner=OralMapGeometry.ToothSurfacePoint(map,toothT,1);
                var inward=(inner-outer).normalized;
                float slopeWidth=Mathf.Clamp(Vector2.Distance(outer,inner)*.85f,minSlope,maxSlope);
                for(int row=0;row<rows.Length;row++)
                {
                    int k=section*rows.Length+row;float across=rows[row];
                    Vector2 p=across<=1?OralMapGeometry.ToothSurfacePoint(map,toothT,across):inner+inward*(slopeWidth*(across-1));
                    vertices[k]=p;uv[k]=MapUV(p);surfaceUv[k]=new Vector2(toothT,across);colors[k]=Color.white;
                }
            }
            int index=0;
            for(int section=0;section<sections;section++)for(int row=0;row<rows.Length-1;row++)
            {
                int a=section*rows.Length+row,b=a+rows.Length;
                triangles[index++]=a;triangles[index++]=b;triangles[index++]=a+1;
                triangles[index++]=a+1;triangles[index++]=b;triangles[index++]=b+1;
            }
            var mesh=new Mesh{name="이동면과 일치하는 부드러운 치아 명암"};
            mesh.vertices=vertices;mesh.uv=uv;mesh.uv2=surfaceUv;mesh.colors=colors;mesh.triangles=triangles;mesh.RecalculateBounds();meshes.Add(mesh);
            toothSurfaceLighting=Render("치아 윗면 · 자연스러운 이동 경계",mesh,toothSurfaceLightingMaterial,-919);
        }
        // Ear clipping handles concave authored contours. Collinear contour
        // vertices remain in the mesh but need no triangle; nothing is expanded.
        internal static int[] TriangulateWarningPolygon(Vector2[] polygon)
        {
            if(polygon==null||polygon.Length<3)return new int[0];
            var remaining=new List<int>(polygon.Length);var triangles=new List<int>((polygon.Length-2)*3);
            float area=0;var origin=polygon[0];
            for(int i=0;i<polygon.Length;i++)area+=Cross(polygon[i]-origin,polygon[(i+1)%polygon.Length]-origin);
            for(int i=0;i<polygon.Length;i++)remaining.Add(area>=0?i:polygon.Length-1-i);
            int guard=polygon.Length*polygon.Length;
            while(remaining.Count>3&&guard-->0)
            {
                bool clipped=false;
                for(int k=0;k<remaining.Count;k++)
                {
                    int a=remaining[(k+remaining.Count-1)%remaining.Count],b=remaining[k],c=remaining[(k+1)%remaining.Count];
                    float corner=Cross(polygon[b]-polygon[a],polygon[c]-polygon[b]);
                    if(Mathf.Abs(corner)<.0000001f){remaining.RemoveAt(k);clipped=true;break;}
                    if(corner<0)continue;
                    bool contains=false;
                    for(int j=0;j<remaining.Count;j++)
                    {
                        int v=remaining[j];if(v==a||v==b||v==c)continue;
                        var p=polygon[v];
                        if(Cross(polygon[b]-polygon[a],p-polygon[a])>=-.0000001f&&
                           Cross(polygon[c]-polygon[b],p-polygon[b])>=-.0000001f&&
                           Cross(polygon[a]-polygon[c],p-polygon[c])>=-.0000001f){contains=true;break;}
                    }
                    if(contains)continue;
                    triangles.Add(a);triangles.Add(b);triangles.Add(c);remaining.RemoveAt(k);clipped=true;break;
                }
                if(!clipped)throw new System.InvalidOperationException("Invalid oral jaw warning contour: cannot triangulate without covering a safe seam.");
            }
            if(remaining.Count==3){triangles.Add(remaining[0]);triangles.Add(remaining[1]);triangles.Add(remaining[2]);}
            return triangles.ToArray();
        }
        static float Cross(Vector2 a,Vector2 b)=>a.x*b.y-a.y*b.x;
        void UpdateWarningFills(OralEventKind kind,float elapsed)
        {
            if(warningFills==null)return;
            bool active=kind==OralEventKind.Jaw&&elapsed<2.86f;
            for(int i=0;i<warningFills.Length;i++)warningFills[i].enabled=active;
            warningFillOpacity=0;if(!active)return;
            float urgency=Mathf.InverseLerp(.9f,2f,elapsed);
            float pulse=.5f+.5f*Mathf.Sin(elapsed*Mathf.PI*1.6f);
            warningFillOpacity=.22f+urgency*(.035f+.065f*pulse);
            // Event time stops with game pause, unlike shader _Time animations.
            warningFillMaterial.SetFloat(WarningClockId,elapsed);
            warningFillMaterial.SetColor(WarningColorId,new Color(1,1,1,warningFillOpacity));
        }

        void BuildTongue()
        {
            const int rings=20,segments=128;var c=OralMapGeometry.TongueCenter(map);var r=OralMapGeometry.TongueRadii(map);
            var tongueUv=JsonUtility.FromJson<SurfaceUV>(Resources.Load<TextAsset>("OralMap/TongueUV").text);
            var palateUv=JsonUtility.FromJson<SurfaceUV>(Resources.Load<TextAsset>("OralMap/PalateUV").text);
            tongueBase=new Vector3[1+rings*segments];var uv=new Vector2[tongueBase.Length];var roofUv=new Vector2[tongueBase.Length];tongueBase[0]=c;uv[0]=MapUV(c);roofUv[0]=palateUv.Map(Vector2.zero);
            for(int j=1;j<=rings;j++)for(int i=0;i<segments;i++)
            {float a=i*Mathf.PI*2/segments;var q=new Vector2(Mathf.Cos(a),Mathf.Sin(a))*j/rings;int k=1+(j-1)*segments+i;
                tongueBase[k]=OralMapGeometry.TonguePoint(map,q);uv[k]=MapUV(tongueBase[k]);roofUv[k]=palateUv.Map(q);}
            var triangles=new List<int>();for(int i=0;i<segments;i++){triangles.Add(0);triangles.Add(1+(i+1)%segments);triangles.Add(1+i);}
            for(int j=1;j<rings;j++)for(int i=0;i<segments;i++){int a=1+(j-1)*segments+i,b=1+(j-1)*segments+(i+1)%segments,cc=a+segments,d=b+segments;triangles.Add(a);triangles.Add(b);triangles.Add(cc);triangles.Add(b);triangles.Add(d);triangles.Add(cc);}
            tongue=new Mesh{name="혀 · 2560개 정점의 연속 변형"};tongue.MarkDynamic();tongueVertices=(Vector3[])tongueBase.Clone();tongue.vertices=tongueVertices;tongue.uv=uv;tongue.uv2=roofUv;tongue.triangles=triangles.ToArray();tongue.RecalculateBounds();meshes.Add(tongue);
            Render("혀 표면",tongue,tongueMaterial,-880);
            tongueOutline=Line("움직이는 혀 외곽선",.24f,new Color(.43f,.10f,.20f),-870,true);
            tongueHighlight=Line("혀 안쪽 윤곽",.09f,new Color(1,.59f,.66f,.8f),-869,true);
        }
        Vector2 MapUV(Vector2 p)=>new Vector2((p.x-map.WorldRect.xMin)/map.WorldRect.width,(p.y-map.WorldRect.yMin)/map.WorldRect.height);
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
        void BuildCrown() {

            // Reuse only the existing cavity mark, so treatment never turns an
            // incisor or canine into a molar and never changes its walkable edge.
            var tex=toothArt[3].texture;var rect=new Rect(1040f/1536*tex.width,256f/1024*tex.height,155f/1536*tex.width,128f/1024*tex.height);
            cavityHole=UnityEngine.Sprite.Create(tex,rect,Vector2.one*.5f,tex.width*.5f);sprites.Add(cavityHole);
            cavityMaterial=new Material(Resources.Load<Shader>("OralMap/Surface"));cavityMaterial.SetFloat("_Mode",7);
            cavityMaterial.SetVector("_HoleRect",new Vector4(rect.x/tex.width,rect.y/tex.height,rect.width/tex.width,rect.height/tex.height));
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
            AnimateTeeth(events,kind,elapsed);UpdateWarningFills(kind,elapsed);AnimateFood(events,kind,elapsed);AnimateFluid(kind,elapsed);AnimateWind(kind,elapsed);AnimateBubbles(events,kind);AnimateCavities(events);AnimateJump();
            bool showHud=!StageEntryLoading.IsLoading&&!runtime.Level.IsLevelEnded&&(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page=="hud");
            hud.SetActive(showHud);
            title.text=events!=null&&kind!=OralEventKind.None&&elapsed>3?events.Banner:"";
            hint.text=ceiling?"거꾸로 붙어 있어도 조작 방향은 같아요":kind==OralEventKind.Jaw?"윗니가 내려옵니다! 치아 사이 틈이나 혀로 피하세요":kind==OralEventKind.Coffee?"커피수혈 · 커피콩이 도는 몬스터를 주의하세요":"송곳니 직전까지 점프 가능  |  앞니 안쪽으로 벗어나면 혀로 미끄러져요";
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
            // The approved art already contains the tongue edge; do not add a
            // second floating outline over the continuous painted surface.
            tongueOutline.enabled=false;tongueHighlight.enabled=false;
        }
        void AnimateTeeth(OralFieldEvents events,OralEventKind kind,float t)
        {
            float close=events!=null?events.JawAmount:0;
            bool dropping=kind==OralEventKind.Jaw&&t>=2&&t<3.51f;
            upperJaw.enabled=dropping;
            if(dropping) {
                upperJaw.transform.localPosition=Vector3.up*(map.WorldRect.height*250/1121*(1-close));
                float opacity=Mathf.InverseLerp(2,2.07f,t)*(1-Mathf.InverseLerp(3.35f,3.51f,t));
                upperJawMaterial.SetColor("_Color",new Color(1,1,1,opacity));
            }
            for(int i=0;i<warnings.Length;i++) {
                var line=warnings[i];line.enabled=kind==OralEventKind.Jaw&&t<2.86f;
                if(!line.enabled)continue;
                // Exactly the same per-tooth polygon is queried by jaw damage.
                var points=OralMapGeometry.ToothDamagePolygon(map,i);
                line.loop=true;line.positionCount=points.Length;
                for(int j=0;j<points.Length;j++)line.SetPosition(j,points[j]);
                line.startColor=line.endColor=new Color(1,.45f+.25f*Mathf.Sin(t*10),.16f,.9f);
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

            for(int i=liveCount;i<cavities.Count;i++)
            {if(cavities[i]!=null)cavities[i].enabled=false;if(cavityProgress[i]!=null)cavityProgress[i].enabled=false;}
            if(events==null)return;
            while(cavities.Count<events.Cavities.Count){int i=cavities.Count;var state=events.Cavities[i];var sr=Sprite("충치와 치료 "+i,cavityHole,-849+OralMapGeometry.ToothDepth(state.ToothIndex));sr.sharedMaterial=cavityMaterial;if(state.Anchor!=null)sr.transform.SetParent(state.Anchor,true);cavities.Add(sr);cavityProgress.Add(Line("치료 진행 "+i,.16f,new Color(.45f,1,.69f),22));}
            for(int i=0;i<liveCount;i++){var state=events.Cavities[i];var sr=cavities[i];if(sr==null)continue;sr.enabled=!state.Healed;Set(sr,state.Position,8);sr.transform.rotation=Quaternion.Euler(0,0,OralMapGeometry.ToothAngle(state.ToothIndex));
                var ring=cavityProgress[i];if(state.Progress>0&&!state.Healed)TraceCrown(ring,state.Position,state.Progress,toothContours[state.ToothIndex]);else ring.enabled=false;}
        }
        void AnimateJump()
        {
            var player=runtime.Level.PlayerCharacter;var c=OralMapGeometry.TongueCenter(map);var p=(Vector2)player.transform.position;var direction=(p-c).normalized;
            if(OralMapGeometry.ZoneAt(map,p)==OralZone.Teeth)direction=-direction;
            Vector2 landing=p;
            bool show=!runtime.OnCeiling&&!runtime.IsSliding&&OralMapGeometry.TryJump(map,p,direction,out landing);
            for(int i=0;i<jumpArcs.Length;i++){var l=jumpArcs[i];l.enabled=show;if(!show)continue;l.positionCount=8;for(int j=0;j<8;j++){float u=(i/3f)+(j/7f)*.23f;var q=Vector2.Lerp(p,landing,u)+Vector2.up*(Mathf.Sin(u*Mathf.PI)*1.1f);l.SetPosition(j,q);}}
        }
        void OnDestroy()
        {if(root!=null)Destroy(root.gameObject);foreach(var m in meshes)if(m!=null)Destroy(m);foreach(var s in sprites)if(s!=null)Destroy(s);
            foreach(var m in new[]{surface,spriteMaterial,tongueMaterial,fluidMaterial,lineMaterial,bubbleMaterial,backdrop,cavityMaterial,approvedFloorMaterial,upperJawMaterial,warningFillMaterial,toothSurfaceLightingMaterial})if(m!=null)Destroy(m);}
    }
}

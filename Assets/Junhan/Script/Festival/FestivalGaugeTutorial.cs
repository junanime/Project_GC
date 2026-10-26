using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Vampire
{
    // Per-participant introduction. No PlayerPrefs: each new festival run gets the guide.
    [DefaultExecutionOrder(20000)]
    public sealed class FestivalGaugeTutorial : MonoBehaviour
    {
        public static FestivalGaugeTutorial Instance { get; private set; }
        static int closedFrame=-10;
        public static bool IsOpen => Instance!=null;
        public static bool BlocksInput => IsOpen || Time.frameCount<=closedFrame+1;
        LevelManager level;
        PlayerCombatBars bars;
        RectTransform surface,frame;
        FestivalGaugeSpotlight spotlight;
        readonly Vector3[] corners=new Vector3[4];
        readonly RectTransform[] lines=new RectTransform[6];
        TMP_FontAsset font;
        bool ownsFont,closing,ownsPause;
        int dismissFrame;
        float shownAt;
        static readonly Color Ink=new Color(.06f,.07f,.12f), Red=new Color(.96f,.31f,.38f), Blue=new Color(.15f,.72f,.83f), Gold=new Color(.91f,.65f,.14f);
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset(){Instance=null;closedFrame=-10;}

        public static void Show(LevelManager owner)
        {
            if(!FestivalDemo.Enabled || owner==null || Instance!=null)return;
            var view=new GameObject("Festival gauge introduction",typeof(RectTransform)).AddComponent<FestivalGaugeTutorial>();
            Instance=view;view.level=owner;view.bars=owner.PlayerCharacter.GetComponent<PlayerCombatBars>();
            view.shownAt=Time.unscaledTime;view.ownsPause=true;Time.timeScale=0;
            view.Build();
        }
        static bool Held() => Keyboard.current?.spaceKey.isPressed==true || Mouse.current?.leftButton.isPressed==true || Touchscreen.current?.primaryTouch.press.isPressed==true;
        void Update()
        {
            if(level==null || level.IsLevelEnded){Destroy(gameObject);return;}
            if(closing)
            {
                // Wait for release and a complete input frame. The closing press cannot cast Shini's flame.
                if(!Held() && Time.frameCount>dismissFrame+1)Close();
                return;
            }
            if(Time.unscaledTime-shownAt<.25f)return;
            if(Keyboard.current?.spaceKey.wasPressedThisFrame==true || Mouse.current?.leftButton.wasPressedThisFrame==true || Touchscreen.current?.primaryTouch.press.wasPressedThisFrame==true)Dismiss();
        }
        public void Dismiss(){if(closing)return;closing=true;dismissFrame=Time.frameCount;}
        void Close()
        {
            closedFrame=Time.frameCount;
            if(ownsPause && level!=null && !level.IsLevelEnded && Time.timeScale==0)Time.timeScale=1;
            ownsPause=false;Instance=null;Destroy(gameObject);
        }
        void OnDestroy()
        {
            if(Instance==this){Instance=null;closedFrame=Time.frameCount;if(ownsPause&&level!=null&&!level.IsLevelEnded&&Time.timeScale==0)Time.timeScale=1;}
            if(ownsFont && font!=null){foreach(var atlas in font.atlasTextures)if(atlas!=null)Destroy(atlas);Destroy(font.material);Destroy(font);}
        }
        void LateUpdate()
        {
            if(frame==null || bars==null)return;
            frame.localScale=Vector3.one*Mathf.Min(surface.rect.width/1280,surface.rect.height/720);
            var targets=new[]{bars.HealthRect,bars.DashRect,bars.ActiveRect};
            var ends=new[]{new Vector2(760,442),new Vector2(510,260),new Vector2(760,235)};
            for(int i=0;i<3;i++)
            {
                if(targets[i]==null)continue;
                targets[i].GetWorldCorners(corners);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,corners[0],null,out var min);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(surface,corners[2],null,out var max);
                spotlight.holes[i]=Rect.MinMaxRect(min.x-3,min.y-2,max.x+3,max.y+2);
                Vector3 startWorld=targets[i].TransformPoint(new Vector3(i==1?targets[i].rect.xMin:targets[i].rect.xMax,0));
                Vector2 start=frame.InverseTransformPoint(startWorld);
                start+=new Vector2(640,360);start.x+=i==1?-7:7;
                Vector2 end=ends[i],bend=new Vector2((start.x+end.x)*.5f,end.y);
                Line(lines[i*2],start,bend);Line(lines[i*2+1],bend,end);
            }
            spotlight.SetVerticesDirty();
        }
        static void Line(RectTransform rect,Vector2 a,Vector2 b)
        {rect.anchoredPosition=(a+b)*.5f;rect.sizeDelta=new Vector2(Vector2.Distance(a,b),2);rect.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);}
        void Build()
        {
            var canvas=gameObject.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=32000;
            gameObject.AddComponent<GraphicRaycaster>();
            var scaler=gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;
            surface=(RectTransform)transform;
            var mask=Node("Dim background except actual gauges",surface);mask.anchorMin=Vector2.zero;mask.anchorMax=Vector2.one;mask.offsetMin=mask.offsetMax=Vector2.zero;
            spotlight=mask.gameObject.AddComponent<FestivalGaugeSpotlight>();spotlight.raycastTarget=true;
            frame=Node("Tutorial layout",surface);frame.anchorMin=frame.anchorMax=new Vector2(.5f,.5f);frame.sizeDelta=new Vector2(1280,720);
            var source=Resources.Load<Font>("TrainingUI/Cafe24Ssurround");font=source!=null?TMP_FontAsset.CreateFontAsset(source):Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig").font;ownsFont=source!=null;
            Plate("Tutorial title",new Rect(300,563,680,68),Red,true);
            Label("시작 전, 게이지만 확인하세요",new Rect(320,575,640,44),31,Color.white);
            Plate("Pause note",new Rect(420,518,440,35),StitchPatchTheme.Thread,false);
            Label("안내 중에는 게임 시간이 흐르지 않습니다",new Rect(430,520,420,31),18,Ink);
            var palette=new[]{Red,Blue,Gold};
            for(int i=0;i<6;i++){lines[i]=Fixed("Gauge connector "+i,frame,new Rect(0,0,1,2));lines[i].pivot=Vector2.one*.5f;var image=lines[i].gameObject.AddComponent<Image>();image.color=palette[i/2];image.raycastTarget=false;}
            Callout(new Rect(760,385,310,119),Red,"체력","빨간 게이지가 모두 줄면\n생존 종료");
            Callout(new Rect(200,204,310,114),Blue,"대시 · Shift","파란 게이지가 차면\n빠르게 회피");
            bool shini=level.PlayerCharacter.Skills.IsShini;
            Callout(new Rect(760,178,340,128),Gold,shini?"액티브 연료 · Space":"액티브 스킬 · Space",shini?"노란 게이지는 불씨 연료\nSpace를 길게 누르면 연속 발사":"노란 게이지가 차면\n스킬 사용 가능");
            Plate("Continue",new Rect(350,78,580,64),Red,true);
            Label("아무 곳이나 클릭 또는 Space로 시작",new Rect(370,88,540,45),27,Color.white);
        }
        void Callout(Rect r,Color accent,string title,string text)
        {
            Plate(title,r,accent,false);
            Plate(title+" heading",new Rect(r.x+5,r.yMax-43,r.width-10,41),accent,true);
            Label(title,new Rect(r.x+18,r.yMax-38,r.width-36,30),22,Color.white);
            Label(text,new Rect(r.x+16,r.y+12,r.width-32,r.height-56),21,Ink);
        }
        static RectTransform Node(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        static RectTransform Fixed(string name,Transform parent,Rect r)
        {var rect=Node(name,parent);rect.anchorMin=rect.anchorMax=Vector2.zero;rect.pivot=Vector2.zero;rect.anchoredPosition=r.position;rect.sizeDelta=r.size;return rect;}
        void Plate(string name,Rect r,Color accent,bool button)
        {var root=Fixed(name,frame,r);var graphic=root.gameObject.AddComponent<StitchPatchGraphic>();graphic.fill=true;graphic.button=button;graphic.accent=accent;graphic.inset=7;graphic.radius=13;graphic.raycastTarget=false;}
        void Label(string text,Rect r,float size,Color color)
        {var rect=Fixed(text,frame,r);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=size;label.color=color;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.enableWordWrapping=true;}
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FestivalGaugeSpotlight : MaskableGraphic
    {
        public readonly Rect[] holes=new Rect[3];
        readonly System.Collections.Generic.List<float> cuts=new System.Collections.Generic.List<float>(8);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;cuts.Clear();cuts.Add(r.yMin);cuts.Add(r.yMax);
            foreach(var hole in holes){cuts.Add(Mathf.Clamp(hole.yMin,r.yMin,r.yMax));cuts.Add(Mathf.Clamp(hole.yMax,r.yMin,r.yMax));}cuts.Sort();
            for(int i=0;i<cuts.Count-1;i++)
            {
                float bottom=cuts[i],top=cuts[i+1];if(top-bottom<.01f)continue;
                float left=r.xMin,right=r.xMax,mid=(bottom+top)*.5f;bool found=false;
                foreach(var hole in holes)if(mid>hole.yMin && mid<hole.yMax){left=Mathf.Clamp(hole.xMin,r.xMin,r.xMax);right=Mathf.Clamp(hole.xMax,r.xMin,r.xMax);found=true;break;}
                if(!found)Box(vh,r.xMin,bottom,r.xMax,top);
                else{Box(vh,r.xMin,bottom,left,top);Box(vh,right,bottom,r.xMax,top);}
            }
        }
        static void Box(VertexHelper vh,float x,float y,float right,float top)
        {
            if(right<=x)return;int n=vh.currentVertCount;Color c=new Color(0,0,0,.73f);
            vh.AddVert(new Vector3(x,y),c,Vector2.zero);vh.AddVert(new Vector3(x,top),c,Vector2.zero);vh.AddVert(new Vector3(right,top),c,Vector2.zero);vh.AddVert(new Vector3(right,y),c,Vector2.zero);vh.AddTriangle(n,n+1,n+2);vh.AddTriangle(n,n+2,n+3);
        }
    }
}

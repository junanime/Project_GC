using TMPro;
using UnityEngine;
using UnityEngine.UI;
namespace Vampire
{
    public sealed class FoodBossHealthHud:MonoBehaviour
    {
        FoodBossRuntime boss;GameObject root;FoodBossHealthRail rail;
        TextMeshProUGUI nameLabel,recoveryLabel;
        bool lastPhase;float flash;
        public void Initialize(FoodBossRuntime b)
        {
            boss=b;root=new GameObject("Food boss health",typeof(Canvas),typeof(CanvasScaler));
            var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=20;
            var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1280,720);scaler.matchWidthOrHeight=.5f;
            var panel=new GameObject("Shared pixel frame",typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(root.transform,false);panel.anchorMin=new Vector2(.23f,.84f);panel.anchorMax=new Vector2(.77f,.84f);panel.pivot=new Vector2(.5f,0);panel.offsetMin=Vector2.zero;panel.offsetMax=new Vector2(0,22);
            rail=new GameObject("Pixel health rail",typeof(RectTransform),typeof(CanvasRenderer),typeof(FoodBossHealthRail)).GetComponent<FoodBossHealthRail>();
            rail.transform.SetParent(panel,false);rail.rectTransform.anchorMin=Vector2.zero;rail.rectTransform.anchorMax=Vector2.one;rail.rectTransform.offsetMin=rail.rectTransform.offsetMax=Vector2.zero;rail.raycastTarget=false;
            nameLabel=Text(panel,"Boss name",TextAlignmentOptions.Bottom,new Vector2(0,25),new Vector2(0,50));
            recoveryLabel=Text(panel,"Recovery objective",TextAlignmentOptions.Top,new Vector2(0,-23),new Vector2(0,-1));recoveryLabel.fontSize=15;
        }
        TextMeshProUGUI Text(RectTransform parent,string title,TextAlignmentOptions alignment,Vector2 min,Vector2 max)
        {
            var t=new GameObject(title,typeof(RectTransform),typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            t.transform.SetParent(parent,false);t.rectTransform.anchorMin=Vector2.zero;t.rectTransform.anchorMax=Vector2.right;t.rectTransform.offsetMin=min;t.rectTransform.offsetMax=max;
            if(ApothecaryUI.Instance!=null)t.font=ApothecaryUI.Instance.Config.font;
            t.fontSize=21;t.fontStyle=FontStyles.Bold;t.alignment=alignment;t.color=new Color(1,.94f,.79f);t.outlineWidth=.06f;t.outlineColor=new Color(.07f,.055f,.09f);t.raycastTarget=false;return t;
        }
        void LateUpdate()
        {
            if(boss==null||root==null)return;
            root.SetActive(!boss.Dead&&!MiniStageRuntimeState.IsInsideMiniStage&&(ApothecaryUI.Instance==null||ApothecaryUI.Instance.Page=="hud"));
            if(boss.PhaseTwo&&!lastPhase)flash=.45f;
            lastPhase=boss.PhaseTwo;flash=Mathf.Max(0,flash-Time.deltaTime);
            rail.Set(boss.Health/boss.MaxHealth,boss.Warning,Mathf.Sin(flash/.45f*Mathf.PI));
            nameLabel.text=FoodBossArt.Names[(int)boss.Kind];
            recoveryLabel.text=boss.Recovering?(boss.Kind==FoodBossKind.Denikani?$"방향 뒤집기 {boss.Reversals}/5":"회복 저지!"):"";
        }
        void OnDestroy(){if(root!=null)Destroy(root);}
    }
    // Shared vector-native frame: pixel bevels retain their thickness at every health value.
    public sealed class FoodBossHealthRail:MaskableGraphic
    {
        float health=1,flash;Color tint=Color.magenta;
        public void Set(float value,Color fill,float pulse)
        {
            value=Mathf.Clamp01(value);
            if(Mathf.Approximately(health,value)&&tint==fill&&Mathf.Approximately(flash,pulse))return;
            health=value;tint=fill;flash=pulse;SetVerticesDirty();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=GetPixelAdjustedRect();
            Polygon(vh,r,8,new Color(.035f,.06f,.10f));
            Polygon(vh,Inset(r,2),6,Color.Lerp(new Color(.94f,.68f,.28f),Color.white,flash*.65f));
            Polygon(vh,Inset(r,4),4,new Color(.055f,.075f,.12f));
            Rect inner=Inset(r,6);float right=Mathf.Lerp(inner.xMin,inner.xMax,health);
            if(health>0)
            {
                var vertices=Points(inner,2);var clipped=new System.Collections.Generic.List<Vector2>();
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector2 a=vertices[i],b=vertices[(i+1)%vertices.Length];bool ia=a.x<=right,ib=b.x<=right;
                    if(ia)clipped.Add(a);
                    if(ia!=ib)clipped.Add(Vector2.Lerp(a,b,(right-a.x)/(b.x-a.x)));
                }
                Fan(vh,clipped.ToArray(),tint);
                if(right>inner.xMin+2)Quad(vh,new Rect(inner.xMin+2,inner.yMax-2,right-inner.xMin-2,2),Color.Lerp(tint,Color.white,.38f));
            }
            Quad(vh,new Rect(r.xMin+9,r.yMax-4,r.width-18,1),new Color(1,.91f,.58f));
            Quad(vh,new Rect(r.center.x-3,r.yMax-1,6,6),new Color(.035f,.06f,.10f));
            Quad(vh,new Rect(r.center.x-1,r.yMax,2,4),new Color(1,.84f,.43f));
        }
        static Rect Inset(Rect r,float v)=>new Rect(r.x+v,r.y+v,Mathf.Max(0,r.width-v*2),Mathf.Max(0,r.height-v*2));
        static Vector2[] Points(Rect r,float c)=>new[]{new Vector2(r.xMin+c,r.yMin),new Vector2(r.xMax-c,r.yMin),new Vector2(r.xMax,r.yMin+c),new Vector2(r.xMax,r.yMax-c),new Vector2(r.xMax-c,r.yMax),new Vector2(r.xMin+c,r.yMax),new Vector2(r.xMin,r.yMax-c),new Vector2(r.xMin,r.yMin+c)};
        static void Polygon(VertexHelper h,Rect r,float c,Color color)=>Fan(h,Points(r,c),color);
        static void Quad(VertexHelper h,Rect r,Color color)=>Fan(h,new[]{new Vector2(r.xMin,r.yMin),new Vector2(r.xMax,r.yMin),new Vector2(r.xMax,r.yMax),new Vector2(r.xMin,r.yMax)},color);
        static void Fan(VertexHelper h,Vector2[] p,Color color)
        {
            if(p.Length<3)return;int start=h.currentVertCount;foreach(var v in p)h.AddVert(v,color,Vector2.zero);
            for(int i=1;i<p.Length-1;i++)h.AddTriangle(start,start+i,start+i+1);
        }
    }
}

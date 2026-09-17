using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Continuous screen-space liquid geometry: no frame swaps or triangular wipe.
    public sealed class CoffeeScreenTransition : MaskableGraphic
    {
        public const float Duration = 2.4f;
        private float elapsed;
        private static readonly Color Coffee = new Color(.55f, .27f, .095f, 1f);
        private static readonly Color Caramel = new Color(.70f, .39f, .16f, 1f);
        private static readonly Color Crema = new Color(1f, .83f, .52f, 1f);
        public static CoffeeScreenTransition Play(Transform owner)
        {
            var obj = new GameObject("Coffee screen pour", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            obj.transform.SetParent(owner, false);
            var canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true; canvas.sortingOrder = 32760;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var visual = new GameObject("Continuous crema liquid", typeof(RectTransform), typeof(CanvasRenderer));
            visual.transform.SetParent(obj.transform, false);
            var rect = visual.GetComponent<RectTransform>();rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
            rect.offsetMin=rect.offsetMax=Vector2.zero;
            var effect=visual.AddComponent<CoffeeScreenTransition>();effect.raycastTarget=false;
            return effect;
        }
        private void Update()
        {
            if(MiniStageRuntimeState.IsInsideMiniStage)return;
            elapsed += Time.deltaTime;SetVerticesDirty();
            if(elapsed >= Duration) Destroy(transform.parent.gameObject);
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;Vector2 center=r.center;
            float w=r.width,h=r.height,diagonal=Mathf.Sqrt(w*w+h*h)*.5f;
            if(w<=0||h<=0||elapsed>=Duration)return;
            // Flowing, rounded, uneven ribbon. Overlaps the central splash before it is removed.
            if(elapsed<1f)
            {
                float fall=Mathf.SmoothStep(0,1,elapsed/.75f);
                const int strips=64;
                for(int i=0;i<strips;i++)
                {
                    float u0=i/(float)strips,u1=(i+1)/(float)strips;
                    float span=w*(.35f+.18f*fall),x0=center.x+(u0-.5f)*span,x1=center.x+(u1-.5f)*span;
                    float edge0=Mathf.Sin(u0*Mathf.PI),edge1=Mathf.Sin(u1*Mathf.PI);
                    float y0=r.yMax-fall*h*(.50f+.20f*edge0)-h*.035f*Mathf.Sin(u0*24+elapsed*13)*fall;
                    float y1=r.yMax-fall*h*(.50f+.20f*edge1)-h*.035f*Mathf.Sin(u1*24+elapsed*13)*fall;
                    Color c=Color.Lerp(Coffee,Caramel,.5f+.5f*Mathf.Sin(u0*33+elapsed*7));
                    for(int row=0;row<20;row++)
                    {
                        float v0=row/20f,v1=(row+1)/20f;
                        Vector2 a=FlowPoint(center.x,x0,r.yMax+h*.1f,y0,v0,elapsed);
                        Vector2 b=FlowPoint(center.x,x1,r.yMax+h*.1f,y1,v0,elapsed);
                        Vector2 d=FlowPoint(center.x,x0,r.yMax+h*.1f,y0,v1,elapsed);
                        Vector2 e=FlowPoint(center.x,x1,r.yMax+h*.1f,y1,v1,elapsed);
                        Color flow=Color.Lerp(c,Caramel,.16f+.16f*Mathf.Sin(v0*17+elapsed*14+u0*13));
                        if(i==5||i==19||i==43||i==59)flow=Color.Lerp(flow,Crema,.5f);
                        Quad(vh,a,b,e,d,flow);
                    }
                    Vector2 end0=FlowPoint(center.x,x0,r.yMax,y0,1,elapsed),end1=FlowPoint(center.x,x1,r.yMax,y1,1,elapsed);
                    Quad(vh,end0,end1,end1+Vector2.up*12,end0+Vector2.up*12,Crema);
                }
            }
            if(elapsed<.48f)return;
            float outer=Mathf.SmoothStep(0,diagonal*1.3f,Mathf.Clamp01((elapsed-.48f)/.47f));
            float inner=elapsed<=1f?0:Mathf.SmoothStep(0,diagonal*1.4f,(elapsed-1f)/(Duration-1f));
            const int segments=128;
            for(int i=0;i<segments;i++)
            {
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments;
                float rippleA=1+.016f*Mathf.Sin(a*11+elapsed*9),rippleB=1+.016f*Mathf.Sin(b*11+elapsed*9);
                Vector2 da=new Vector2(Mathf.Cos(a),Mathf.Sin(a)),db=new Vector2(Mathf.Cos(b),Mathf.Sin(b));
                float ia=inner*rippleA,ib=inner*rippleB,oa=Mathf.Max(ia,outer*rippleA),ob=Mathf.Max(ib,outer*rippleB);
                Quad(vh,center+da*ia,center+db*ib,center+db*ob,center+da*oa,Color.Lerp(Coffee,Caramel,.4f+.2f*Mathf.Sin(a*9+elapsed*5)));
                float foam=14+6*Mathf.Sin(a*17+elapsed*8);
                if(inner>0)Quad(vh,center+da*ia,center+db*ib,center+db*Mathf.Min(ob,ib+foam),center+da*Mathf.Min(oa,ia+foam),Crema);
                if(outer<diagonal*1.2f)Quad(vh,center+da*Mathf.Max(ia,oa-foam),center+db*Mathf.Max(ib,ob-foam),center+db*ob,center+da*oa,Crema);
                if(i%4==0&&oa>ia+12)
                {
                    float foamRadius=4+3*(.5f+.5f*Mathf.Sin(i*3.7f));
                    float ring=inner>0?ia+foam*1.9f:oa-foam*1.5f;
                    Disc(vh,center+da*ring,foamRadius,Crema);
                }
            }
        }
        private static Vector2 FlowPoint(float center,float x,float top,float bottom,float v,float time)
        {
            float width=1+.08f*Mathf.Sin(v*8-time*6)*v+.10f*v*v;
            return new Vector2(center+(x-center)*width,Mathf.Lerp(top,bottom,v));
        }
        private static void Disc(VertexHelper vh,Vector2 center,float radius,Color color)
        {
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                int n=vh.currentVertCount;vh.AddVert(center,color,Vector2.zero);
                vh.AddVert(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);
                vh.AddVert(center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,color,Vector2.zero);vh.AddTriangle(n,n+1,n+2);
            }
        }
        private static void Quad(VertexHelper vh,Vector2 a,Vector2 b,Vector2 c,Vector2 d,Color color)
        {
            int i=vh.currentVertCount;vh.AddVert(a,color,Vector2.zero);vh.AddVert(b,color,Vector2.zero);
            vh.AddVert(c,color,Vector2.zero);vh.AddVert(d,color,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}

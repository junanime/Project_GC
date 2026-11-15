using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    [RequireComponent(typeof(CanvasRenderer))]
    [DefaultExecutionOrder(100)]
    public sealed class GStarInputFocus : MaskableGraphic
    {
        public TMP_InputField input;
        public Graphic hint;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(input==null||!input.isFocused)return;
            var r=rectTransform.rect;float radius=Mathf.Min(9,r.height*.22f),width=2.5f;
            float pulse=.66f+.34f*(.5f+.5f*Mathf.Sin(Time.unscaledTime*5));
            Color c=new Color(1,.19f,.40f,pulse);
            for(int n=0;n<48;n++)
            {
                Vector2 a=Edge(r,radius,n/48f),b=Edge(r,radius,(n+1)/48f);
                var inset=new Rect(r.x+width,r.y+width,r.width-2*width,r.height-2*width);
                Vector2 d=Edge(inset,Mathf.Max(1,radius-width),n/48f),e=Edge(inset,Mathf.Max(1,radius-width),(n+1)/48f);
                int s=vh.currentVertCount;Add(vh,a,c);Add(vh,b,c);Add(vh,e,c);Add(vh,d,c);
                vh.AddTriangle(s,s+1,s+2);vh.AddTriangle(s,s+2,s+3);
            }
        }
        static Vector2 Edge(Rect r,float radius,float p)
        {
            p=Mathf.Repeat(p,1);int k=Mathf.Min(3,(int)(p*4));float angle=(k*90+(p*4-k)*90)*Mathf.Deg2Rad;
            var center=new Vector2(k==0||k==3?r.xMax-radius:r.xMin+radius,k<2?r.yMax-radius:r.yMin+radius);
            return center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
        }
        static void Add(VertexHelper vh,Vector2 p,Color c){var v=UIVertex.simpleVert;v.position=p;v.color=c;vh.AddVert(v);}
        void LateUpdate()
        {
            if(input==null)return;
            if(hint!=null)hint.enabled=!input.isFocused&&string.IsNullOrEmpty(input.text);
            SetVerticesDirty();
        }
    }
}

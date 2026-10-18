using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Resolution-independent UI shapes; the bookmark has a shallow curved swallowtail.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class BookNavigationGraphic : MaskableGraphic
    {
        public bool gear;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            if(gear)
            {
                const int n=64;float radius=Mathf.Min(r.width,r.height)*.5f;
                for(int i=0;i<n;i++)
                {
                    float a=i*2*Mathf.PI/n,b=(i+1)*2*Mathf.PI/n;
                    float outer=(i%8<4?1:.77f)*radius,next=((i+1)%8<4?1:.77f)*radius;
                    int v=vh.currentVertCount;
                    vh.AddVert(r.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*outer,color,Vector2.zero);
                    vh.AddVert(r.center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*next,color,Vector2.zero);
                    vh.AddVert(r.center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius*.36f,color,Vector2.zero);
                    vh.AddVert(r.center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius*.36f,color,Vector2.zero);
                    vh.AddTriangle(v,v+1,v+2);vh.AddTriangle(v,v+2,v+3);
                }
                return;
            }
            Ribbon(vh,r,new Color(.43f,.20f,.09f));
            Ribbon(vh,Inset(r,2),new Color(1,.75f,.35f));
            Ribbon(vh,Inset(r,4),new Color(1,.97f,.80f));
            Ribbon(vh,Inset(r,6),color);
        }
        static Rect Inset(Rect r,float v)=>new Rect(r.x+v,r.y+v,r.width-2*v,r.height-2*v);
        static void Ribbon(VertexHelper vh,Rect r,Color tint)
        {
            int start=vh.currentVertCount;vh.AddVert(r.center,tint,Vector2.zero);
            var points=new System.Collections.Generic.List<Vector2>{new Vector2(r.x,r.yMax-8),new Vector2(r.x+8,r.yMax),new Vector2(r.xMax-8,r.yMax),new Vector2(r.xMax,r.yMax-8),new Vector2(r.xMax,r.y)};
            for(int i=1;i<=20;i++){float t=i/20f;points.Add(new Vector2(Mathf.Lerp(r.xMax,r.x,t),r.y+Mathf.Sin(t*Mathf.PI)*r.height*.19f));}
            foreach(var p in points)vh.AddVert(p,tint,Vector2.zero);
            for(int i=0;i<points.Count;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%points.Count);
        }
    }
}

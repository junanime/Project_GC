using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Small bevels keep the illustrated keycaps legible without stretching any texture.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class KeyboardKeycapGraphic : MaskableGraphic
    {
        public Color face=new Color(1,.96f,.87f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=GetPixelAdjustedRect();
            Round(vh,r,5,new Color(.25f,.21f,.16f),new Color(.37f,.29f,.20f));
            r=new Rect(r.x+1.2f,r.y+1.2f,r.width-2.4f,r.height-2.4f);
            Round(vh,r,4,Color.Lerp(face,new Color(.52f,.37f,.22f),.38f),Color.Lerp(face,Color.white,.65f));
            r=new Rect(r.x+2.3f,r.y+3.2f,r.width-4.6f,r.height-5.2f);
            Round(vh,r,3,face,Color.Lerp(face,Color.white,.22f));
        }
        static void Round(VertexHelper vh,Rect r,float radius,Color bottom,Color top)
        {
            radius=Mathf.Min(radius,Mathf.Min(r.width,r.height)*.5f);
            int start=vh.currentVertCount;vh.AddVert(r.center,Color.Lerp(bottom,top,.5f),Vector2.zero);
            const int steps=5;const int count=4*(steps+1);
            for(int c=0;c<4;c++) {
                Vector2 center=new Vector2(c<2?r.xMax-radius:r.x+radius,c==0||c==3?r.y+radius:r.yMax-radius);
                for(int i=0;i<=steps;i++) {
                    float a=(-90+c*90+i*90f/steps)*Mathf.Deg2Rad;var p=center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius;
                    vh.AddVert(p,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,p.y)),Vector2.zero);
                }
            }
            for(int i=0;i<count;i++)vh.AddTriangle(start,start+1+i,start+1+(i+1)%count);
        }
    }
}

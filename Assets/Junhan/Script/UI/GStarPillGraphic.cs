using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Live controls use alpha-clean geometry, so stretching never drags paper/adjacent art.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class GStarPillGraphic : MaskableGraphic
    {
        public Color face=new Color(.68f,1,.81f);
        public bool gloss=true;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;
            float line=Mathf.Min(3.3f,r.height*.055f);
            var shadow=r;shadow.y-=line*.6f;
            Round(vh,shadow,new Color(.025f,.035f,.065f),new Color(.025f,.035f,.065f));
            Round(vh,r,new Color(.025f,.035f,.065f),new Color(.025f,.035f,.065f));
            var inner=Inset(r,line);
            Round(vh,inner,Color.Lerp(face,Color.white,.72f),Color.Lerp(face,Color.black,.18f));
            var center=Inset(r,line*2.1f);
            Round(vh,center,Color.Lerp(face,Color.white,.25f),face);
            if(gloss)
            {
                var shine=new Rect(center.x+center.height*.3f,center.yMax-center.height*.23f,center.width-center.height*.6f,center.height*.10f);
                if(shine.width>0)Round(vh,shine,new Color(1,1,1,.34f),new Color(1,1,1,.34f));
            }
        }
        static Rect Inset(Rect r,float n)=>new Rect(r.x+n,r.y+n,Mathf.Max(1,r.width-2*n),Mathf.Max(1,r.height-2*n));
        void Round(VertexHelper vh,Rect r,Color top,Color bottom)
        {
            float rad=Mathf.Min(r.width,r.height)*.5f;int first=vh.currentVertCount;
            Add(vh,r.center,Color.Lerp(bottom,top,.5f));
            for(int corner=0;corner<4;corner++)
            {
                Vector2 c=new Vector2(corner==0||corner==3?r.xMax-rad:r.xMin+rad,corner<2?r.yMax-rad:r.yMin+rad);
                for(int i=0;i<=10;i++)
                {
                    float angle=(corner*90+i*9)*Mathf.Deg2Rad;
                    Vector2 p=c+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*rad;
                    Add(vh,p,Color.Lerp(bottom,top,Mathf.InverseLerp(r.yMin,r.yMax,p.y)));
                }
            }
            int count=44;for(int i=0;i<count;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%count);
        }
        void Add(VertexHelper vh,Vector2 point,Color tint){var v=UIVertex.simpleVert;v.position=point;v.color=tint*color;vh.AddVert(v);}
    }
}

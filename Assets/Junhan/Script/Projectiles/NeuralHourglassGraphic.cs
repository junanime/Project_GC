using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    // Resolution independent mint/cream hourglass; sand is clipped geometrically to the glass.
    public sealed class NeuralHourglassGraphic : MaskableGraphic
    {
        float remaining = 1, phase;
        bool warning, calm;
        static readonly Color Ink = new Color(.15f,.21f,.19f);
        public void SetState(float value, float time, bool ending, bool reduced)
        { remaining = Mathf.Clamp01(value); phase = time; warning = ending; calm = reduced; SetVerticesDirty(); }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            // Authored coordinates: x -50..50, y 0..160 (label at the bottom).
            Box(vh,-49,0,98,24,5,Ink);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(vh,side*32-4,43,8,88,4,Ink);
                Box(vh,side*32-2,44,4,87,2,new Color(.96f,.88f,.69f));
            }
            Poly(vh,Ink,new Vector2(-29,133),new Vector2(29,133),new Vector2(28,117),new Vector2(9,87),new Vector2(8,81),new Vector2(27,53),new Vector2(28,39),new Vector2(-28,39),new Vector2(-27,53),new Vector2(-8,81),new Vector2(-9,87),new Vector2(-28,117));
            Poly(vh,new Color(.68f,.88f,.91f),new Vector2(-25,129),new Vector2(25,129),new Vector2(24,117),new Vector2(5,86),new Vector2(4,80),new Vector2(23,51),new Vector2(24,43),new Vector2(-24,43),new Vector2(-23,51),new Vector2(-4,80),new Vector2(-5,86),new Vector2(-24,117));
            // Horizontal strips conserve the top cone's apparent area as time passes.
            float top = Mathf.Lerp(85,126,Mathf.Sqrt(remaining));
            for(float y=85;y<top;y+=2)
            {
                float y2=Mathf.Min(top,y+2),w1=Mathf.Min(24,4+(y-85)*.54f),w2=Mathf.Min(24,4+(y2-85)*.54f);
                Poly(vh,new Color(1f,.77f,.27f),new Vector2(-w1,y),new Vector2(w1,y),new Vector2(w2,y2),new Vector2(-w2,y2));
            }
            float pile=Mathf.Sqrt(1-remaining);
            Poly(vh,new Color(1f,.74f,.21f),new Vector2(-23*pile,44),new Vector2(23*pile,44),new Vector2(14*pile,49+pile*8),new Vector2(0,45+pile*22),new Vector2(-14*pile,49+pile*8));
            if(remaining>0 && remaining<1)
                for(int i=0;i<7;i++) { float y=80-Mathf.Repeat(phase*38+i*5,31); if(y>45+pile*22)Box(vh,-1,y,2,3,0,new Color(1,.81f,.37f)); }
            Poly(vh,new Color(1,1,1,.7f),new Vector2(-23,123),new Vector2(-20,123),new Vector2(-18,111),new Vector2(-20,112));
            Poly(vh,new Color(1,1,1,.6f),new Vector2(20,48),new Vector2(22,48),new Vector2(18,59),new Vector2(16,60));
            Box(vh,-38,126,76,27,12,Ink); Box(vh,-35,129,70,21,10,new Color(.48f,.8f,.63f));
            Box(vh,-31,140,62,7,4,new Color(.78f,.96f,.79f));
            Box(vh,-36,31,72,16,7,Ink); Box(vh,-33,34,66,10,5,new Color(.94f,.87f,.7f));
            Box(vh,-29,39,58,4,2,new Color(1,.97f,.83f));
            Box(vh,-11,130,22,9,3,new Color(.94f,.96f,.83f));
            bool red=warning&&(calm||Mathf.Sin(phase*19)>0);
            Box(vh,-8,132,16,5,2,red?new Color(.91f,.25f,.27f):new Color(.23f,.42f,.34f));
        }
        Vector3 Point(Vector2 p)
        { var r=rectTransform.rect;return new Vector3(r.xMin+(p.x+50)/100*r.width,r.yMin+p.y/160*r.height); }
        void Poly(VertexHelper vh,Color c,params Vector2[] points)
        {
            int first=vh.currentVertCount; Vector2 center=Vector2.zero;foreach(var p in points)center+=p;center/=points.Length;
            vh.AddVert(Point(center),c,Vector2.zero);foreach(var p in points)vh.AddVert(Point(p),c,Vector2.zero);
            for(int i=0;i<points.Length;i++)vh.AddTriangle(first,first+1+i,first+1+(i+1)%points.Length);
        }
        void Box(VertexHelper vh,float x,float y,float w,float h,float radius,Color c)
        {
            radius=Mathf.Min(radius,Mathf.Min(w,h)/2);int first=vh.currentVertCount;
            vh.AddVert(Point(new Vector2(x+w/2,y+h/2)),c,Vector2.zero);
            const int steps=6;
            for(int corner=0;corner<4;corner++)
            {
                var center=new Vector2(corner<2?x+w-radius:x+radius,corner==0||corner==3?y+h-radius:y+radius);
                for(int i=0;i<=steps;i++) {float a=(90-corner*90-i*90f/steps)*Mathf.Deg2Rad;vh.AddVert(Point(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius),c,Vector2.zero);}
            }
            for(int i=0;i<4*(steps+1);i++)vh.AddTriangle(first,first+i+1,first+1+(i+1)%(4*(steps+1)));
        }
    }
}

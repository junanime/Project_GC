using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Traced from the approved first concept (1536x1024), including its isolated S ridge.
    public static class ColonMapGeometry
    {
        static readonly Vector2[] outer=Smooth(Points(new float[]{55,500,55,260,115,105,260,55,435,77,610,133,760,110,930,50,1120,50,1290,68,1380,110,1470,220,1490,420,1480,660,1390,820,1215,910,1000,960,750,957,540,942,350,910,170,852,80,700}),true);
        static readonly Vector2[] spine=Smooth(Points(new float[]{411,302,475,335,625,350,795,360,940,391,1004,440,1007,497,960,541,855,564,700,562,585,595,535,655,520,740}),false);
        static readonly Vector2[] peninsula=Smooth(Points(new float[]{1490,464,1415,466,1338,474,1300,510}),false);
        static Vector2[] Points(float[] xy){var a=new Vector2[xy.Length/2];for(int i=0;i<a.Length;i++)a[i]=new Vector2(xy[i*2]/1536f,1-xy[i*2+1]/1024f);return a;}
        static Vector2[] Smooth(Vector2[] src,bool closed)
        {
            var r=new List<Vector2>();int length=closed?src.Length:src.Length-1;
            for(int i=0;i<length;i++)for(int k=0;k<5;k++)
            {
                Vector2 a=src[closed?(i+src.Length-1)%src.Length:Mathf.Max(0,i-1)],b=src[i],c=src[(i+1)%src.Length],d=src[closed?(i+2)%src.Length:Mathf.Min(src.Length-1,i+2)];
                float t=k/5f,t2=t*t,t3=t2*t;r.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t2+(-a+3*b-3*c+d)*t3));
            }
            if(!closed)r.Add(src[src.Length-1]);return r.ToArray();
        }
        public static Vector2[] Outer(FiniteStomachMap m){var r=new Vector2[outer.Length];for(int i=0;i<r.Length;i++)r[i]=m.UVToWorld(outer[i]);return r;}
        static bool Inside(Vector2 p){bool inside=false;for(int i=0,j=outer.Length-1;i<outer.Length;j=i++)if((outer[i].y>p.y)!=(outer[j].y>p.y)&&p.x<(outer[j].x-outer[i].x)*(p.y-outer[i].y)/(outer[j].y-outer[i].y)+outer[i].x)inside=!inside;return inside;}
        static float Nearest(FiniteStomachMap m,Vector2[] points,Vector2 p,bool closed,out Vector2 q)
        {
            float best=float.MaxValue;q=p;
            for(int i=0;i<(closed?points.Length:points.Length-1);i++)
            {Vector2 a=m.UVToWorld(points[i]),e=m.UVToWorld(points[(i+1)%points.Length])-a;var at=a+e*Mathf.Clamp01(Vector2.Dot(p-a,e)/Mathf.Max(.000001f,e.sqrMagnitude));float sq=(at-p).sqrMagnitude;if(sq<best){best=sq;q=at;}}
            return Mathf.Sqrt(best);
        }
        public static float Distance(FiniteStomachMap m,Vector2 p,out Vector2 inward,out float along)
        {
            along=0;bool inside=Inside((p-m.WorldRect.min)/m.WorldRect.size);
            float d=Nearest(m,outer,p,true,out var q);inward=(inside?p-q:q-p).normalized;d=inside?d:-d;
            float ridge=Nearest(m,spine,p,false,out var s)-.75f;if(ridge<d){d=ridge;inward=(p-s).normalized;}
            float branch=Nearest(m,peninsula,p,false,out var b)-1.25f;if(branch<d){d=branch;inward=(p-b).normalized;}
            if(inward.sqrMagnitude<.001f)inward=Vector2.down;return d;
        }
        public static bool Contains(FiniteStomachMap m,Vector2 p)=>Distance(m,p,out _,out _)>=0;
    }
}

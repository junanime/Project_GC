using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Approved circulation plan, 2026-11-09. Coordinates are from the map-only
    // reference canvas. The lower hook is deliberately detached from the outlet rim.
    public static class ColonMapGeometry
    {
        static readonly Vector2[] outer=Smooth(Points(new float[]{130,500,110,375,130,255,205,180,340,145,475,150,625,188,750,185,900,148,1050,150,1185,158,1320,165,1390,265,1395,410,1400,545,1405,700,1360,835,1230,885,1030,900,830,890,630,900,440,898,260,880,135,810,105,650}),true);
        static readonly Vector2[] spine=Smooth(Points(new float[]{425,330,490,322,590,347,700,350,810,327,920,300,1000,300,1030,310}),false);
        static readonly Vector2[] left=Smooth(Points(new float[]{105,480,205,525,330,548,460,539,565,515,595,501}),false);
        static readonly Vector2[] right=Smooth(Points(new float[]{1410,435,1310,457,1190,455,1070,464,970,493}),false);
        static readonly Vector2[] hook=Smooth(Points(new float[]{430,748,470,697,585,658,730,627,880,623,1015,638,1125,667,1150,683}),false);
        static readonly Vector2[][] ridges={spine,left,right,hook};
        sealed class PathData { public Vector2[] points,edges; public float[] lengths,along,squared; }
        static readonly Dictionary<Vector2[],PathData> worldPaths=new Dictionary<Vector2[],PathData>();
        static Rect cachedRect;
        static PathData WorldPath(FiniteStomachMap m,Vector2[] source)
        {
            if(cachedRect!=m.WorldRect){worldPaths.Clear();cachedRect=m.WorldRect;}
            if(worldPaths.TryGetValue(source,out var data))return data;
            int count=source.Length;data=new PathData{points=new Vector2[count],edges=new Vector2[count],lengths=new float[count],along=new float[count],squared=new float[count]};
            for(int i=0;i<count;i++)data.points[i]=m.UVToWorld(source[i]);
            for(int i=0;i<count;i++){var edge=data.points[(i+1)%count]-data.points[i];data.edges[i]=edge;data.squared[i]=Mathf.Max(.000001f,edge.sqrMagnitude);data.lengths[i]=edge.magnitude;if(i>0)data.along[i]=data.along[i-1]+data.lengths[i-1];}
            worldPaths.Add(source,data);return data;
        }
        public static IEnumerable<Vector2[]> HabitatRidges(FiniteStomachMap m)
        {
            // Only detached central walls: never attach the manatee to the outer boundary.
            foreach(var points in new[]{spine,hook}){var r=new Vector2[points.Length];for(int i=0;i<r.Length;i++)r[i]=m.UVToWorld(points[i]);yield return r;}
        }
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
        public static Vector2[] CentralRidge(FiniteStomachMap m){var r=new Vector2[spine.Length];for(int i=0;i<r.Length;i++)r[i]=m.UVToWorld(spine[i]);return r;}
        static bool Inside(Vector2 p){bool inside=false;for(int i=0,j=outer.Length-1;i<outer.Length;j=i++)if((outer[i].y>p.y)!=(outer[j].y>p.y)&&p.x<(outer[j].x-outer[i].x)*(p.y-outer[i].y)/(outer[j].y-outer[i].y)+outer[i].x)inside=!inside;return inside;}
        static float Nearest(FiniteStomachMap m,Vector2[] points,Vector2 p,bool closed,out Vector2 q,out float along)
        {
            float best=float.MaxValue;q=p;along=0;var path=WorldPath(m,points);
            for(int i=0;i<(closed?points.Length:points.Length-1);i++)
            {Vector2 a=path.points[i],e=path.edges[i];float t=Mathf.Clamp01(Vector2.Dot(p-a,e)/path.squared[i]);var at=a+e*t;float sq=(at-p).sqrMagnitude;if(sq<best){best=sq;q=at;along=path.along[i]+path.lengths[i]*t;}}
            return Mathf.Sqrt(best);
        }
        public static float Distance(FiniteStomachMap m,Vector2 p,out Vector2 inward,out float along)
            => Distance(m,p,out inward,out along,out _);
        public static float Distance(FiniteStomachMap m,Vector2 p,out Vector2 inward,out float along,out float exterior)
        {
            bool inside=Inside((p-m.WorldRect.min)/m.WorldRect.size);exterior=1;
            float d=Nearest(m,outer,p,true,out var q,out along);inward=(inside?p-q:q-p).normalized;d=inside?d:-d;
            float outerDistance=d;
            foreach(var ridge in ridges){float rd=Nearest(m,ridge,p,false,out var s,out var arc)-.55f;if(rd<d){d=rd;inward=(p-s).normalized;along=arc;}}
            // Blend only visual rim height at attached branches. Collision distance
            // remains exact; a binary height switch would leave triangular seams.
            exterior=1-Mathf.SmoothStep(0,1,(outerDistance-d)/4f);
            if(inward.sqrMagnitude<.001f)inward=Vector2.down;return d;
        }
        public static bool Contains(FiniteStomachMap m,Vector2 p)=>Distance(m,p,out _,out _)>=0;
        // Rendering blends wall directions at ridge/rim junctions. Physical
        // distance and its nearest-surface normal are deliberately left unchanged.
        public static void VisualDirections(FiniteStomachMap m,Vector2 p,out Vector2 normal,out Vector2 phase,out float exterior)
        {
            bool inside=Inside((p-m.WorldRect.min)/m.WorldRect.size);
            float nearest=Nearest(m,outer,p,true,out var q,out var arc);
            normal=(inside?p-q:q-p).normalized;nearest=inside?nearest:-nearest;
            float angle=arc/24f*Mathf.PI*2;phase=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            float total=1;exterior=1;const float blend=1.4f;
            foreach(var ridge in ridges)
            {
                float d=Nearest(m,ridge,p,false,out q,out arc)-.55f;
                if(d<nearest)
                {
                    float scale=Mathf.Exp((d-nearest)/blend);
                    total*=scale;normal*=scale;phase*=scale;exterior*=scale;nearest=d;
                }
                float weight=Mathf.Exp((nearest-d)/blend);angle=arc/24f*Mathf.PI*2;
                normal+=(p-q).normalized*weight;phase+=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*weight;total+=weight;
            }
            normal=normal.normalized;phase/=total;exterior/=total;
        }
    }
}

using UnityEngine;

namespace Vampire
{
    public enum OralZone { None, Tongue, Teeth, Chasm }

    // All collision, animation and spawn placement share this analytical description.
    public static class OralMapGeometry
    {
        public const int ToothCount = 18; public const float ToothScale = 26.4f;
        static Rect cachedRect; static Vector2[] cachedCenters;
        static float southWallWidth=-1;
        public static float SouthWallWidth {get {if(southWallWidth<0){var definition=Resources.Load<StomachMapDefinition>("ColonMap/Stage2");southWallWidth=definition!=null?definition.wallWidth:4.5f;}return southWallWidth;}}
        public static float SouthFloor(FiniteStomachMap map)=>map.WorldRect.yMin+SouthWallWidth;
        public static Vector2 TongueCenter(FiniteStomachMap map) => map.UVToWorld(new Vector2(.5f,.44f));
        public static Vector2 TongueRadii(FiniteStomachMap map) => Vector2.Scale(map.WorldRect.size,new Vector2(.23f,.23f));
        public static Vector2 ArchCenter(FiniteStomachMap map) => map.UVToWorld(new Vector2(.5f,.45f));
        public static Vector2 OuterRadii(FiniteStomachMap map) => Vector2.Scale(map.WorldRect.size,new Vector2(.485f,.50f));
        public static Vector2 InnerRadii(FiniteStomachMap map) => Vector2.Scale(map.WorldRect.size,new Vector2(.393f,.405f));
        public static float ArchBottom(FiniteStomachMap map) => map.WorldRect.yMin+map.WorldRect.height*.15f;
        public static Vector2[] ToothCenters(FiniteStomachMap map)
        {
            if(cachedCenters!=null&&cachedRect==map.WorldRect)return cachedCenters;
            var result=new Vector2[ToothCount]; var center=ArchCenter(map);
            var axes=Vector2.Scale(map.WorldRect.size,new Vector2(.385f,.355f));
            for(int i=0;i<result.Length;i++) {float a=Mathf.Lerp(-25,205,i/(float)(result.Length-1))*Mathf.Deg2Rad;
                result[i]=center+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),axes);}
            cachedRect=map.WorldRect;cachedCenters=result;return result;
        }
        public static Vector2 TonguePoint(FiniteStomachMap map,Vector2 q) {
            var center=TongueCenter(map);var radii=TongueRadii(map);
            if(q.y>=0)return center+Vector2.Scale(q,radii);
            var square=q*(q.magnitude/Mathf.Max(.00001f,Mathf.Max(Mathf.Abs(q.x),Mathf.Abs(q.y))));
            return center+new Vector2(square.x*radii.x,square.y*(center.y-SouthFloor(map)));
        }
        public static float TongueDistance(FiniteStomachMap map,Vector2 p,out Vector2 inward) {
            var center=TongueCenter(map);var radii=TongueRadii(map);
            if(p.y>=center.y)return EllipseDistance(p,center,radii,out inward);
            float side=radii.x-Mathf.Abs(p.x-center.x),bottom=p.y-SouthFloor(map);
            inward=side<bottom?(p.x>center.x?Vector2.left:Vector2.right):Vector2.up;
            return Mathf.Min(side,bottom);
        }
        public static float TeethDistance(FiniteStomachMap map,Vector2 p,out Vector2 inward)
        {
            float best=float.NegativeInfinity; inward=Vector2.up;
            foreach(var c in ToothCenters(map)) {
                float radius=ToothScale*.53f,centerDistance=Vector2.Distance(p,c);
                if(centerDistance-radius>Mathf.Max(0,-best))continue;
                var d=ToothDistance(p-c,out var n);
                if(d>best){best=d;inward=n;}
            }
            return best;
        }
        // Shared crown contour: navigation and treatment trace the same visible enamel edge.
        public static Vector2[] Crown;
        public static float ToothDistance(Vector2 point,out Vector2 inward) {
            if(Crown==null)return EllipseDistance(point,Vector2.zero,new Vector2(8.6f,6.5f),out inward);
            bool inside=false;float best=float.PositiveInfinity;Vector2 closest=point;
            for(int i=0,j=Crown.Length-1;i<Crown.Length;j=i++) {
                var a=Crown[j];var b=Crown[i];var edge=b-a;
                if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                var q=a+edge*Mathf.Clamp01(Vector2.Dot(point-a,edge)/Mathf.Max(.00001f,edge.sqrMagnitude));
                float d=(point-q).sqrMagnitude;if(d<best){best=d;closest=q;}
            }
            inward=(inside?point-closest:closest-point).normalized;
            return Mathf.Sqrt(best)*(inside?1:-1);
        }
        public static float Distance(FiniteStomachMap map,Vector2 p,out Vector2 inward)
        {
            float tongue=TongueDistance(map,p,out inward);
            if(tongue>=0)return tongue;
            float teeth=TeethDistance(map,p,out var n);
            if(teeth>tongue){inward=n;return teeth;}return tongue;
        }
        public static OralZone ZoneAt(FiniteStomachMap map,Vector2 p)
        {
            if(TongueDistance(map,p,out _)>=0)return OralZone.Tongue;
            if(TeethDistance(map,p,out _)>=0)return OralZone.Teeth;
            return OralZone.Chasm;
        }
        public static bool Contains(FiniteStomachMap map,Vector2 p)=>Distance(map,p,out _)>=0;
        public static Vector2 Clamp(FiniteStomachMap map,Vector2 p,float radius=.25f,OralZone preferredZone=OralZone.None)
        {
            radius=Mathf.Clamp(radius,.1f,1.6f);
            if(preferredZone==OralZone.None||preferredZone==OralZone.Chasm)
                preferredZone=TongueDistance(map,p,out _)>TeethDistance(map,p,out _)?OralZone.Tongue:OralZone.Teeth;
            for(int i=0;i<24;i++)
            {
                float d=preferredZone==OralZone.Tongue?TongueDistance(map,p,out var n):TeethDistance(map,p,out n);
                if(d>=radius-.002f)return p;
                p+=n*(radius-d+.015f);
            }
            if(preferredZone==OralZone.Tongue)return TongueCenter(map);
            var teeth=ToothCenters(map);float best=float.MaxValue;Vector2 result=teeth[0];
            foreach(var t in teeth)if((t-p).sqrMagnitude<best){best=(t-p).sqrMagnitude;result=t;}
            return result;
        }
        public static bool TryJump(FiniteStomachMap map,Vector2 from,Vector2 direction,out Vector2 landing)
        {
            landing=from;
            var zone=ZoneAt(map,from);
            if(direction.sqrMagnitude<.01f||zone==OralZone.Chasm)return false;
            direction.Normalize();
            float edge=zone==OralZone.Tongue?TongueDistance(map,from,out var n):TeethDistance(map,from,out n);
            if(edge>1.8f||Vector2.Dot(direction,-n)<.55f)return false;
            var target=zone==OralZone.Tongue?OralZone.Teeth:OralZone.Tongue;
            for(float d=1;d<15;d+=.15f)
            {
                Vector2 p=from+direction*d;
                float clearance=target==OralZone.Teeth?TeethDistance(map,p,out _):TongueDistance(map,p,out _);
                if(clearance<.7f)continue;
                landing=p;return true;
            }
            return false;
        }
        public static Vector2[] Outer(FiniteStomachMap map)
        {
            var result=new Vector2[64];for(int i=0;i<result.Length;i++){float a=i*Mathf.PI*2/result.Length;
                result[i]=ArchCenter(map)+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),OuterRadii(map));}return result;
        }
        static float EllipseDistance(Vector2 p,Vector2 center,Vector2 axes,out Vector2 inward)
        {
            var q=p-center;float k=Mathf.Sqrt(q.x*q.x/(axes.x*axes.x)+q.y*q.y/(axes.y*axes.y));
            if(k<.00001f){inward=Vector2.up;return Mathf.Min(axes.x,axes.y);}
            // Signed radial distance gives a stable field; gradient directs safe projection.
            Vector2 edge=q/k,normal=new Vector2(edge.x/(axes.x*axes.x),edge.y/(axes.y*axes.y)).normalized;
            inward=-normal;return Vector2.Dot(edge-q,normal);
        }
    }
}

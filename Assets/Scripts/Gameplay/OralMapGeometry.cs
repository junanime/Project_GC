using UnityEngine;

namespace Vampire
{
    public enum OralZone { None, Tongue, Teeth, Chasm }
    public enum OralToothKind { Molar, Incisor, Canine }

    // Authored against the approved 1403 x 1121 map. Navigation, warnings and
    // damage share these surfaces; enamel sidewalls and gums are never floor.
    public static class OralMapGeometry
    {
        public const int ToothCount = 14;
        public const float ToothScale = 17.6f;
        public const float ToothFootRadius = .12f;
        public const float JawSeamHalfWidth = .70f;
        const int SamplesPerTooth = 24;
        static Rect cachedRect;
        static Vector2[] cachedCenters, band, centerSamples;
        static Vector2[][] walkPolygons, damagePolygons;
        static Rect[] damageBounds;
        static float[] cachedAngles;
        // Each pair is a cross-section of the flat top: rear right, front,
        // rear left. Odd entries are tooth centers; even entries are seams.
        static readonly Vector2[] OuterPixels = {
            new Vector2(1180,690),new Vector2(1190,630),new Vector2(1153,574),
            new Vector2(1145,528),new Vector2(1090,482),new Vector2(1080,444),
            new Vector2(1033,414),new Vector2(1004,377),new Vector2(952,357),
            new Vector2(914,320),new Vector2(882,322),new Vector2(830,296),
            new Vector2(791,296),new Vector2(746,282),new Vector2(702,292),
            new Vector2(660,282),new Vector2(611,294),new Vector2(570,296),
            new Vector2(523,319),new Vector2(482,322),new Vector2(447,349),
            new Vector2(400,373),new Vector2(364,413),new Vector2(333,421),
            new Vector2(310,478),new Vector2(252,515),new Vector2(231,577),
            new Vector2(208,635),new Vector2(222,707)
        };
        static readonly Vector2[] InnerPixels = {
            new Vector2(1108,690),new Vector2(1085,630),new Vector2(1080,586),
            new Vector2(1054,535),new Vector2(1047,500),new Vector2(1008,458),
            new Vector2(995,438),new Vector2(973,425),new Vector2(947,383),
            new Vector2(914,337),new Vector2(882,337),new Vector2(830,312),
            new Vector2(791,311),new Vector2(746,296),new Vector2(701,304),
            new Vector2(660,296),new Vector2(611,307),new Vector2(570,308),
            new Vector2(523,333),new Vector2(482,338),new Vector2(457,387),
            new Vector2(420,423),new Vector2(402,458),new Vector2(384,478),
            new Vector2(358,493),new Vector2(336,550),new Vector2(316,575),
            new Vector2(324,649),new Vector2(283,692)
        };
        public static Vector2 PixelToWorld(FiniteStomachMap map,Vector2 pixel) => map.UVToWorld(new Vector2(pixel.x/1403f,1-pixel.y/1121f));
        public static Vector2 WorldToPixel(FiniteStomachMap map,Vector2 p) => new Vector2((p.x-map.WorldRect.xMin)/map.WorldRect.width*1403,(1-(p.y-map.WorldRect.yMin)/map.WorldRect.height)*1121);
        public static OralToothKind ToothKind(int index) {
            int fromBack=Mathf.Min(index,ToothCount-1-index);
            return fromBack>=5?OralToothKind.Incisor:fromBack==4?OralToothKind.Canine:OralToothKind.Molar;
        }
        public static bool CanJumpToTooth(int index) => index>=0 && index<ToothCount && ToothKind(index)==OralToothKind.Molar;
        public static float ToothAngle(int index) => cachedAngles!=null?cachedAngles[index]:0;
        public static int ToothDepth(int index) => Mathf.Abs(index*2-(ToothCount-1));
        public static Vector2 Rotate(Vector2 p,float degrees) {
            float a=degrees*Mathf.Deg2Rad,c=Mathf.Cos(a),s=Mathf.Sin(a);
            return new Vector2(p.x*c-p.y*s,p.x*s+p.y*c);
        }
        static float southWallWidth=-1;
        public static float SouthWallWidth {get {if(southWallWidth<0){var definition=Resources.Load<StomachMapDefinition>("ColonMap/Stage2");southWallWidth=definition!=null?definition.wallWidth:4.5f;}return southWallWidth;}}
        public static float SouthFloor(FiniteStomachMap map) => PixelToWorld(map,new Vector2(710,890)).y;
        public static Vector2 TongueCenter(FiniteStomachMap map) => PixelToWorld(map,new Vector2(710,690));
        public static Vector2 TongueRadii(FiniteStomachMap map) => Vector2.Scale(map.WorldRect.size,new Vector2(275f/1403,215f/1121));
        public static Vector2 ArchCenter(FiniteStomachMap map) => PixelToWorld(map,new Vector2(701.5f,650));
        public static Vector2 OuterRadii(FiniteStomachMap map) => Vector2.Scale(map.WorldRect.size,new Vector2(.385f,.355f));
        public static Vector2 InnerRadii(FiniteStomachMap map) => Vector2.Scale(map.WorldRect.size,new Vector2(.22f,.20f));
        public static float ArchBottom(FiniteStomachMap map) => PixelToWorld(map,new Vector2(701.5f,720)).y;
        static Vector2 Spline(Vector2[] points,float toothT) {
            float t=Mathf.Clamp(toothT*2,0,points.Length-1);
            int i=Mathf.Min(Mathf.FloorToInt(t),points.Length-2);float u=t-i;
            var a=points[Mathf.Max(0,i-1)];var b=points[i];var c=points[i+1];var d=points[Mathf.Min(points.Length-1,i+2)];
            return .5f*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
        }
        static Vector2 Section(FiniteStomachMap map,float t,float across) => PixelToWorld(map,Vector2.Lerp(Spline(OuterPixels,t),Spline(InnerPixels,t),across));
        static Vector2[] Surface(FiniteStomachMap map,float start,float end,float edgeInset=0) {
            int samples=Mathf.Max(8,Mathf.CeilToInt((end-start)*SamplesPerTooth));
            var result=new Vector2[(samples+1)*2];
            for(int i=0;i<=samples;i++) {
                float t=Mathf.Lerp(start,end,i/(float)samples);
                var a=Section(map,t,0);var b=Section(map,t,1);
                float inset=Mathf.Min(edgeInset,Vector2.Distance(a,b)*.12f);
                var n=(b-a).normalized;
                result[i]=a+n*inset;result[result.Length-1-i]=b-n*inset;
            }
            return result;
        }
        static float InsetAlong(FiniteStomachMap map,float start,float direction,float distance) {
            float t=start,total=0;var p=Section(map,t,.5f);
            for(int i=0;i<120&&total<distance;i++) {
                float next=Mathf.Clamp(t+direction/120f,0,ToothCount);
                var q=Section(map,next,.5f);float step=Vector2.Distance(p,q);
                if(total+step>=distance)return Mathf.Lerp(t,next,(distance-total)/Mathf.Max(.0001f,step));
                if(next==t)break;total+=step;t=next;p=q;
            }
            return t;
        }
        static void Ensure(FiniteStomachMap map) {
            if(cachedCenters!=null&&cachedRect==map.WorldRect)return;
            cachedRect=map.WorldRect;cachedCenters=new Vector2[ToothCount];cachedAngles=new float[ToothCount];
            walkPolygons=new Vector2[ToothCount][];damagePolygons=new Vector2[ToothCount][];damageBounds=new Rect[ToothCount];
            band=Surface(map,0,ToothCount);
            centerSamples=new Vector2[ToothCount*SamplesPerTooth+1];
            for(int i=0;i<centerSamples.Length;i++)centerSamples[i]=Section(map,i/(float)SamplesPerTooth,.5f);
            for(int i=0;i<ToothCount;i++) {
                cachedCenters[i]=Section(map,i+.5f,.5f);
                var inward=Section(map,i+.5f,1)-Section(map,i+.5f,0);
                cachedAngles[i]=Mathf.Atan2(-inward.y,-inward.x)*Mathf.Rad2Deg-90;
                walkPolygons[i]=Surface(map,i,i+1);
                float a=InsetAlong(map,i,1,i==0?.18f:JawSeamHalfWidth);
                float b=InsetAlong(map,i+1,-1,i==ToothCount-1?.18f:JawSeamHalfWidth);
                // Even very small maps retain a nonempty tooth-local danger zone.
                a=Mathf.Min(a,i+.42f);b=Mathf.Max(b,i+.58f);
                damagePolygons[i]=Surface(map,a,b,.035f);
                var min=damagePolygons[i][0];var max=min;
                foreach(var p in damagePolygons[i]){min=Vector2.Min(min,p);max=Vector2.Max(max,p);}
                damageBounds[i]=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
            }
        }
        public static Vector2[] ToothCenters(FiniteStomachMap map) {Ensure(map);return cachedCenters;}
        public static Vector2 ToothWalkCenter(FiniteStomachMap map,int index) {Ensure(map);return cachedCenters[Mathf.Clamp(index,0,ToothCount-1)];}
        public static Vector2[] ToothWalkPolygon(FiniteStomachMap map,int index) {Ensure(map);return walkPolygons[Mathf.Clamp(index,0,ToothCount-1)];}
        public static Vector2[] ToothDamagePolygon(FiniteStomachMap map,int index) {Ensure(map);return damagePolygons[Mathf.Clamp(index,0,ToothCount-1)];}
        public static Vector2 ToothSafeGap(FiniteStomachMap map,int indexBetween) {Ensure(map);return Section(map,Mathf.Clamp(indexBetween,0,ToothCount-2)+1,.5f);}
        static float ClosestT(FiniteStomachMap map,Vector2 p) {
            Ensure(map);float best=float.PositiveInfinity,t=0;
            for(int i=0;i<centerSamples.Length-1;i++) {
                var a=centerSamples[i];var v=centerSamples[i+1]-a;
                float f=Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(.000001f,v.sqrMagnitude));
                float d=(p-a-v*f).sqrMagnitude;
                if(d<best){best=d;t=(i+f)/SamplesPerTooth;}
            }
            return t;
        }
        public static int NearestTooth(FiniteStomachMap map,Vector2 p) => Mathf.Clamp(Mathf.FloorToInt(ClosestT(map,p)),0,ToothCount-1);
        public static bool IsJawDanger(FiniteStomachMap map,Vector2 p,float radius=0) {
            Ensure(map);
            radius=Mathf.Max(0,radius);
            for(int i=0;i<damagePolygons.Length;i++) {
                var bounds=damageBounds[i];
                if(p.x<bounds.xMin-radius||p.x>bounds.xMax+radius||p.y<bounds.yMin-radius||p.y>bounds.yMax+radius)continue;
                if(PolygonDistance(p,damagePolygons[i],out _)>=-radius)return true;
            }
            return false;
        }
        public static bool TrySlideToTongue(FiniteStomachMap map,Vector2 from,Vector2 attempted,out Vector2 landing) {
            landing=attempted;
            if((attempted-from).sqrMagnitude<.0000001f||TeethDistance(map,from,out _) < -.25f||TeethDistance(map,attempted,out _)>ToothFootRadius+.025f)return false;
            float t=ClosestT(map,from);int tooth=Mathf.Clamp(Mathf.FloorToInt(t),0,ToothCount-1);
            if(CanJumpToTooth(tooth))return false;
            var towardInner=(Section(map,t,1)-Section(map,t,0)).normalized;
            if(Vector2.Dot(attempted-from,towardInner)<=.00001f)return false;
            // The outer edge remains a hard boundary; only the tongue-facing slope slides.
            if(Vector2.Dot(attempted-Section(map,t,.5f),towardInner)<=0)return false;
            landing=Clamp(map,attempted+((TongueCenter(map)-attempted).normalized*1.5f),1f,OralZone.Tongue);
            return true;
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
        public static float TeethDistance(FiniteStomachMap map,Vector2 p,out Vector2 inward) {Ensure(map);return PolygonDistance(p,band,out inward);}
        // Kept for old cavity-art callers. Navigation never samples opaque sprite sides.
        public static Vector2[] Crown;
        public static readonly Vector2[][] Crowns=new Vector2[3][];
        public static Vector2[] ToothContour(int index) {
            if(walkPolygons==null)return Crowns[(int)ToothKind(index)]??Crown;
            var source=walkPolygons[index];var result=new Vector2[source.Length];
            for(int i=0;i<result.Length;i++)result[i]=source[i]-cachedCenters[index];return result;
        }
        public static float ToothDistance(Vector2 point,out Vector2 inward,Vector2[] contour=null) {
            contour=contour??Crown;
            return contour!=null?PolygonDistance(point,contour,out inward):EllipseDistance(point,Vector2.zero,new Vector2(5.7f,4.3f),out inward);
        }
        static float PolygonDistance(Vector2 point,Vector2[] contour,out Vector2 inward) {
            bool inside=false;float best=float.PositiveInfinity;Vector2 closest=point;
            for(int i=0,j=contour.Length-1;i<contour.Length;j=i++) {
                var a=contour[j];var b=contour[i];var edge=b-a;
                if((a.y>point.y)!=(b.y>point.y)&&point.x<(b.x-a.x)*(point.y-a.y)/(b.y-a.y)+a.x)inside=!inside;
                var q=a+edge*Mathf.Clamp01(Vector2.Dot(point-a,edge)/Mathf.Max(.000001f,edge.sqrMagnitude));
                float d=(point-q).sqrMagnitude;if(d<best){best=d;closest=q;}
            }
            inward=(inside?point-closest:closest-point).normalized;
            if(inward.sqrMagnitude<.00001f)inward=Vector2.up;
            return Mathf.Sqrt(best)*(inside?1:-1);
        }
        public static float Distance(FiniteStomachMap map,Vector2 p,out Vector2 inward) {
            float tongue=TongueDistance(map,p,out inward);if(tongue>=0)return tongue;
            float teeth=TeethDistance(map,p,out var n);if(teeth>tongue){inward=n;return teeth;}return tongue;
        }
        public static OralZone ZoneAt(FiniteStomachMap map,Vector2 p) {
            if(TongueDistance(map,p,out _)>=0)return OralZone.Tongue;
            if(TeethDistance(map,p,out _)>=0)return OralZone.Teeth;return OralZone.Chasm;
        }
        public static bool Contains(FiniteStomachMap map,Vector2 p) => Distance(map,p,out _)>=0;
        public static Vector2 Clamp(FiniteStomachMap map,Vector2 p,float radius=.25f,OralZone preferredZone=OralZone.None) {
            radius=Mathf.Clamp(radius,.1f,1.6f);
            if(preferredZone==OralZone.None||preferredZone==OralZone.Chasm)
                preferredZone=TongueDistance(map,p,out _)>TeethDistance(map,p,out _)?OralZone.Tongue:OralZone.Teeth;
            // Teeth use the actor's ground-contact point. A full sprite radius
            // would make the intentionally narrow front ridge impossible to walk.
            if(preferredZone==OralZone.Teeth)radius=Mathf.Min(radius,ToothFootRadius);
            for(int i=0;i<24;i++) {
                float d=preferredZone==OralZone.Tongue?TongueDistance(map,p,out var n):TeethDistance(map,p,out n);
                if(d>=radius-.002f)return p;p+=n*(radius-d+.008f);
            }
            if(preferredZone==OralZone.Tongue)return TongueCenter(map);
            return Section(map,ClosestT(map,p),.5f);
        }
        public static bool TryJump(FiniteStomachMap map,Vector2 from,Vector2 direction,out Vector2 landing) {
            landing=from;var zone=ZoneAt(map,from);
            if(direction.sqrMagnitude<.01f||zone==OralZone.Chasm)return false;
            direction.Normalize();
            float edge=zone==OralZone.Tongue?TongueDistance(map,from,out var n):TeethDistance(map,from,out n);
            if(edge>1.8f||Vector2.Dot(direction,-n)<.55f)return false;
            var target=zone==OralZone.Tongue?OralZone.Teeth:OralZone.Tongue;
            for(float d=1;d<20;d+=.10f) {
                var p=from+direction*d;
                float clearance=target==OralZone.Teeth?TeethDistance(map,p,out _):TongueDistance(map,p,out _);
                if(clearance<.7f)continue;
                if(target==OralZone.Teeth&&!CanJumpToTooth(NearestTooth(map,p)))return false;
                landing=p;return true;
            }
            return false;
        }
        public static Vector2[] Outer(FiniteStomachMap map) {
            var result=new Vector2[64];for(int i=0;i<result.Length;i++){float a=i*Mathf.PI*2/result.Length;
                result[i]=ArchCenter(map)+Vector2.Scale(new Vector2(Mathf.Cos(a),Mathf.Sin(a)),OuterRadii(map));}return result;
        }
        static float EllipseDistance(Vector2 p,Vector2 center,Vector2 axes,out Vector2 inward) {
            var q=p-center;float k=Mathf.Sqrt(q.x*q.x/(axes.x*axes.x)+q.y*q.y/(axes.y*axes.y));
            if(k<.00001f){inward=Vector2.up;return Mathf.Min(axes.x,axes.y);}
            Vector2 edge=q/k,normal=new Vector2(edge.x/(axes.x*axes.x),edge.y/(axes.y*axes.y)).normalized;
            inward=-normal;return Vector2.Dot(edge-q,normal);
        }
    }
}

using UnityEngine;
namespace Vampire
{
    // Shared metric doorway geometry for the terrain, navigation and seal anchors.
    // Local y always points out of the arena; the lower outlet is the inlet inverted.
    public static class ColonOpeningGeometry
    {
        public const float HalfArchWidth = 1.28f;
        public const float ArchHeight = 1.34f;
        public const float ArchBase = -.20f;
        public const float StemEnd = -1.05f;
        public const float TubeRadius = .52f;
        public static Vector2 Center(FiniteStomachMap map, bool upper)
        { var s=map.OpeningShape(upper);return map.UVToWorld(new Vector2(s.x,s.y)); }
        public static Vector2 Radii(FiniteStomachMap map,bool upper)
        {var s=map.OpeningShape(upper);return Vector2.Scale(new Vector2(s.z,s.w),map.WorldRect.size);}
        public static Vector2 Outward(bool upper)=>upper?Vector2.up:Vector2.down;
        public static Vector2 Local(FiniteStomachMap map,Vector2 p,bool upper)
        {var r=Radii(map,upper);var v=p-Center(map,upper);return new Vector2(v.x/r.x,v.y/r.y*(upper?1:-1));}
        public static Vector2 World(FiniteStomachMap map,Vector2 local,bool upper)
        {var r=Radii(map,upper);return Center(map,upper)+new Vector2(local.x*r.x,local.y*r.y*(upper?1:-1));}
        public static Vector2 SealCenter(FiniteStomachMap map,bool upper=false)=>World(map,new Vector2(0,-.04f),upper);
        public static Vector2 ThroneCenter(FiniteStomachMap map)
        {
            var approach=Center(map,false)-Outward(false)*(Radii(map,false).y+3.4f);
            return map.ClampToFloor(approach,1.85f);
        }
        public static Vector2 ChainAnchor(FiniteStomachMap map,int index,bool upper=false)
        {
            // Both pairs are buried in the solid jambs, away from the arena silhouette.
            float x=(index==0||index==2)?-HalfArchWidth:HalfArchWidth;
            float y=index<2?-.62f:.30f;
            return World(map,new Vector2(x,y),upper);
        }
        public static float WallClearance(FiniteStomachMap map,Vector2 p,bool upper,out Vector2 normal)
        {
            var r=Radii(map,upper);var q=Local(map,p,upper);Vector2 nearest;
            if(q.y<ArchBase)nearest=new Vector2(Mathf.Sign(q.x)*HalfArchWidth,Mathf.Max(StemEnd,q.y));
            else
            {
                float angle=Mathf.Atan2((q.y-ArchBase)/ArchHeight,q.x/HalfArchWidth);
                nearest=new Vector2(Mathf.Cos(angle)*HalfArchWidth,ArchBase+Mathf.Sin(angle)*ArchHeight);
            }
            var center=World(map,nearest,upper);var delta=p-center;
            normal=delta.sqrMagnitude<.00001f?Vector2.right:delta.normalized;
            return delta.magnitude-Mathf.Min(r.x,r.y)*TubeRadius;
        }
        public static float ApproachClearance(FiniteStomachMap map,Vector2 p,bool upper,out Vector2 normal)
        {
            var r=Radii(map,upper);var q=Local(map,p,upper);
            float flare=Mathf.Clamp01((-1.05f-q.y)/1.25f);
            float width=.745f+.38f*flare*flare*(3-2*flare);
            float slope=-.38f*6*flare*(1-flare)/1.25f;
            float side=r.x*width-Mathf.Abs(q.x*r.x);
            float back=(-.94f-q.y)*r.y;
            if(side<back){normal=new Vector2(q.x>0?-1:1,slope*r.x/r.y*(upper?1:-1)).normalized;return side;}
            normal=-Outward(upper);return back;
        }
        public static float AdjustFloor(FiniteStomachMap map,Vector2 p,float floor,out Vector2 normal,Vector2 floorNormal)
        {
            normal=floorNormal;
            for(int i=0;i<2;i++)
            {
                bool upper=i==0;var q=Local(map,p,upper);
                if(Mathf.Abs(q.x)>2f||q.y>1.85f||q.y<-2.7f)continue;
                float approach=ApproachClearance(map,p,upper,out var an);
                if(approach>floor){floor=approach;normal=an;}
                float wall=WallClearance(map,p,upper,out var wn);
                if(wall<floor){floor=wall;normal=wn;}
            }
            return floor;
        }
    }
}

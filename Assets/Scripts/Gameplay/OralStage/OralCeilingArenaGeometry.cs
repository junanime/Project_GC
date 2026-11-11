using UnityEngine;
namespace Vampire
{
    // C02's compact, rounded-square floor. Its 139.9 square units are two thirds
    // of the mini-stage's approximately 209.8 square-unit clipped walkable floor.
    public static class OralCeilingArenaGeometry
    {
        public static readonly Vector2 HalfExtents=new Vector2(6.12f,6.165f);
        public static Vector2 Center(FiniteStomachMap map)=>OralMapGeometry.TongueCenter(map);
        public static Vector2 Point(FiniteStomachMap map,Vector2 normalized)=>Clamp(map,Center(map)+Vector2.Scale(normalized,HalfExtents));
        public static bool Contains(FiniteStomachMap map,Vector2 point,float inset=0)
        {
            var half=Inset(inset);var q=point-Center(map);
            float x=q.x/half.x,y=q.y/half.y;return x*x*x*x+y*y*y*y<=1.00001f;
        }
        public static Vector2 Clamp(FiniteStomachMap map,Vector2 point,float inset=0)
        {
            var half=Inset(inset);var c=Center(map);var q=point-c;
            float x=q.x/half.x,y=q.y/half.y,n=x*x*x*x+y*y*y*y;
            return n<=1?point:c+q/Mathf.Pow(n,.25f);
        }
        static Vector2 Inset(float inset)=>new Vector2(Mathf.Max(.1f,HalfExtents.x-Mathf.Max(0,inset)),Mathf.Max(.1f,HalfExtents.y-Mathf.Max(0,inset)));
    }
}

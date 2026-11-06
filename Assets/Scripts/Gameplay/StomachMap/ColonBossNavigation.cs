using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // A separate flow field keeps the ordinary-monster routing unchanged. Its cells leave
    // enough clearance for the widest optional boss, rather than a small monster's body.
    public sealed class ColonBossNavigation : MonoBehaviour
    {
        const float Cell=1.5f,BodyRadius=1.41f,GridMargin=.65f;
        const int MaxFields=16;
        sealed class FlowField { public int[] distance; public long used; }
        FiniteStomachMap map;StomachMapDefinition definition;
        int w,h;long sequence;bool[] walk;
        readonly Dictionary<int,FlowField> fields=new Dictionary<int,FlowField>();
        readonly Queue<int> queue=new Queue<int>();
        float Limit=>map.ClimbLimit+BodyRadius+.05f;
        public static Vector2 Toward(Vector2 from,Vector2 target)
        {
            var m=FiniteStomachMap.Instance;
            if(m==null||!m.IsColon||!m.AppliesAt(from))return(target-from).normalized;
            var nav=m.GetComponent<ColonBossNavigation>()??m.gameObject.AddComponent<ColonBossNavigation>();
            return nav.Direction(m,from,target);
        }
        Vector2 Point(int i)=>map.WorldRect.min+new Vector2((i%w+.5f)*Cell,(i/w+.5f)*Cell);
        int Index(Vector2 p)
        {
            var q=(p-map.WorldRect.min)/Cell;
            return Mathf.Clamp((int)q.y,0,h-1)*w+Mathf.Clamp((int)q.x,0,w-1);
        }
        bool SegmentClear(Vector2 a,Vector2 b)
        {
            int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/.65f));
            for(int i=1;i<=steps;i++)if(map.NavigationDistance(Vector2.Lerp(a,b,i/(float)steps),out _,true)<Limit)return false;
            return true;
        }
        int Goal(Vector2 target)
        {
            int at=Index(map.ClampToField(target,Limit+GridMargin));
            if(walk[at])return at;
            int best=-1;float nearest=float.MaxValue;
            for(int dy=-5;dy<=5;dy++)for(int dx=-5;dx<=5;dx++)
            {
                int x=at%w+dx,y=at/w+dy;if(x<0||x>=w||y<0||y>=h)continue;
                int n=y*w+x;if(!walk[n])continue;float d=(Point(n)-target).sqrMagnitude;
                if(d<nearest){nearest=d;best=n;}
            }
            return best;
        }
        int[] Distances(int goal)
        {
            if(fields.TryGetValue(goal,out var field)){field.used=++sequence;return field.distance;}
            if(fields.Count>=MaxFields)
            {
                int oldest=-1;long age=long.MaxValue;
                foreach(var entry in fields)if(entry.Value.used<age){oldest=entry.Key;age=entry.Value.used;}
                field=fields[oldest];fields.Remove(oldest);
            }
            else field=new FlowField{distance=new int[walk.Length]};
            var distances=field.distance;for(int i=0;i<distances.Length;i++)distances[i]=int.MaxValue;
            queue.Clear();distances[goal]=0;queue.Enqueue(goal);
            while(queue.Count>0)
            {
                int at=queue.Dequeue(),x=at%w,y=at/w;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {
                    int nx=x+dx,ny=y+dy;if(nx<0||nx>=w||ny<0||ny>=h||(dx==0&&dy==0))continue;
                    int n=ny*w+nx;if(!walk[n]||distances[n]!=int.MaxValue)continue;
                    if(dx!=0&&dy!=0&&(!walk[y*w+nx]||!walk[ny*w+x]))continue;
                    distances[n]=distances[at]+1;queue.Enqueue(n);
                }
            }
            field.used=++sequence;fields.Add(goal,field);return distances;
        }
        internal Vector2 Direction(FiniteStomachMap m,Vector2 from,Vector2 target)
        {
            if(map!=m||definition!=m.Definition||walk==null)
            {
                map=m;definition=m.Definition;w=Mathf.CeilToInt(map.WorldRect.width/Cell);h=Mathf.CeilToInt(map.WorldRect.height/Cell);
                walk=new bool[w*h];fields.Clear();queue.Clear();sequence=0;
                for(int i=0;i<walk.Length;i++)walk[i]=map.NavigationDistance(Point(i),out _,true)>=Limit+GridMargin;
            }
            if(SegmentClear(from,target))return(target-from).normalized;
            int goal=Goal(target);if(goal<0)return Vector2.zero;
            var distances=Distances(goal);int here=Index(from),best=-1,steps=int.MaxValue;float tie=float.MaxValue;
            // Looking one additional cell out lets an off-grid dash recover to a safe waypoint.
            for(int dy=-2;dy<=2;dy++)for(int dx=-2;dx<=2;dx++)
            {
                int x=here%w+dx,y=here/w+dy;if(x<0||x>=w||y<0||y>=h)continue;
                int n=y*w+x;if(!walk[n]||distances[n]==int.MaxValue)continue;
                float distance=(Point(n)-from).sqrMagnitude;if(distance<.01f||!SegmentClear(from,Point(n)))continue;
                if(distances[n]<steps||distances[n]==steps&&distance<tie){steps=distances[n];tie=distance;best=n;}
            }
            if(best>=0)return(Point(best)-from).normalized;
            // Never repeatedly push into the ridge when a target moves along its opposite side.
            Vector2 safe=map.ClampToField(from,Limit+GridMargin);
            return(safe-from).sqrMagnitude>.0001f?(safe-from).normalized:Vector2.zero;
        }
    }
}

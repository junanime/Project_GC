using System.Collections.Generic;
using UnityEngine;
namespace Vampire
{
    // Strongest source wins. Dash trails have no exit tail; Kisses start their 2s timer on exit.
    public sealed class SnailSlowStatus : MonoBehaviour
    {
        struct Entry { public float amount,until; public bool inside; }
        readonly Dictionary<int,Entry> sources=new Dictionary<int,Entry>();
        readonly List<int> expired=new List<int>(); Character player;
        public float Multiplier {get;private set;}=1f;
        void Awake(){player=GetComponent<Character>();}
        public void Enter(int id,float amount){sources[id]=new Entry{amount=amount,inside=true};Recompute();}
        public void Exit(int id,float tail)
        {
            if(!sources.TryGetValue(id,out var e))return;
            if(tail<=0)sources.Remove(id); else {e.inside=false;e.until=Time.time+tail;sources[id]=e;}
            Recompute();
        }
        void Update()
        {
            if(SnailBossRuntime.Paused){Clear();return;}
            expired.Clear();foreach(var p in sources)if(!p.Value.inside&&Time.time>=p.Value.until)expired.Add(p.Key);
            foreach(int id in expired)sources.Remove(id);Recompute();
        }
        void Recompute()
        {
            float amount=0;foreach(var p in sources)amount=Mathf.Max(amount,p.Value.amount);
            float next=1-Mathf.Clamp01(amount);
            if(Mathf.Approximately(next,Multiplier))return;Multiplier=next;if(player!=null)player.UpdateMoveSpeed();
        }
        public void Clear(){sources.Clear();Recompute();}
        void OnDisable(){Clear();}
    }
}

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vampire.Tests.Editor
{
    // Separate physics scene: measures only the real simulation and its MonoBehaviour
    // callbacks. Main-game camera and unrelated monsters cannot contaminate its timings.
    public static class GStarFiberStress
    {
        const int Groups=12;
        const float Dt=.02f, Cooldown=.125f;
        static readonly CultureInfo Inv=CultureInfo.InvariantCulture;
        static Vector2 Center(int group)=>new Vector2(5000+(group%4)*8,-5000+(group/4)*8);
        static GameObject Root(Scene scene,string name)
        {
            var o=new GameObject(name);
            SceneManager.MoveGameObjectToScene(o,scene);
            return o;
        }
        static void Assert(bool ok,string msg){if(!ok)throw new InvalidOperationException("Fiber physics QA: "+msg);}
        public static IEnumerator Run(string output,int count,int ticks)
        {
            var scene=SceneManager.CreateScene("QA isolated fiber "+Guid.NewGuid().ToString("N"),
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
            var physics=scene.GetPhysicsScene2D();
            int capture=Time.captureFramerate;
            Time.captureFramerate=50;
            var receivers=new GStarFiberStressReceiver[Groups*2];
            var segments=new int[Groups];
            var shapes=new List<Collider2D>(count);
            var bodySet=new HashSet<Rigidbody2D>();
            var nextHit=new float[receivers.Length];
            var expected=new int[receivers.Length];
            var trace=new StringBuilder("tick,target,hits,hp\n");
            var timing=new StringBuilder("tick,simulate_ms\n");
            var costs=new double[ticks-10];
            var contacts=new List<Collider2D>(96);
            var observedPairs=new HashSet<ulong>();
            var shapesOnly=new HashSet<ulong>();
            int contactFiber=0,contactMob=0,overlapFiber=0,overlapMob=0;
            var filter=new ContactFilter2D().NoFilter();
            filter.useTriggers=true;
            try
            {
                for(int i=0;i<receivers.Length;i++)
                {
                    var go=Root(scene,"QA damage receiver "+i);
                    go.layer=LayerMask.NameToLayer("Monster Full");
                    go.transform.position=Center(i/2)+(i%2==0?Vector2.zero:Vector2.up*3);
                    var body=go.AddComponent<Rigidbody2D>();
                    body.gravityScale=0;body.constraints=RigidbodyConstraints2D.FreezeAll;
                    go.AddComponent<CircleCollider2D>().radius=.24f;
                    var secondary=new GameObject("Second hitbox");
                    secondary.layer=go.layer;secondary.transform.SetParent(go.transform,false);
                    secondary.AddComponent<CircleCollider2D>().radius=.21f;
                    receivers[i]=go.AddComponent<GStarFiberStressReceiver>();
                    receivers[i].enabled=false;
                }
                for(int s=0;s<count;s++)
                {
                    int group=s%Groups;
                    segments[group]++;
                    var go=Root(scene,"QA original fiber "+s);
                    var dir=(Vector2)(Quaternion.Euler(0,0,(s/Groups)*137.5077f+group*9)*Vector2.right);
                    var at=Center(group)+dir*.025f;
                    go.AddComponent<FiberTrailSegment>().Init(at-dir*.88f,at+dir*.88f,LayerMask.GetMask("Monster Full"),
                        (ticks+120)*Dt,8f,Cooldown,.18f,Color.white);
                    var col=go.GetComponent<Collider2D>(); shapes.Add(col);bodySet.Add(col.attachedRigidbody);
                }
                Physics2D.SyncTransforms();
                // Let captureFramerate take effect before any contact. Local physics only
                // advances explicitly below, while the rest of the game's scene still ticks.
                yield return null;
                for(int t=0;t<ticks;t++)
                {
                    bool visitor=(t%55>=8 && t%55<27) || t%55>=38;
                    for(int i=1;i<receivers.Length;i+=2)
                    {
                        var body=receivers[i].GetComponent<Rigidbody2D>();
                        var position=Center(i/2)+(visitor?Vector2.right*.055f:Vector2.up*3);
                        if(body.position!=position) body.position=position;
                    }
                    Physics2D.SyncTransforms();
                    long begin=Stopwatch.GetTimestamp();
                    bool ran=physics.Simulate(Dt);
                    double ms=(Stopwatch.GetTimestamp()-begin)*1000d/Stopwatch.Frequency;
                    Assert(ran,"the local physics simulation ran");
                    if(t>=10)costs[t-10]=ms;
                    timing.Append(t).Append(',').Append(ms.ToString("F5",Inv)).Append('\n');
                    for(int i=0;i<receivers.Length;i++)
                    {
                        bool inside=i%2==0 || visitor;
                        if(inside && Time.time>=nextHit[i]){expected[i]+=segments[i/2];nextHit[i]=Time.time+Cooldown;}
                        var receiver=receivers[i];
                        Assert(receiver.Hits==expected[i] && Mathf.Abs(receiver.HP-(100000-expected[i]))<.01f,
                            "receiver "+i+" tick "+t+" expected "+expected[i]+", observed "+receiver.Hits+" HP="+receiver.HP);
                        trace.Append(t).Append(',').Append(i).Append(',').Append(receiver.Hits).Append(',').Append((int)receiver.HP).Append('\n');
                    }
                    if(t==18)
                    {
                        // Separate from the timed window. Contact queries observe engine-captured
                        // pairs; shape overlaps are reported separately, never treated as callbacks.
                        foreach(var col in shapes)
                        {
                            contacts.Clear();col.GetContacts(filter,contacts);
                            foreach(var other in contacts)Count(col,other,observedPairs,ref contactFiber,ref contactMob);
                            contacts.Clear();col.OverlapCollider(filter,contacts);
                            foreach(var other in contacts)Count(col,other,shapesOnly,ref overlapFiber,ref overlapMob);
                        }
                    }
                    yield return null;
                }
                Array.Sort(costs);
                File.WriteAllText(Path.Combine(output,"fiber-hits.csv"),trace.ToString());
                File.WriteAllText(Path.Combine(output,"fiber-ticks.csv"),timing.ToString());
                var lines=new[]{"PASS","segments="+count,"receivers="+receivers.Length,"unique_fiber_bodies="+bodySet.Count,
                    "ticks="+ticks,"sum_hits="+expected.Sum(),"damage_per_hit=1","contact_fiber_pairs="+contactFiber,"contact_target_pairs="+contactMob,
                    "overlap_fiber_pairs="+overlapFiber,"overlap_target_pairs="+overlapMob,
                    "median_simulate_ms="+costs[(costs.Length-1)/2].ToString("F4",Inv),"p95_simulate_ms="+costs[(int)Math.Ceiling(costs.Length*.95)-1].ToString("F4",Inv)};
                File.WriteAllLines(Path.Combine(output,"fiber-checks.txt"),lines);
            }
            finally {Time.captureFramerate=capture;SceneManager.UnloadSceneAsync(scene);}
        }
        static void Count(Collider2D a,Collider2D b,HashSet<ulong> pairs,ref int fibers,ref int receivers)
        {
            if(b==null || a==b)return;
            uint x=unchecked((uint)a.GetInstanceID()),y=unchecked((uint)b.GetInstanceID());
            if(!pairs.Add(((ulong)Math.Min(x,y)<<32)|Math.Max(x,y)))return;
            if(b.GetComponent<FiberTrailSegment>()!=null)fibers++;
            else if(b.GetComponentInParent<GStarFiberStressReceiver>()!=null)receivers++;
        }
    }
    // Captures the damage which the production fiber actually dispatches. Both colliders
    // resolve to this one Monster, letting the fixture check per-target tick deduplication.
    public sealed class GStarFiberStressReceiver : Monster
    {
        public int Hits { get; private set; }
        protected override void Awake(){currentHealth=100000;}
        public override void TakeDamage(float damage,Vector2 knockback=default(Vector2),bool isCritical=false)
        {
            Hits++; currentHealth-=damage;
        }
    }
}

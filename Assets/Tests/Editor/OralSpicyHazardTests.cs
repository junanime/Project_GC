using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class OralSpicyHazardTests
    {
        [Test] public void WarningAndExtinguishingAreNeverDamaging()
        {
            Assert.IsFalse(OralSpicyHazard.IsHot(0));
            Assert.IsFalse(OralSpicyHazard.IsHot(OralTasteEvents.SpicyWarning-.001f));
            Assert.IsTrue(OralSpicyHazard.IsHot(OralTasteEvents.SpicyWarning));
            Assert.IsTrue(OralSpicyHazard.IsHot(OralTasteEvents.SpicyWarning+OralTasteEvents.SpicySeconds-.001f));
            Assert.IsFalse(OralSpicyHazard.IsHot(OralTasteEvents.SpicyWarning+OralTasteEvents.SpicySeconds));
            Assert.IsFalse(OralSpicyHazard.IsHot(OralTasteEvents.SpicyWarning+OralTasteEvents.SpicySeconds+.5f));
        }

        [Test] public void FootprintUsesTheVisibleCircleRatherThanTheWholeTongue()
        {
            var art=Resources.Load<Texture2D>("OralMap/Events/SpicyCircle");
            Assert.NotNull(art,"dedicated circular art is required");
            Assert.AreEqual(1254,art.width);Assert.AreEqual(1254,art.height);
            var centers = new List<Vector2>{new Vector2(4,-2),new Vector2(12,-2)};
            Assert.That(OralSpicyHazard.Radius*2,Is.EqualTo(1.4f*2*2).Within(.0001f));
            for(int i=0;i<360;i++)
            {
                var direction=new Vector2(Mathf.Cos(i*Mathf.Deg2Rad),Mathf.Sin(i*Mathf.Deg2Rad));
                Assert.IsTrue(OralSpicyHazard.Contains(centers,centers[0]+direction*(OralSpicyHazard.Radius-.001f)));
                Assert.IsFalse(OralSpicyHazard.Contains(centers,centers[0]+direction*(OralSpicyHazard.Radius+.001f)));
            }
            Assert.IsFalse(OralSpicyHazard.Contains(centers,new Vector2(8,-2)));
        }

        [Test] public void ActualTongueLayoutsHaveConnectedGapsAndNoWallOverlap()
        {
            var source=Resources.Load<StomachMapDefinition>("StomachMap/Stage1");Assert.NotNull(source);
            var go=new GameObject("Spicy geometry regression");go.SetActive(false);
            var map=go.AddComponent<FiniteStomachMap>();
            float width=source.worldWidth/Mathf.Sqrt(6);
            typeof(FiniteStomachMap).GetField("<WorldRect>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(map,new Rect(new Vector2(-41,-27),new Vector2(width,width/source.aspect)));
            try
            {
                Vector2 center=OralMapGeometry.TongueCenter(map),radii=OralMapGeometry.TongueRadii(map);
                Func<Vector2,float> clearance=p=>OralMapGeometry.TongueDistance(map,p,out _);
                var entrances=new List<Vector2>();
                for(int tooth=0;tooth<OralMapGeometry.ToothCount;tooth++)
                    if(OralGuardRoute.TryCreate(map,tooth,out var route))entrances.Add(route.Launch);
                Assert.AreEqual(8,entrances.Count,"all eight molar jump entrances are exercised");
                var arrangements=new HashSet<Vector2>();
                for(int rotation=-12;rotation<=12;rotation++)
                {
                    var centers=OralSpicyHazard.Layout(center,map.WorldRect,clearance,rotation);
                    Assert.AreEqual(10,centers.Count,"full-sized danger circles for distribution "+rotation);
                    arrangements.Add(centers[0]);
                    Assert.That(centers.FindAll(p=>p.y>center.y).Count,Is.InRange(4,6),"both northern and southern tongue receive hazards");
                    foreach(var entrance in entrances)
                        foreach(var hazard in centers)
                            Assert.That(Vector2.Distance(entrance,hazard),Is.GreaterThanOrEqualTo(OralSpicyHazard.Radius+.28f),"molar takeoff area remains free");
                    for(int a=0;a<centers.Count;a++)
                    {
                        for(int b=a+1;b<centers.Count;b++)
                            Assert.That(Vector2.Distance(centers[a],centers[b])-OralSpicyHazard.Radius*2,
                                Is.GreaterThanOrEqualTo(OralSpicyHazard.Passage-.0001f));
                        for(int sample=0;sample<360;sample++)
                        {
                            float angle=sample*Mathf.Deg2Rad;
                            var rim=centers[a]+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*OralSpicyHazard.Radius;
                            Assert.That(clearance(rim),Is.GreaterThanOrEqualTo(OralSpicyHazard.EdgePassage-.01f));
                        }
                    }
                    AssertConnectedSafeFloor(centers,clearance,Rect.MinMaxRect(center.x-radii.x,
                        OralMapGeometry.SouthFloor(map),center.x+radii.x,center.y+radii.y),entrances);
                }
                Assert.AreEqual(25,arrangements.Count,"each input produces new continuous coordinates, not one of six fixed layouts");
                var original=OralSpicyHazard.Layout(center,map.WorldRect,clearance,-12);
                var shift=new Vector2(173,-81);
                typeof(FiniteStomachMap).GetField("<WorldRect>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                    .SetValue(map,new Rect(map.WorldRect.position+shift,map.WorldRect.size));
                var moved=OralSpicyHazard.Layout(OralMapGeometry.TongueCenter(map),map.WorldRect,clearance,-12);
                Assert.AreEqual(original.Count,moved.Count);
                for(int i=0;i<original.Count;i++)Assert.That(Vector2.Distance(moved[i],original[i]+shift),Is.LessThan(.0001f),"translated maps preserve the same safe packing");
            }
            finally {Object.DestroyImmediate(go);}
        }

        static void AssertConnectedSafeFloor(List<Vector2> centers,Func<Vector2,float> clearance,Rect bounds,List<Vector2> entrances)
        {
            const float step=.35f,footRadius=.28f;
            int width=Mathf.CeilToInt(bounds.width/step),height=Mathf.CeilToInt(bounds.height/step),total=0,ground=0;
            var safe=new bool[width,height];var visited=new bool[width,height];var queue=new Queue<Vector2Int>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var point=bounds.min+new Vector2((x+.5f)*step,(y+.5f)*step);
                if(clearance(point)<footRadius)continue;ground++;
                bool free=true;foreach(var c in centers)if(Vector2.Distance(c,point)<OralSpicyHazard.Radius+footRadius){free=false;break;}
                if(!free)continue;safe[x,y]=true;total++;
                if(queue.Count==0)queue.Enqueue(new Vector2Int(x,y));
            }
            Assert.That(total/(float)ground,Is.InRange(.44f,.50f),"ten circles retain almost half the tongue as connected walking space, including actor width");
            int reached=0;var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();if(!safe[p.x,p.y]||visited[p.x,p.y])continue;visited[p.x,p.y]=true;reached++;
                foreach(var d in directions){var q=p+d;if(q.x>=0&&q.y>=0&&q.x<width&&q.y<height&&safe[q.x,q.y]&&!visited[q.x,q.y])queue.Enqueue(q);}
            }
            Assert.AreEqual(total,reached,"every sampled safe cell must belong to the same traversable region");
            foreach(var entrance in entrances)
            {
                int ex=Mathf.FloorToInt((entrance.x-bounds.xMin)/step),ey=Mathf.FloorToInt((entrance.y-bounds.yMin)/step);
                bool connected=false;
                for(int y=Mathf.Max(0,ey-2);y<=Mathf.Min(height-1,ey+2)&&!connected;y++)
                for(int x=Mathf.Max(0,ex-2);x<=Mathf.Min(width-1,ex+2)&&!connected;x++)
                {
                    if(!visited[x,y])continue;
                    var point=bounds.min+new Vector2((x+.5f)*step,(y+.5f)*step);
                    bool clear=true;int samples=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(entrance,point)/.04f));
                    for(int i=0;i<=samples&&clear;i++)
                    {
                        var p=Vector2.Lerp(entrance,point,i/(float)samples);
                        if(clearance(p)<footRadius){clear=false;break;}
                        foreach(var hazard in centers)if(Vector2.Distance(p,hazard)<OralSpicyHazard.Radius+footRadius){clear=false;break;}
                    }
                    connected=clear;
                }
                Assert.IsTrue(connected,"each molar entrance connects to the common safe walking network");
            }
        }

        [Test] public void ContinuousScatterChangesAllCirclesWithinTheSameSeedLayout()
        {
            var source=Resources.Load<StomachMapDefinition>("StomachMap/Stage1");Assert.NotNull(source);
            var go=new GameObject("Spicy continuous scatter regression");go.SetActive(false);
            var map=go.AddComponent<FiniteStomachMap>();float width=source.worldWidth/Mathf.Sqrt(6);
            typeof(FiniteStomachMap).GetField("<WorldRect>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic)
                .SetValue(map,new Rect(-41,-27,width,width/source.aspect));
            var savedRandom=UnityEngine.Random.state;
            try
            {
                Vector2 center=OralMapGeometry.TongueCenter(map);
                Func<Vector2,float> clearance=p=>OralMapGeometry.TongueDistance(map,p,out _);
                foreach(float input in new[]{-10.1f,1.234f,10.2f})
                {
                    var first=OralSpicyHazard.Layout(center,map.WorldRect,clearance,input);
                    var nearby=OralSpicyHazard.Layout(center,map.WorldRect,clearance,input+.0001f);
                    var repeat=OralSpicyHazard.Layout(center,map.WorldRect,clearance,input);
                    Assert.AreEqual(10,first.Count);Assert.AreEqual(10,nearby.Count);
                    CollectionAssert.AreEqual(first,repeat,"same seed remains reproducible for debugging");
                    float total=0,largest=0;
                    for(int i=0;i<first.Count;i++)
                    {
                        float displacement=Vector2.Distance(first[i],nearby[i]);total+=displacement;largest=Mathf.Max(largest,displacement);
                        Assert.That(displacement,Is.GreaterThan(.001f),"every circle changes even for inputs inside the same packed-layout bucket");
                        for(int j=i+1;j<nearby.Count;j++)Assert.That(Vector2.Distance(nearby[i],nearby[j])-OralSpicyHazard.Radius*2,
                            Is.GreaterThanOrEqualTo(OralSpicyHazard.Passage));
                    }
                    Assert.That(total/first.Count,Is.GreaterThan(.07f),"difference is spatially meaningful rather than a floating-point epsilon");
                    Assert.That(largest,Is.GreaterThan(.18f),"at least one circle visibly shifts within the safe corridor constraints");
                }
                UnityEngine.Random.InitState(31415);float expected=UnityEngine.Random.value;
                UnityEngine.Random.InitState(31415);OralSpicyHazard.Layout(center,map.WorldRect,clearance,1.234f);
                Assert.AreEqual(expected,UnityEngine.Random.value,"bounded scatter uses an independent RNG and preserves gameplay random state");
            }
            finally {UnityEngine.Random.state=savedRandom;Object.DestroyImmediate(go);}
        }

        [Test] public void SmallerChangedMapsNeverForceTenOverlappingCircles()
        {
            var bounds=new Rect(-8,-6,16,12);
            Func<Vector2,float> clearance=p=>Mathf.Min(8-Mathf.Abs(p.x),6-Mathf.Abs(p.y));
            var centers=OralSpicyHazard.Layout(Vector2.zero,bounds,clearance,0);
            Assert.That(centers.Count,Is.InRange(1,9),"an undersized future map uses the conservative fallback");
            for(int a=0;a<centers.Count;a++)
            {
                Assert.That(clearance(centers[a])-OralSpicyHazard.Radius,Is.GreaterThanOrEqualTo(OralSpicyHazard.EdgePassage));
                for(int b=a+1;b<centers.Count;b++)Assert.That(Vector2.Distance(centers[a],centers[b])-OralSpicyHazard.Radius*2,
                    Is.GreaterThanOrEqualTo(OralSpicyHazard.Passage));
            }
        }

        [Test] public void MultipleContactsCannotStackOrRestartTheBurnClock()
        {
            var once=new OralSpicyBurnClock();var overlapping=new OralSpicyBurnClock();int first=0,second=0;
            float last=-10;
            for(int frame=0;frame<600;frame++)
            {
                once.Expose();for(int contact=0;contact<4;contact++)overlapping.Expose();
                if(once.Tick(.02f))first++;
                if(overlapping.Tick(.02f)){second++;Assert.That(frame*.02f-last,Is.GreaterThanOrEqualTo(.99f));last=frame*.02f;}
            }
            Assert.AreEqual(first,second);Assert.That(first,Is.InRange(11,13));
        }

        [Test] public void ExitBurnExpiresAndLongFramesCannotBurstSeveralHits()
        {
            var clock=new OralSpicyBurnClock();clock.Expose();int ticks=0;
            for(int frame=0;frame<80;frame++)if(clock.Tick(.05f))ticks++;
            Assert.AreEqual(3,ticks);Assert.AreEqual(0,clock.Remaining);
            clock.Reset();clock.Expose();Assert.IsTrue(clock.Tick(2.5f));
            Assert.IsFalse(clock.Tick(.001f));Assert.IsFalse(clock.Tick(.001f));
            clock.Reset();Assert.IsFalse(clock.Tick(20));Assert.AreEqual(0,clock.Remaining);
        }

        public static void Run()
        {
            try
            {
                var t=new OralSpicyHazardTests();t.WarningAndExtinguishingAreNeverDamaging();
                t.FootprintUsesTheVisibleCircleRatherThanTheWholeTongue();t.ActualTongueLayoutsHaveConnectedGapsAndNoWallOverlap();
                t.ContinuousScatterChangesAllCirclesWithinTheSameSeedLayout();t.SmallerChangedMapsNeverForceTenOverlappingCircles();
                t.MultipleContactsCannotStackOrRestartTheBurnClock();t.ExitBurnExpiresAndLongFramesCannotBurstSeveralHits();
                Debug.Log("[OralSpicyHazardTests] PASS 7 suites: timed danger, circular contact, 25 continuous ten-circle layouts, connected molar entrances, independent random scatter, changed-map fallback, nonstacking burn");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

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
                for(int rotation=-12;rotation<=12;rotation++)
                {
                    var centers=OralSpicyHazard.Layout(center,map.WorldRect,clearance,rotation);
                    Assert.That(centers.Count,Is.InRange(5,16),"distribution rotation "+rotation);
                    Assert.That(Vector2.Distance(center,centers[0]),Is.LessThan(.001f));
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
                        OralMapGeometry.SouthFloor(map),center.x+radii.x,center.y+radii.y));
                }
            }
            finally {Object.DestroyImmediate(go);}
        }

        static void AssertConnectedSafeFloor(List<Vector2> centers,Func<Vector2,float> clearance,Rect bounds)
        {
            const float step=.35f,footRadius=.28f;
            int width=Mathf.CeilToInt(bounds.width/step),height=Mathf.CeilToInt(bounds.height/step),total=0,ground=0;
            var safe=new bool[width,height];var queue=new Queue<Vector2Int>();
            for(int y=0;y<height;y++)for(int x=0;x<width;x++)
            {
                var point=bounds.min+new Vector2((x+.5f)*step,(y+.5f)*step);
                if(clearance(point)<footRadius)continue;ground++;
                bool free=true;foreach(var c in centers)if(Vector2.Distance(c,point)<OralSpicyHazard.Radius+footRadius){free=false;break;}
                if(!free)continue;safe[x,y]=true;total++;
                if(queue.Count==0)queue.Enqueue(new Vector2Int(x,y));
            }
            Assert.That(total/(float)ground,Is.InRange(.3f,.85f),"safe floor share");
            int reached=0;var directions=new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();if(!safe[p.x,p.y])continue;safe[p.x,p.y]=false;reached++;
                foreach(var d in directions){var q=p+d;if(q.x>=0&&q.y>=0&&q.x<width&&q.y<height&&safe[q.x,q.y])queue.Enqueue(q);}
            }
            Assert.That(reached,Is.GreaterThanOrEqualTo(total*.99f),"safe floor must remain traversable, not isolated islands");
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
                t.MultipleContactsCannotStackOrRestartTheBurnClock();t.ExitBurnExpiresAndLongFramesCannotBurstSeveralHits();
                Debug.Log("[OralSpicyHazardTests] PASS 5 suites: timed danger, circular contact, 25 tongue layouts, connected safe gaps, nonstacking burn");
                EditorApplication.Exit(0);
            }
            catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
        }
    }
}

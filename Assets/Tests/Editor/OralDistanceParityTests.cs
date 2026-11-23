using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class OralDistanceParityTests
    {
        [Test] public void VortexMembershipMatchesDepthAtBoundariesAndTies()
        {
            var host=new GameObject("Vortex lookup parity");
            try
            {
                var finale=host.AddComponent<OralSwallowFinale>();
                var list=(System.Collections.Generic.List<OralWhirlpoolState>)finale.Whirlpools;
                list.Add(new OralWhirlpoolState{Position=Vector2.zero,Radius=2f});
                list.Add(new OralWhirlpoolState{Position=Vector2.zero,Radius=2f});
                list.Add(new OralWhirlpoolState{Position=new Vector2(.8f,.2f),Radius=2.3f});
                var find=typeof(OralSwallowFinale).GetMethod("FindContainingVortex",BindingFlags.NonPublic|BindingFlags.Instance);
                void CheckAt(Vector2 p)
                {
                    OralWhirlpoolState best=null;float depth=0f;
                    foreach (var w in list)
                    {
                        float d=1-Vector2.Distance(p,w.Position)/w.Radius;
                        if (d>depth) { best=w;depth=d; }
                    }
                    Assert.AreSame(best,find.Invoke(finale,new object[]{p}), $"Vortex selection at {p:R}, reference depth {depth:R}");
                }
                CheckAt(Vector2.zero);
                foreach (var w in list)
                    for (int i=0;i<16;i++)
                    {
                        float a=i*Mathf.PI/8;
                        Vector2 dir=new Vector2(Mathf.Cos(a),Mathf.Sin(a));
                        CheckAt(w.Position+dir*w.Radius);
                        CheckAt(w.Position+dir*(w.Radius-.00001f)); CheckAt(w.Position+dir*(w.Radius+.00001f));
                    }
            }
            finally { Object.DestroyImmediate(host); }
        }

        [TestCase(-58f,-44f,124f,105f)]
        [TestCase(243f,-651f,199f,130f)]
        public void BandMatchesFullContourAndClampAtEdgesAndAcrossMap(float x,float y,float width,float height)
        {
            var host=new GameObject("Oral band parity test");
            try
            {
                var map=host.AddComponent<FiniteStomachMap>();
                var rect=new Rect(x,y,width,height);
                typeof(FiniteStomachMap).GetProperty("WorldRect").SetValue(map,rect);
                OralMapGeometry.ToothWalkCenter(map,0);
                var band=(Vector2[])typeof(OralMapGeometry).GetField("band",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
                void CheckPoint(Vector2 point)
                {
                    float expected=OralMapGeometry.ToothDistance(point,out var normal,band);
                    float actual=OralMapGeometry.TeethDistance(map,point,out var result);
                    if (expected!=actual || !normal.Equals(result))
                        Assert.Fail($"Contour {point:R}: expected {expected:R}/{normal:R}, actual {actual:R}/{result:R}");
                }
                for (int i=0;i<band.Length;i++)
                {
                    var a=band[(i+band.Length-1)%band.Length];var b=band[i];
                    var mid=(a+b)*.5f;
                    var n=Vector2.Perpendicular(b-a).normalized*.0001f;
                    CheckPoint(b);CheckPoint(mid);CheckPoint(mid+n);CheckPoint(mid-n);
                    CheckPoint(new Vector2(rect.xMin,b.y));CheckPoint(new Vector2(rect.xMax,b.y));
                }
                var random=new System.Random(20261121);
                for (int i=0;i<3072;i++)
                {
                    var p=new Vector2(x+width*((float)random.NextDouble()*1.4f-.2f),y+height*((float)random.NextDouble()*1.4f-.2f));
                    CheckPoint(p);
                    if (i%16!=0) continue;
                    float radius=(i%31)*.08f;
                    float tongue=OralMapGeometry.TongueDistance(map,p,out _);
                    var oldZone=tongue>OralMapGeometry.ToothDistance(p,out _,band) ? OralZone.Tongue : OralZone.Teeth;
                    var expected=OralMapGeometry.Clamp(map,p,radius,oldZone);
                    var actual=OralMapGeometry.Clamp(map,p,radius,OralZone.None);
                    Assert.IsTrue(expected.Equals(actual),"auto-selected surface and final position for "+p);
                }
            }
            finally { Object.DestroyImmediate(host); }
        }
    }
}

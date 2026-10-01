using NUnit.Framework;
using UnityEngine;
namespace Vampire.Tests.Editor
{
    public class SnailBossRegressionTests
    {
        [Test] public void InstalledAssetsAndRules(){SnailBossChecks.Run();}
        [Test] public void BodyPosesShareOneCanvasWithoutEyestalkDistortion()
        {
            var reference=SnailBossArt.Get("Body1");
            foreach(string name in new[]{"Body1","Body2","Puff1","Puff2","DashPuff1","DashPuff2","BasicSpit1","BasicSpit2","Spit1","Spit2","Groggy1","Groggy2"})
            {
                var pose=SnailBossArt.Get(name);Assert.NotNull(pose,name);
                Assert.AreEqual(reference.rect.size,pose.rect.size,name);
                Assert.AreEqual(reference.pivot,pose.pivot,name);Assert.AreEqual(reference.pixelsPerUnit,pose.pixelsPerUnit,name);
            }
        }
        [Test] public void OverlappingSlowsRestoreRemainingSourceNotBasePrematurely()
        {
            var go=new GameObject("Slow rule test");
            try
            {
                var slow=go.AddComponent<SnailSlowStatus>();
                slow.Enter(1,.25f);slow.Enter(2,.3f);Assert.AreEqual(.7f,slow.Multiplier,.0001f);
                slow.Exit(2,0);Assert.AreEqual(.75f,slow.Multiplier,.0001f);
                slow.Exit(1,0);Assert.AreEqual(1,slow.Multiplier);
                slow.Enter(3,.3f);slow.Exit(3,2);Assert.AreEqual(.7f,slow.Multiplier,.0001f);
                slow.Clear();Assert.AreEqual(1,slow.Multiplier);
            }finally{Object.DestroyImmediate(go);}
        }
    }
}

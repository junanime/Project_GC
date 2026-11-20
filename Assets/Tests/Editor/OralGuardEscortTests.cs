using NUnit.Framework;
using UnityEngine;

namespace Vampire.Tests.Editor
{
    public sealed class OralGuardEscortTests
    {
        [Test] public void SharedHealthRejectsSameVolleyAndCompletesOnlyOnce()
        {
            var health = new OralGuardHealth();
            Assert.AreEqual(80, health.Current);
            for (int member = 0; member < 5; member++) health.Damage(12);
            Assert.AreEqual(68, health.Current, "One volley overlapping five members is one shared hit");
            health.Advance(.64f); Assert.IsFalse(health.Damage(12));
            health.Advance(.02f); Assert.IsTrue(health.Damage(12));
            Assert.IsTrue(health.Complete()); Assert.IsFalse(health.Complete());
            health.Advance(1); Assert.IsFalse(health.Damage(999));
            Assert.IsFalse(health.Failed); Assert.AreEqual(56, health.Current);
        }
        [Test] public void LethalHitAndTimeoutCannotBecomeRewardSuccess()
        {
            var health = new OralGuardHealth();
            Assert.IsTrue(health.Damage(80)); Assert.IsTrue(health.Failed);
            Assert.IsFalse(health.Complete()); Assert.IsFalse(health.Damage(1));
            Assert.AreEqual(0, health.Current);
            var timeout = new OralGuardHealth(); timeout.Fail();
            Assert.IsFalse(timeout.Complete()); Assert.IsFalse(timeout.Completed);
            Assert.IsFalse(new OralGuardHealth().Damage(float.NaN));
        }
        [Test] public void EveryMolarHasSafeStraightApproachAndValidJump()
        {
            var go = new GameObject("Escort route geometry"); var map = go.AddComponent<FiniteStomachMap>();
            try
            {
                var stage = Resources.Load<StomachMapDefinition>("StomachMap/Stage1");
                var size = stage.Size / Mathf.Sqrt(6);
                typeof(FiniteStomachMap).GetProperty("WorldRect").SetValue(map, new Rect(new Vector2(41, -23), size));
                int valid = 0;
                for (int tooth = 0; tooth < OralMapGeometry.ToothCount; tooth++)
                {
                    bool found = OralGuardRoute.TryCreate(map, tooth, out var route);
                    Assert.AreEqual(OralMapGeometry.ToothKind(tooth) == OralToothKind.Molar, found, "escort destination " + tooth);
                    if (!found) continue; valid++;
                    Assert.AreEqual(tooth, OralMapGeometry.NearestTooth(map, route.Landing));
                    Assert.AreEqual(OralZone.Teeth, OralMapGeometry.ZoneAt(map, route.Landing));
                    for (int i = 0; i < 24; i++)
                    {
                        var near = map.UVToWorld(new Vector2((i % 6) / 5f, (i / 6) / 3f));
                        var safe = OralMapGeometry.Clamp(map, near, .75f, OralZone.Tongue);
                        Assert.IsTrue(OralGuardRoute.SegmentOnTongue(map, safe, route.Launch), "straight tongue path " + tooth + "/" + i);
                    }
                }
                Assert.AreEqual(8, valid);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}

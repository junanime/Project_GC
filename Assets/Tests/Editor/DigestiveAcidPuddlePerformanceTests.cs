using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class DigestiveAcidPuddlePerformanceTests
    {
        static readonly Vector2 Center = new Vector2(4578, -3902);
        static readonly Func<Collider2D, List<IDamageable>, IDamageable> Resolve =
            (Func<Collider2D, List<IDamageable>, IDamageable>)Delegate.CreateDelegate(
                typeof(Func<Collider2D, List<IDamageable>, IDamageable>),
                typeof(NeedleTargeting).GetMethod("FindResidueTarget", BindingFlags.Static | BindingFlags.NonPublic));
        static readonly MethodInfo Apply = typeof(DigestiveAcidPuddle).GetMethod("ApplyDamage", BindingFlags.Instance | BindingFlags.NonPublic);

        static GameObject Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }
        static IDamageable LegacyOwner(Collider2D hit)
        {
            IDamageable king = hit.GetComponentInParent<ConstipationKingRuntime>();
            if (king != null) return king;
            IDamageable colon = hit.GetComponentInParent<ColonBossRuntime>();
            return colon != null ? colon : hit.GetComponentInParent<Monster>();
        }

        [Test]
        public void NestedTargetsRetainTypePriorityAndNearestOwnerOfThatType()
        {
            var root = new GameObject("Nested acid targets");
            try
            {
                var kings = Child(root.transform, "Two kings");
                var outerKing = kings.AddComponent<ConstipationKingRuntime>();
                var innerKing = Child(kings.transform, "Inner king").AddComponent<ConstipationKingRuntime>();
                var colon = Child(innerKing.transform, "Colon").AddComponent<ColonBossRuntime>();
                var monster = Child(colon.transform, "Ordinary monster").AddComponent<Monster>();
                var hit = Child(monster.transform, "Hitbox").AddComponent<BoxCollider2D>();
                var scratch = new List<IDamageable>();
                void Compare(IDamageable expected)
                {
                    Assert.AreSame(expected, LegacyOwner(hit));
                    Assert.AreSame(expected, Resolve(hit, scratch));
                }
                Compare(innerKing);
                innerKing.enabled = false; Compare(innerKing);
                Object.DestroyImmediate(innerKing); Compare(outerKing);
                colon.transform.SetParent(root.transform); Compare(colon);
                monster.transform.SetParent(root.transform); Compare(monster);
                hit.transform.SetParent(root.transform); Compare(null);
                hit.transform.SetParent(monster.transform); Compare(monster);
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void FirstEntryOverlappingHitboxesAndTwoSeparatePuddlesKeepTheirTickTiming()
        {
            var root = new GameObject("Acid tick fixture");
            root.transform.position = Center;
            try
            {
                var monster = Child(root.transform, "Ordinary monster").AddComponent<Monster>();
                ColonContentSmoke.Set(monster, "currentHealth", 100f);
                monster.transform.localPosition = Vector3.right * 4;
                Child(monster.transform, "Body").AddComponent<BoxCollider2D>();
                Child(monster.transform, "Legs").AddComponent<CircleCollider2D>();
                DigestiveAcidPuddle Puddle()
                {
                    var p = Child(root.transform, "Puddle").AddComponent<DigestiveAcidPuddle>();
                    ColonContentSmoke.Set(p, "radius", 1f);
                    ColonContentSmoke.Set(p, "damagePerSecond", 8f);
                    ColonContentSmoke.Set(p, "tickInterval", .5f);
                    return p;
                }
                var first = Puddle(); var second = Puddle();
                void Tick(DigestiveAcidPuddle p) { Physics2D.SyncTransforms(); Apply.Invoke(p, null); }
                Tick(first); Assert.AreEqual(100f, monster.HP);
                monster.transform.localPosition = Vector3.zero;
                Tick(first); Assert.AreEqual(96f, monster.HP, "entry is immediate and two hitboxes count once");
                Tick(first); Assert.AreEqual(96f, monster.HP, "unchanged tick deadline");
                Tick(second); Assert.AreEqual(92f, monster.HP, "separate zones stack");
                var deadlines = (Dictionary<int,float>)ColonContentSmoke.Field(first, "nextDamageTimes").GetValue(first);
                deadlines[monster.gameObject.GetInstanceID()] = Time.time - .01f;
                Tick(first); Assert.AreEqual(88f, monster.HP, "next due tick is applied");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CrowdedOverlapPreservesOrderedRecipientsAndMeasuresBothQueries()
        {
            var root = new GameObject("Crowded puddle");
            root.transform.position = Center;
            try
            {
                for (int i = 0; i < 120; i++)
                {
                    var holder = Child(root.transform, "Pickup or enemy");
                    holder.transform.localPosition = new Vector3((i % 10) * .13f, (i / 10) * .11f, i % 7);
                    if (i % 5 == 0) holder.AddComponent<Monster>();
                    Child(holder.transform, "Surface").AddComponent<CircleCollider2D>();
                }
                Physics2D.SyncTransforms();
                var hits = new List<Collider2D>();
                var parents = new List<IDamageable>();
                var expected = Physics2D.OverlapCircleAll(Center, 3f);
                NeedleTargeting.FillCircle(Center, 3f, Physics2D.DefaultRaycastLayers, hits);
                CollectionAssert.AreEqual(expected, hits);
                foreach (var hit in expected) Assert.AreSame(LegacyOwner(hit), Resolve(hit, parents));
                (double Ms, long Bytes, int Targets) Cost(bool reuse)
                {
                    int targets = 0;
                    long before = GC.GetAllocatedBytesForCurrentThread();
                    long time = Stopwatch.GetTimestamp();
                    for (int n = 0; n < 64; n++)
                    {
                        if (reuse)
                        {
                            NeedleTargeting.FillCircle(Center, 3f, Physics2D.DefaultRaycastLayers, hits);
                            foreach (var hit in hits) if (Resolve(hit, parents) != null) targets++;
                        }
                        else foreach (var hit in Physics2D.OverlapCircleAll(Center, 3f))
                            if (LegacyOwner(hit) != null) targets++;
                    }
                    return ((Stopwatch.GetTimestamp() - time) * 1000d / Stopwatch.Frequency,
                        GC.GetAllocatedBytesForCurrentThread() - before, targets);
                }
                var oldA = Cost(false); var newA = Cost(true); var newB = Cost(true); var oldB = Cost(false);
                Assert.AreEqual(oldA.Targets, newB.Targets);
                Assert.AreEqual(24 * 64, newA.Targets);
                TestContext.WriteLine($"128 crowded queries, previous {oldA.Ms+oldB.Ms:F2} ms / {oldA.Bytes+oldB.Bytes} bytes; current {newA.Ms+newB.Ms:F2} ms / {newA.Bytes+newB.Bytes} bytes");
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class OrbitDamageWitness : IDamageable
    {
        public List<OrbitDamageWitness> Trace;
        public override void TakeDamage(float damage, Vector2 knockback = default, bool isCritical = false)
        {
            Assert.Greater(damage, 0f);
            Trace.Add(this);
        }
        public override void Knockback(Vector2 knockback) { }
    }

    public sealed class CombatTargetPerformanceTests
    {
        // SendMessage asks Unity to schedule a player behaviour in EditMode and asserts.
        // Call its managed lifecycle method directly when driving these isolated fixtures.
        static void CallLifecycle(Component component, string name) =>
            component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, null);

        [Test]
        public void OverlappingOrbitNeedlesPreserveContactOrderAndIndependentDamageCooldowns()
        {
            var root = new GameObject("Two orbiting needles");
            root.transform.position = new Vector3(-4720, 4600, 0);
            var random = Random.state;
            try
            {
                var source = root.AddComponent<Character>();
                ColonContentSmoke.Set(source, "critChance", 0f);
                var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Junhan/Prefabs/Abilities/Syringe Dart Ability.prefab");
                var ability = Object.Instantiate(prefab, root.transform).GetComponent<SyringeDartAbility>();
                ColonContentSmoke.Set(ability, "playerCharacter", source);
                var orbit = root.AddComponent<HedgehogNeedleController>();
                ColonContentSmoke.Set(orbit, "sourceCharacter", source);
                ColonContentSmoke.Set(orbit, "sourceNeedleAbility", ability);
                ColonContentSmoke.Set(orbit, "damageMultiplier", 1f);
                ColonContentSmoke.Set(orbit, "sameNeedleSameTargetCooldown", 30f);
                ColonContentSmoke.Set(orbit, "nextBurstVfxAt", float.PositiveInfinity);
                var needles = (List<Transform>)ColonContentSmoke.Field(orbit, "orbitNeedleVisuals").GetValue(orbit);
                var observed = new List<OrbitDamageWitness>();
                for (int i = 0; i < 2; i++)
                {
                    var needle = new GameObject("Orbit point");
                    needle.transform.SetParent(root.transform, false);
                    needles.Add(needle.transform);
                    var obj = new GameObject("Target");
                    obj.transform.SetParent(root.transform, false);
                    obj.transform.localPosition = Vector3.forward * i;
                    obj.AddComponent<OrbitDamageWitness>().Trace = observed;
                    obj.AddComponent<BoxCollider2D>();
                    obj.AddComponent<CircleCollider2D>();
                }
                Physics2D.SyncTransforms();
                var legacy = Physics2D.OverlapCircleAll(root.transform.position, .03f)
                    .Select(hit => hit.GetComponent<OrbitDamageWitness>()).Where(hit => hit != null).Distinct().ToList();
                Assert.AreEqual(2, legacy.Count);
                void Detect() => CallLifecycle(orbit, "DetectEnemiesTouchingOrbitNeedles");
                Detect();
                CollectionAssert.AreEqual(legacy.Concat(legacy), observed);
                observed.Clear();
                Detect();
                Assert.IsEmpty(observed);
                var times = (System.Collections.IDictionary)ColonContentSmoke.Field(orbit, "nextDamageAllowedTimeByNeedleAndTarget").GetValue(orbit);
                foreach (var key in times.Keys.Cast<object>().ToArray())
                    if ((int)key.GetType().GetField("needleIndex").GetValue(key) == 0) times[key] = Time.time - 1;
                Detect();
                CollectionAssert.AreEqual(legacy, observed);
            }
            finally { Object.DestroyImmediate(root); Random.state = random; }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReusablePhysicsQueryKeepsEveryLegacyHitInTheSameOrder(bool triggers)
        {
            var group = new GameObject("Needle overlap fixture");
            var center = new Vector2(-4721, 4600);
            bool original = Physics2D.queriesHitTriggers;
            try
            {
                // Include ties at identical Z, mixed depth, and more than a small fixed buffer.
                for (int i = 0; i < 96; i++)
                {
                    var target = new GameObject("collider " + i);
                    target.transform.SetParent(group.transform);
                    target.layer = i % 5 == 0 ? 2 : 0;
                    target.transform.position = new Vector3(center.x + i % 4 * .10f, center.y + i % 2 * .2f, i % 5);
                    target.AddComponent<CircleCollider2D>().isTrigger = i % 3 == 0;
                }
                Physics2D.SyncTransforms();
                Physics2D.queriesHitTriggers = triggers;
                var hits = new List<Collider2D>();
                foreach (int mask in new[] { ~0, Physics2D.DefaultRaycastLayers, 1 << 2, 0 })
                {
                    NeedleTargeting.FillCircle(center, 2f, mask, hits);
                    CollectionAssert.AreEqual(Physics2D.OverlapCircleAll(center, 2f, mask), hits);
                }
            }
            finally { Physics2D.queriesHitTriggers = original; Object.DestroyImmediate(group); }
        }

        [Test]
        public void CompressionFallbackTracksPooledDisabledAndNeutralActorsInLegacyOrder()
        {
            var group = new GameObject("Compression fallback fixture");
            var monsters = new List<Monster>();
            var enzymes = new List<DigestiveEnzymeMonster>();
            try
            {
                var enemy = new GameObject("Disabled component");
                enemy.transform.SetParent(group.transform);
                var monster = enemy.AddComponent<Monster>();
                monster.enabled = false;
                CallLifecycle(monster, "Awake");
                var pooled = new GameObject("Pooled neutral enzyme");
                pooled.transform.SetParent(group.transform);
                CallLifecycle(pooled.AddComponent<DigestiveEnzymeMonster>(), "Awake");
                pooled.SetActive(false);
                void Compare()
                {
                    StageActorRegistry.CopyActiveMonsters(monsters);
                    StageActorRegistry.CopyActiveEnzymes(enzymes);
                    CollectionAssert.AreEqual(Object.FindObjectsOfType<Monster>(), monsters);
                    CollectionAssert.AreEqual(Object.FindObjectsOfType<DigestiveEnzymeMonster>(), enzymes);
                }
                Compare();
                pooled.SetActive(true);
                Compare();
                var added = new GameObject("Added after first cast");
                added.transform.SetParent(group.transform);
                var late = added.AddComponent<Monster>();
                // Non-ExecuteAlways components in edit mode do not run Awake automatically.
                CallLifecycle(late, "Awake");
                Compare();
                group.SetActive(false);
                Compare();
                group.SetActive(true);
                Object.DestroyImmediate(added);
                Compare();
            }
            finally { Object.DestroyImmediate(group); }
        }

        [Test]
        public void CompressionKeepsFirstEligibleComponentAcrossDeathReparentingAndReuse()
        {
            var root = new GameObject("Nested compression targets");
            try
            {
                var field = root.AddComponent<OrganCompressionField>();
                var find = typeof(OrganCompressionField).GetMethod("FirstEligibleParent", BindingFlags.NonPublic | BindingFlags.Instance);
                var parent = new GameObject("Outer actor");
                parent.transform.SetParent(root.transform, false);
                var outer = parent.AddComponent<Monster>();
                var child = new GameObject("Inner actor with two damageable components");
                child.transform.SetParent(parent.transform, false);
                var inner = child.AddComponent<Monster>();
                var second = child.AddComponent<Monster>();
                var surface = new GameObject("Hit surface");
                surface.transform.SetParent(child.transform, false);
                var hit = surface.AddComponent<CircleCollider2D>();
                void Compare()
                {
                    var legacy = hit.GetComponentsInParent<Component>(true).OfType<IDamageable>().FirstOrDefault(
                        actor => actor.gameObject.activeInHierarchy && (!(actor is Monster m) || m.HP > 0));
                    Assert.AreSame(legacy, find.Invoke(field, new object[] { hit }));
                }
                ColonContentSmoke.Set(outer, "currentHealth", 10f);
                Compare(); // two dead inner components, live outer parent
                ColonContentSmoke.Set(second, "currentHealth", 8f);
                Compare(); // dead first, live second on the same GameObject
                ColonContentSmoke.Set(inner, "currentHealth", 5f);
                inner.enabled = false;
                Compare(); // disabled component on an active object is still eligible
                surface.transform.SetParent(root.transform, false);
                Compare();
                surface.transform.SetParent(child.transform, false);
                parent.SetActive(false); Compare();
                parent.SetActive(true); Compare();
                Object.DestroyImmediate(inner); Compare();
            }
            finally { Object.DestroyImmediate(root); }
        }

        [Test]
        public void CompressionAfterglowsKeepAllActiveHitsAndReuseOnlyExpiredDrawing()
        {
            var owner = new GameObject("Compression afterglow owner");
            var props = new MaterialPropertyBlock();
            var poolField = typeof(HerbalGroundVisual).GetField("poolRoot", BindingFlags.NonPublic | BindingFlags.Static);
            var previousPool = poolField.GetValue(null) as Transform;
            float previousOpacity = GamePreferences.Current.augmentVfxOpacity;
            try
            {
                GamePreferences.PreviewAugmentVfx(.4f);
                var first = HerbalGroundVisual.Create(owner.transform, Vector3.right, .24f, .24f, 2);
                var firstMesh = first.GetComponent<MeshFilter>().sharedMesh;
                var firstMat = first.GetComponent<MeshRenderer>().sharedMaterial;
                var ids = new HashSet<int> { first.GetInstanceID() };
                for (int i = 0; i < 257; i++)
                {
                    var effect = HerbalGroundVisual.Create(owner.transform, Vector3.up, .24f, .24f, 2);
                    Assert.IsTrue(ids.Add(effect.GetInstanceID()), "a live hit visual was stolen");
                    Assert.AreSame(firstMesh, effect.GetComponent<MeshFilter>().sharedMesh);
                    Assert.AreSame(firstMat, effect.GetComponent<MeshRenderer>().sharedMaterial);
                }
                // EditMode does not automatically tick the runtime behaviours.
                CallLifecycle(first.GetComponent<AugmentVfxTag>(), "OnEnable");
                ColonContentSmoke.Set(first, "started", Time.time - .12f);
                CallLifecycle(first, "LateUpdate");
                first.GetComponent<MeshRenderer>().GetPropertyBlock(props);
                Assert.AreEqual(2f, props.GetFloat("_Style"));
                Assert.That(props.GetFloat("_Fade"), Is.EqualTo(1f).Within(.001f));
                Assert.That(props.GetFloat("_AugmentOpacity"), Is.EqualTo(.4f).Within(.001f));
                Assert.AreEqual("Vampire/HerbalGround", firstMat.shader.name);
                ColonContentSmoke.Set(first, "started", Time.time - .239f);
                CallLifecycle(first, "LateUpdate");
                Assert.IsTrue(first.gameObject.activeSelf, "last live frame of the hit must render");
                ColonContentSmoke.Set(first, "started", Time.time - .25f);
                CallLifecycle(first, "LateUpdate");
                Assert.IsFalse(first.gameObject.activeSelf);
                var reused = HerbalGroundVisual.Create(owner.transform, Vector3.left, .3f, .4f, 2);
                Assert.AreSame(first, reused);
                Assert.IsTrue(reused.gameObject.activeSelf);
                Assert.AreSame(owner.transform, reused.transform.parent);
                Assert.AreEqual(Vector3.left, reused.transform.position);
                Assert.AreEqual(258, owner.transform.childCount);
            }
            finally
            {
                Object.DestroyImmediate(owner);
                if (previousPool == null && poolField.GetValue(null) is Transform pool && pool != null)
                    Object.DestroyImmediate(pool.gameObject);
                GamePreferences.PreviewAugmentVfx(previousOpacity);
            }
        }
    }
}

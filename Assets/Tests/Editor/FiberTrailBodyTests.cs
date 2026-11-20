using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Vampire.Tests.Editor
{
    public sealed class FiberContactWitness : MonoBehaviour
    {
        public readonly List<Collider2D> Entered = new List<Collider2D>();
        public readonly List<Collider2D> Stayed = new List<Collider2D>();
        public readonly List<Collider2D> Exited = new List<Collider2D>();
        void OnTriggerEnter2D(Collider2D other) { if (other.GetComponent<FiberTrailSegment>()) Entered.Add(other); }
        void OnTriggerStay2D(Collider2D other) { if (other.GetComponent<FiberTrailSegment>()) Stayed.Add(other); }
        void OnTriggerExit2D(Collider2D other) { if (other.GetComponent<FiberTrailSegment>()) Exited.Add(other); }
    }

    public sealed class FiberTrailBodyTests
    {
        sealed class Fixture
        {
            internal readonly Scene Scene;
            internal readonly PhysicsScene2D Physics;
            internal readonly List<BoxCollider2D> Fields = new List<BoxCollider2D>();
            internal readonly Monster Monster;
            internal readonly Rigidbody2D Target;
            internal readonly Rigidbody2D Observer;
            internal readonly FiberContactWitness Witness;
            internal readonly Character Source;
            readonly Transform legacyParent;

            GameObject Create(string name)
            {
                var obj = new GameObject(name);
                SceneManager.MoveGameObjectToScene(obj, Scene);
                return obj;
            }
            internal Fixture(string name, bool separate)
            {
                Scene = SceneManager.CreateScene(name, new CreateSceneParameters(LocalPhysicsMode.Physics2D));
                Physics = Scene.GetPhysicsScene2D();
                if (separate) legacyParent = Create("External parent with independent trail bodies").transform;
                var source = Create("Source");
                source.SetActive(false);
                Source = source.AddComponent<Character>();
                var actor = Create("Monster with child hitboxes on a different layer");
                actor.layer = 6;
                Target = actor.AddComponent<Rigidbody2D>();
                Target.gravityScale = 0;
                Target.position = Vector2.right * 6;
                Monster = actor.AddComponent<Monster>();
                Monster.enabled = false;
                actor.GetComponent<ZPositioner>().enabled = false;
                ColonContentSmoke.Set(Monster, "currentHealth", 100f);
                for (int i = 0; i < 2; i++)
                {
                    var hit = Create("Secondary hitbox");
                    hit.transform.SetParent(actor.transform, false);
                    hit.AddComponent<CircleCollider2D>().isTrigger = true;
                }
                var observer = Create("Unrelated stationary kinematic actor");
                Observer = observer.AddComponent<Rigidbody2D>();
                Observer.bodyType = RigidbodyType2D.Kinematic;
                Observer.position = Vector2.right * 6;
                observer.AddComponent<CircleCollider2D>().isTrigger = true;
                Witness = observer.AddComponent<FiberContactWitness>();
                for (int i = 0; i < 2; i++) Fields.Add(MakeField(0f, 30f));
            }
            internal BoxCollider2D MakeField(float x, float duration)
            {
                var go = Create("Fiber");
                if (legacyParent) go.transform.SetParent(legacyParent);
                var segment = go.AddComponent<FiberTrailSegment>();
                segment.Init(new Vector2(x - 1, 0), new Vector2(x + 1, 0), 1 << 6, duration,
                    8f, .5f, .12f, Color.white, Source, "Fiber body parity");
                return go.GetComponent<BoxCollider2D>();
            }
            internal void CheckQuery()
            {
                var hits = new List<Collider2D>();
                var filter = new ContactFilter2D();
                filter.NoFilter();
                filter.useTriggers = true;
                Physics.OverlapCircle(Vector2.zero, 1.5f, filter, hits);
                CollectionAssert.AreEquivalent(Fields, hits.Where(c => c.GetComponent<FiberTrailSegment>() != null));
            }
            internal void Position(float x)
            {
                Target.position = new Vector2(x, 0);
                Observer.position = new Vector2(x, 0);
            }
            internal void Step(float expected)
            {
                Physics2D.SyncTransforms();
                Assert.IsTrue(Physics.Simulate(.02f));
                Assert.AreEqual(expected, Monster.HP, Scene.name);
            }
            internal void MakeTicksDue()
            {
                foreach (var field in Fields)
                {
                    var seg = field.GetComponent<FiberTrailSegment>();
                    var times = (Dictionary<int,float>)ColonContentSmoke.Field(seg, "nextDamageTimesByTarget").GetValue(seg);
                    times[Monster.gameObject.GetInstanceID()] = Time.time - .01f;
                }
            }
        }

        [UnityTest]
        public IEnumerator PerShapeEnterStayExitQuerySourceAndSceneLifetimeMatchSeparateBodies()
        {
            yield return new EnterPlayMode();
            Time.timeScale = 1;
            var old = new Fixture("Separate fiber comparison", true);
            var now = new Fixture("Compound fiber comparison", false);
            var second = new Fixture("Independent compound scene", false);
            Assert.AreNotSame(old.Fields[0].attachedRigidbody, old.Fields[1].attachedRigidbody);
            var shared = now.Fields[0].attachedRigidbody;
            Assert.AreSame(shared, now.Fields[1].attachedRigidbody);
            Assert.AreNotSame(shared, second.Fields[0].attachedRigidbody);
            Assert.AreEqual(now.Scene, shared.gameObject.scene);
            foreach (var fixture in new[] { old, now })
            {
                fixture.CheckQuery();
                fixture.Step(100);
                fixture.Position(0); fixture.Step(92);
                fixture.Step(92);
                CollectionAssert.AreEquivalent(fixture.Fields, fixture.Witness.Entered);
                CollectionAssert.AreEquivalent(fixture.Fields, fixture.Witness.Stayed.Distinct());
                fixture.MakeTicksDue(); fixture.Step(84);
                fixture.Position(6); fixture.Step(84);
                CollectionAssert.AreEquivalent(fixture.Fields, fixture.Witness.Exited);
                fixture.Position(0); fixture.Step(84);
                Object.Destroy(fixture.Source.gameObject);
            }
            float deadline = Time.realtimeSinceStartup + 5f;
            while ((old.Source != null || now.Source != null) && Time.realtimeSinceStartup < deadline) yield return null;
            foreach (var fixture in new[] { old, now })
            {
                Assert.IsTrue(fixture.Source == null);
                fixture.MakeTicksDue(); fixture.Step(76);
                Assert.AreEqual(4, fixture.Witness.Entered.Count, "both shapes notify after re-entry");
            }
            var shortFiber = now.MakeField(12, .1f);
            deadline = Time.realtimeSinceStartup + 5f;
            while (shortFiber != null && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(shortFiber == null && shared != null, "expired shape leaves its sibling bodies active");
            Assert.AreEqual(2, shared.attachedColliderCount);
            var unloading = SceneManager.UnloadSceneAsync(now.Scene);
            while (!unloading.isDone) yield return null;
            Assert.IsTrue(shared == null);
            second.Position(0); second.Step(92);
            unloading = SceneManager.UnloadSceneAsync(old.Scene);
            while (!unloading.isDone) yield return null;
            unloading = SceneManager.UnloadSceneAsync(second.Scene);
            while (!unloading.isDone) yield return null;
            yield return new ExitPlayMode();
        }
    }
}

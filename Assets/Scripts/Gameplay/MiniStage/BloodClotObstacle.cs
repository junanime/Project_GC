using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Fixed movement footprint, separate from interaction triggers and damage hitboxes.</summary>
    [DisallowMultipleComponent]
    public sealed class BloodClotObstacle : MonoBehaviour
    {
        public const string LayerName = "BloodClot Solid";
        internal static readonly HashSet<BloodClotObstacle> Active = new HashSet<BloodClotObstacle>();
        private CircleCollider2D solid;
        public Collider2D Solid => solid;
        private static readonly RaycastHit2D[] castHits = new RaycastHit2D[32];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() { Active.Clear(); }

        public static BloodClotObstacle Ensure(GameObject owner)
        {
            var obstacle = owner.GetComponent<BloodClotObstacle>();
            if (obstacle == null) obstacle = owner.AddComponent<BloodClotObstacle>();
            obstacle.Configure();
            return obstacle;
        }

        public void Configure()
        {
            var source = GetComponent<CircleCollider2D>();
            bool damageableClot = GetComponent<BloodClotMonster>() != null;
            if (solid == null)
            {
                var child = new GameObject("Blood Clot Solid Footprint");
                child.transform.SetParent(transform, false);
                child.layer = LayerMask.NameToLayer(LayerName);
                solid = child.AddComponent<CircleCollider2D>();
                solid.isTrigger = false;
            }
            // Keep the outer interaction ring reachable without entering the physical footprint.
            solid.radius = source != null ? source.radius * (damageableClot ? 1f : 0.65f) : 0.3f;
            solid.offset = source != null ? source.offset : Vector2.zero;
            if (damageableClot && source != null) source.isTrigger = true;
            var body = GetComponent<Rigidbody2D>();
            if (body != null)
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.gravityScale = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }
            solid.enabled = isActiveAndEnabled;
            foreach (var actor in BloodClotPassThrough.Active)
                if (actor != null) actor.ApplyTo(this, true);
        }

        private void OnEnable() { Active.Add(this); Configure(); }
        private void OnDisable() { Active.Remove(this); if (solid != null) solid.enabled = false; }
        public void SetBlocking(bool value) { if (solid != null) solid.enabled = value; }

        // Dash temporarily turns the player's body into a trigger; explicitly sweep the footprint
        // so that this visual ghosting cannot bypass a solid blood clot.
        public static Vector2 ClampDash(Rigidbody2D body, Vector2 destination)
        {
            if (body == null) return destination;
            Vector2 delta = destination - body.position;
            if (delta.sqrMagnitude < 0.000001f) return destination;
            var collider = body.GetComponent<Collider2D>();
            if (collider == null) return destination;
            var filter = new ContactFilter2D();
            filter.SetLayerMask(LayerMask.GetMask(LayerName));
            filter.useTriggers = false;
            float distance = delta.magnitude;
            int count = collider.Cast(delta / distance, filter, castHits, distance);
            for (int i = 0; i < count; i++)
                distance = Mathf.Min(distance, Mathf.Max(0f, castHits[i].distance - 0.01f));
            return body.position + delta.normalized * distance;
        }
    }
}

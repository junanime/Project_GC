using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    /// <summary>Ground projection independent of the floating visual; shared tint ownership handles overlaps.</summary>
    [DefaultExecutionOrder(10000)]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class BossGroundShadow : MonoBehaviour
    {
        [SerializeField] private BossPartDamageTestRootController healthRoot;
        [SerializeField] private Vector2 radius = new Vector2(2.4f, 0.9f);
        [SerializeField] private Color shadowColor = new Color(0.12f, 0.10f, 0.16f, 0.45f);
        private SpriteRenderer projection;
        private readonly HashSet<SpriteRenderer> affected = new HashSet<SpriteRenderer>();
        private readonly HashSet<SpriteRenderer> found = new HashSet<SpriteRenderer>();
        private readonly List<SpriteRenderer> leaving = new List<SpriteRenderer>();
        private static readonly Dictionary<SpriteRenderer, TintState> states = new Dictionary<SpriteRenderer, TintState>();

        private sealed class TintState
        {
            public Color original;
            public Color applied;
            public readonly Dictionary<BossGroundShadow, Color> sources = new Dictionary<BossGroundShadow, Color>();
        }

        private void Awake() { projection = GetComponent<SpriteRenderer>(); }

        private void LateUpdate()
        {
            if (projection == null) projection = GetComponent<SpriteRenderer>();
            bool visible = healthRoot == null || !healthRoot.IsBossDead;
            projection.enabled = visible;
            projection.color = shadowColor;
            projection.sortingLayerName = "Default";
            projection.sortingOrder = -20;
            if (!visible) { ReleaseAll(); return; }

            found.Clear();
            Vector2 extent = new Vector2(Mathf.Abs(transform.lossyScale.x) * radius.x,
                Mathf.Abs(transform.lossyScale.y) * radius.y);
            Vector2 center = transform.position;
            // Query all colliders so disabled/destroyed actors and teleports need no exit callback.
            foreach (var collider in Physics2D.OverlapAreaAll(center - extent, center + extent))
            {
                var player = collider.GetComponentInParent<Character>();
                Component actor = player != null ? (Component)player : collider.GetComponentInParent<Monster>();
                if (actor == null || actor.transform.root == transform.root) continue;
                Vector2 local = transform.InverseTransformPoint(actor.transform.position);
                if (radius.x <= 0 || radius.y <= 0 ||
                    local.x * local.x / (radius.x * radius.x) + local.y * local.y / (radius.y * radius.y) > 1) continue;
                foreach (var renderer in actor.GetComponentsInChildren<SpriteRenderer>(true))
                    if (renderer != null && renderer.gameObject.activeInHierarchy && renderer != projection)
                        found.Add(renderer);
            }
            leaving.Clear();
            foreach (var renderer in affected) if (!found.Contains(renderer)) leaving.Add(renderer);
            foreach (var renderer in leaving) { Release(renderer); affected.Remove(renderer); }
            foreach (var renderer in found) { Apply(renderer); affected.Add(renderer); }
        }

        private void Apply(SpriteRenderer renderer)
        {
            if (!states.TryGetValue(renderer, out var state))
            {
                state = new TintState { original = renderer.color, applied = renderer.color };
                states.Add(renderer, state);
            }
            CaptureExternalColor(renderer, state);
            state.sources[this] = Color.Lerp(Color.white, new Color(shadowColor.r, shadowColor.g, shadowColor.b, 1), shadowColor.a);
            Recompute(renderer, state);
        }

        private static void CaptureExternalColor(SpriteRenderer renderer, TintState state)
        {
            // Preserve hit flashes or other gameplay color changes made since our last write.
            if (renderer.color != state.applied) state.original = renderer.color;
        }

        private static void Recompute(SpriteRenderer renderer, TintState state)
        {
            Color color = state.original;
            foreach (var tint in state.sources.Values) color *= tint;
            state.applied = color;
            renderer.color = color;
        }

        private void Release(SpriteRenderer renderer)
        {
            if (!states.TryGetValue(renderer, out var state)) return;
            if (renderer != null) CaptureExternalColor(renderer, state);
            state.sources.Remove(this);
            if (renderer != null) Recompute(renderer, state);
            if (state.sources.Count == 0) states.Remove(renderer);
        }

        private void ReleaseAll()
        {
            foreach (var renderer in affected) Release(renderer);
            affected.Clear();
        }

        private void OnDisable() { ReleaseAll(); }
        private void OnDestroy() { ReleaseAll(); }
    }
}

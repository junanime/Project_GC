using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Vampire
{
    /// <summary>Passive corner ornaments, driven by live event state, never by an estimated timer.</summary>
    public sealed class StageEventCornerUI : MonoBehaviour
    {
        private sealed class Cluster
        {
            public RectTransform root;
            public Image foundation;
            public readonly List<Image> particles = new List<Image>();
            public StageEventFlowGraphic flow;
            public int corner;
        }
        private sealed class Active
        {
            public StageEventPresentation state;
            public readonly List<Cluster> clusters = new List<Cluster>();
            public float age;
        }
        private readonly Dictionary<object, Active> active = new Dictionary<object, Active>();
        private readonly List<object> removal = new List<object>();
        private readonly List<object> order = new List<object>();
        private StageEventCornerArt art;
        private CanvasGroup group;
        private RectTransform canvasRoot;
        public int ActiveCount => active.Count;
        public bool IsVisible => group != null && group.alpha > 0;
        public float VisualAge(object key) => active.TryGetValue(key, out var entry) ? entry.age : 0;

        public void Sync(IReadOnlyList<StageEventPresentation> states, bool hidden, bool paused)
        {
            if (canvasRoot == null && states.Count == 0) return;
            EnsureCanvas();
            bool layoutChanged = false;
            removal.Clear();
            foreach (var pair in active)
            {
                bool found = false;
                for (int i = 0; i < states.Count; i++) if (ReferenceEquals(states[i].key, pair.Key)) { found = true; break; }
                if (!found) removal.Add(pair.Key);
            }
            foreach (var key in removal)
            {
                ClearClusters(active[key]); active.Remove(key); order.Remove(key); layoutChanged = true;
            }
            for (int i = 0; i < states.Count; i++)
            {
                var state = states[i];
                if (!active.TryGetValue(state.key, out var entry))
                {
                    entry = new Active(); active.Add(state.key, entry); order.Add(state.key); layoutChanged = true;
                }
                entry.state = state;
            }
            if (layoutChanged) RebuildLayout();
            group.alpha = hidden || active.Count == 0 ? 0 : 1;
            foreach (var entry in active.Values)
            {
                if (!hidden && !paused) entry.age += Time.deltaTime;
                foreach (var cluster in entry.clusters) Animate(entry, cluster);
            }
        }

        public void Hide() { if (group != null) group.alpha = 0; }
        private void OnDisable() => Hide();
        private void OnDestroy() { if (canvasRoot != null) Destroy(canvasRoot.gameObject); }

        private void EnsureCanvas()
        {
            if (canvasRoot != null) return;
            art = Resources.Load<StageEventCornerArt>("StageEventCornerArt");
            // Above the world, below existing HUD canvases: ornaments must never cover
            // XP, currencies, the minimap, or skill/consumable controls in a corner.
            canvasRoot = CreateCanvas("Stage event corner ornaments", -10);
            group = canvasRoot.GetComponent<CanvasGroup>();
        }

        internal static RectTransform CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = sortingOrder;
            var scaler = go.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            var group = go.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            // Deliberately no GraphicRaycaster: decorations cannot steal gameplay clicks.
            return go.GetComponent<RectTransform>();
        }

        internal static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = anchor;
            rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
        }

        internal static Image Picture(string name, Transform parent, Vector2 position, Vector2 size, Sprite sprite)
        {
            var rect = Rect(name, parent, Vector2.zero, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = sprite; image.preserveAspect = true;
            image.raycastTarget = false; image.enabled = sprite != null; return image;
        }

        private void ClearClusters(Active entry)
        {
            foreach (var c in entry.clusters) if (c.root != null) { c.root.gameObject.SetActive(false); Destroy(c.root.gameObject); }
            entry.clusters.Clear();
        }

        private void RebuildLayout()
        {
            foreach (var entry in active.Values) ClearClusters(entry);
            if (order.Count == 0) return;
            // One event: all four corners. Two: opposite pairs. Three/four: a corner each.
            // More than four (debug/custom schedules): a second small cluster along the edge.
            int slots = Mathf.Max(4, order.Count);
            for (int slot = 0; slot < slots; slot++)
            {
                int corner = slot % 4, lane = slot / 4;
                var entry = active[order[slot % order.Count]];
                bool right = corner == 1 || corner == 3, top = corner < 2;
                Vector2 anchor = new Vector2(right ? 1 : 0, top ? 1 : 0);
                // Keep effects inside the outer corners, leaving screen middle and timer clear.
                var root = Rect(entry.state.kind + " corner " + corner, canvasRoot, anchor,
                    new Vector2((right ? -1 : 1) * (88 + lane * 150), top ? -66 : 66), new Vector2(166,118));
                root.pivot = Vector2.zero;
                var c = new Cluster {root = root, corner = corner};
                var frames = art != null ? art.Frames(entry.state.kind) : null;
                Sprite first = frames != null && frames.Length > 0 ? frames[0] : null;
                if (entry.state.kind == StageEventVisualKind.Antacid)
                    c.foundation = Picture("Gameplay foam",root,new Vector2(0,-17),new Vector2(155,104),art != null ? art.antacidFoam : null);
                for (int i = 0; i < 5; i++) c.particles.Add(Picture("Animated " + entry.state.kind + " " + i, root, Vector2.zero, Vector2.one * 48, first));
                if (entry.state.kind == StageEventVisualKind.Drift || entry.state.kind == StageEventVisualKind.Coffee)
                {
                    var r = Rect("Flow",root,Vector2.zero,Vector2.zero,new Vector2(155,100));
                    c.flow = r.gameObject.AddComponent<StageEventFlowGraphic>(); c.flow.raycastTarget = false;
                    c.flow.color = entry.state.kind == StageEventVisualKind.Coffee ? new Color(1,.91f,.75f,.85f) : new Color(.72f,1,1,.85f);
                    c.flow.steam = entry.state.kind == StageEventVisualKind.Coffee;
                }
                entry.clusters.Add(c);
            }
        }

        private void Animate(Active entry, Cluster cluster)
        {
            var frames = art != null ? art.Frames(entry.state.kind) : null;
            if (frames == null || frames.Length == 0) return;
            float age = entry.age, fadeIn = Mathf.Clamp01(age / .35f);
            if (cluster.foundation != null)
            {
                cluster.foundation.color = new Color(1,1,1,.72f * fadeIn);
                cluster.foundation.rectTransform.localScale = Vector3.one * (1 + .025f * Mathf.Sin(age * 2));
            }
            if (cluster.flow != null) cluster.flow.Animate(age, entry.state.direction);
            for (int i = 0; i < cluster.particles.Count; i++)
            {
                var image = cluster.particles[i]; var rect = image.rectTransform;
                float phase = i * .193f + cluster.corner * .13f;
                float t = Mathf.Repeat(age / (2.1f + (i % 3) * .25f) + phase, 1);
                float alpha = Mathf.Clamp01(t * 8) * Mathf.Clamp01((1 - t) * 7) * fadeIn;
                float size = 36 + (i % 3) * 12;
                Vector2 p = new Vector2((i - 2) * 27, -24 + t * 65);
                float scaleX = 1, scaleY = 1, rotation = 0;
                int frame = Mathf.FloorToInt(age * 7 + i) % frames.Length;
                switch (entry.state.kind)
                {
                    case StageEventVisualKind.Antacid:
                        size = 68 + (i % 3) * 20;
                        p.x += Mathf.Sin(t * 5 + phase * 6) * 9;
                        scaleX = scaleY = Mathf.Lerp(.45f,1.12f,Mathf.Min(t / .76f,1));
                        frame = t < .76f ? 0 : Mathf.Min(frames.Length - 1,1 + Mathf.FloorToInt((t - .76f) / .24f * (frames.Length - 1)));
                        break;
                    case StageEventVisualKind.Infection:
                        p = new Vector2((i - 2) * 28, Mathf.Sin(age * 2 + i) * 13 + (i % 2) * 24 - 15);
                        scaleX = 1 + .15f * Mathf.Sin(age * 3 + i); scaleY = 2 - scaleX;
                        rotation = Mathf.Sin(age * 1.5f + i) * 15;
                        break;
                    case StageEventVisualKind.Gold:
                        scaleX = Mathf.Max(.12f, Mathf.Abs(Mathf.Cos(age * 3 + i)));
                        p.y += Mathf.Sin(t * Mathf.PI) * 15; size = 42;
                        break;
                    case StageEventVisualKind.AcidRain:
                        p.y = 44 - t * 85; size = 47; rotation = -12;
                        break;
                    case StageEventVisualKind.Reflux:
                        p = new Vector2(-40 + t * 85, (i % 3 - 1) * 25); size = 95;
                        scaleX = entry.state.direction < 0 ? -1 : 1;
                        scaleY = .9f + .1f * Mathf.Sin(age * 4 + i);
                        break;
                    case StageEventVisualKind.Drift:
                        p = new Vector2((-58 + t * 116) * entry.state.direction, (i % 3 - 1) * 22);
                        size = 32; rotation = -30 * entry.state.direction + Mathf.Sin(age * 3 + i) * 12;
                        break;
                    case StageEventVisualKind.Coffee:
                        p = new Vector2((i - 2) * 26, -25 + Mathf.Sin(age * 2 + i) * 7);
                        size = 40; alpha = fadeIn * .92f; rotation = Mathf.Sin(age * 1.6f + i) * 18;
                        break;
                }
                image.sprite = frames[frame]; image.color = new Color(1,1,1,alpha);
                rect.anchoredPosition = p; rect.sizeDelta = new Vector2(size,size);
                rect.localScale = new Vector3(scaleX,scaleY,1); rect.localRotation = Quaternion.Euler(0,0,rotation);
            }
        }
    }
}

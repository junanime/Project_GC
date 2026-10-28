using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Vampire
{
    // Port of the approved 03-wind-ribbons-smooth preview. All distances are in
    // its 254 x 412 card space, independent of the actual UI/device resolution.
    public sealed class AugmentSelectionMotion : MonoBehaviour
    {
        public const float Duration = 1.5f;
        // Uniformly retime the approved 3.27-second motion, including both fades.
        const float TimingScale = Duration / 3.27f;
        public const float OrbitStart = .55f * TimingScale, OrbitEnd = 2.15f * TimingScale,
            TurnDuration = 1.12f * TimingScale;
        public const int NeedleCount = 8;
        public float Age { get; private set; }
        public float Turn => Mathf.Clamp01((Age - OrbitEnd) / TurnDuration);
        public float Yaw => Mathf.PI * Smooth(Turn);
        public float CardOpacity => 1 - Smooth((Turn - .42f) / .58f);
        public float WindOpacity => Smooth((Age - OrbitStart) / (.16f * TimingScale)) * (1 - Smooth(Turn / .60f));
        public float Spin => Mathf.PI * 2 * Mathf.Clamp01((Age - OrbitStart) / (OrbitEnd - OrbitStart));
        public bool ShowingBack => Mathf.Cos(Yaw) < 0;
        public RectTransform Root { get; private set; }
        public Vector2 Size => Root.rect.size;
        public bool ManualPreview { get; set; }
        public static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }

        struct SavedGroup { public CanvasGroup group; public float alpha; public bool interactable, blocks; }
        readonly List<SavedGroup> groups = new List<SavedGroup>();
        readonly List<AugmentCardProjection> projections = new List<AugmentCardProjection>();
        readonly List<AugmentTextProjection> textProjections = new List<AugmentTextProjection>();
        readonly List<Graphic> effects = new List<Graphic>();
        RectTransform cardRect;
        RectTransform slotPlaceholder;
        Transform originalParent;
        int originalIndex;
        Vector2 anchorMin, anchorMax, pivot, sizeDelta, anchored;
        Vector3 scale;
        CanvasGroup selectedGroup;
        Material backMaterial, needleMaterial;
        Action completed;
        bool disposed;
        double startedAt;

        public static AugmentSelectionMotion Create(RectTransform parent, AbilityCard selected,
            List<AbilityCard> cards, Action complete)
        {
            selected.SettleForSelection();
            Canvas.ForceUpdateCanvases();
            var root = AugmentPanelTheme.Rect("Selected card tornado", parent);
            var motion = root.gameObject.AddComponent<AugmentSelectionMotion>();
            motion.Root = root;
            motion.completed = complete;
            motion.cardRect = (RectTransform)selected.transform;
            var c = motion.cardRect;
            motion.originalParent = c.parent; motion.originalIndex = c.GetSiblingIndex();
            motion.anchorMin = c.anchorMin; motion.anchorMax = c.anchorMax; motion.pivot = c.pivot;
            motion.sizeDelta = c.sizeDelta; motion.anchored = c.anchoredPosition; motion.scale = c.localScale;
            AugmentPanelTheme.Box(root, parent.InverseTransformPoint(c.TransformPoint(c.rect.center)), c.rect.size);
            // Root is a sibling of the layout, not a child of a card mask. Both
            // halves of the orbit may extend over adjacent cards at full size.
            root.SetAsLastSibling();
            foreach (var card in cards)
            {
                if (card == null) continue;
                var g = card.GetComponent<CanvasGroup>();
                if (g == null) g = card.gameObject.AddComponent<CanvasGroup>();
                motion.groups.Add(new SavedGroup { group = g, alpha = g.alpha, interactable = g.interactable, blocks = g.blocksRaycasts });
                // Blocking pointer hits is enough; the dialog also gates every
                // keyboard action. Disabling interactable would tint the chosen
                // card with the button's grey disabled color during its turn.
                g.blocksRaycasts = false;
                if (card == selected) motion.selectedGroup = g;
            }
            motion.backMaterial = new Material(Resources.Load<Shader>("AugmentSelection/CardBackTint"));
            motion.backMaterial.SetColor("_Accent", StitchPatchTheme.CardAccent(selected.SelectionGrade));
            motion.needleMaterial = new Material(Resources.Load<Shader>("AugmentSelection/NeedleProjection"));
            motion.AddEffect("Rear wind ribbons", AugmentOrbitGraphic.Part.Wind, false);
            motion.AddEffect("Rear needles", AugmentOrbitGraphic.Part.Needles, false);
            motion.slotPlaceholder = AugmentPanelTheme.Rect("Selected card slot", motion.originalParent);
            motion.slotPlaceholder.sizeDelta = c.rect.size;
            motion.slotPlaceholder.SetSiblingIndex(motion.originalIndex);
            var slot = motion.slotPlaceholder.gameObject.AddComponent<LayoutElement>();
            slot.preferredWidth = c.rect.width; slot.preferredHeight = c.rect.height;
            slot.minWidth = c.rect.width; slot.minHeight = c.rect.height;
            c.SetParent(root, false); AugmentPanelTheme.Box(c, Vector2.zero, root.rect.size);
            // Project the live UI (including actual labels/icons) instead of
            // loading a large frame animation or capturing the screen on mobile.
            foreach (var graphic in selected.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic is TextMeshProUGUI text)
                {
                    var textProjection = text.gameObject.AddComponent<AugmentTextProjection>();
                    textProjection.Init(motion, text); motion.textProjections.Add(textProjection); continue;
                }
                var projection = graphic.gameObject.AddComponent<AugmentCardProjection>();
                projection.motion = motion;
                motion.projections.Add(projection);
            }
            var back = AugmentPanelTheme.Rect("Embroidered card back", root).gameObject.AddComponent<AugmentBackGraphic>();
            AugmentPanelTheme.Anchors(back.rectTransform, Vector2.zero, Vector2.one);
            back.motion = motion; back.material = motion.backMaterial;
            back.texture = Resources.Load<Texture2D>("AugmentSelection/CardBack");
            back.raycastTarget = false; back.maskable = false; motion.effects.Add(back);
            motion.AddEffect("Front wind ribbons", AugmentOrbitGraphic.Part.Wind, true);
            motion.AddEffect("Front needles", AugmentOrbitGraphic.Part.Needles, true);
            motion.Sample(0);
            motion.startedAt = Time.realtimeSinceStartupAsDouble;
            return motion;
        }

        void AddEffect(string name, AugmentOrbitGraphic.Part part, bool front)
        {
            var g = AugmentPanelTheme.Rect(name, Root).gameObject.AddComponent<AugmentOrbitGraphic>();
            AugmentPanelTheme.Anchors(g.rectTransform, Vector2.zero, Vector2.one);
            g.motion = this; g.part = part; g.front = front; g.raycastTarget = false; g.maskable = false;
            if (part == AugmentOrbitGraphic.Part.Needles)
            {
                g.texture = Resources.Load<Texture2D>("AugmentSelection/Needle");
                g.material = needleMaterial;
            }
            effects.Add(g);
        }

        void Update()
        {
            if (disposed || ManualPreview) return;
            // The creation frame may already include a long layout/asset load.
            // Count only time since this animation actually became ready.
            Sample((float)(Time.realtimeSinceStartupAsDouble - startedAt));
            if (Age >= Duration)
            {
                var callback = completed;
                Cancel();
                callback?.Invoke();
            }
        }

        public void Sample(float time)
        {
            Age = Mathf.Clamp(time, 0, Duration);
            foreach (var saved in groups)
                if (saved.group != null) saved.group.alpha = saved.group == selectedGroup ?
                    (ShowingBack ? 0 : CardOpacity) : saved.alpha * (1 - Smooth(Age / (.22f * TimingScale)));
            foreach (var p in projections) if (p != null) p.Refresh();
            foreach (var p in textProjections) if (p != null) p.Refresh();
            foreach (var g in effects) if (g != null) g.SetVerticesDirty();
        }

        public Vector3 Project(Vector3 p)
        {
            float x = p.x / Size.x * 254f;
            float depth = -x * Mathf.Sin(Yaw);
            float perspective = 780f / (780f + depth);
            return new Vector3(p.x * Mathf.Cos(Yaw) * perspective, p.y * perspective, p.z);
        }

        public void Cancel()
        {
            if (disposed) return;
            disposed = true; completed = null;
            foreach (var p in projections) if (p != null) { p.enabled = false; p.Refresh(); Destroy(p); }
            foreach (var p in textProjections) if (p != null) { p.Restore(); Destroy(p); }
            if (cardRect != null && originalParent != null)
            {
                cardRect.SetParent(originalParent, false); cardRect.SetSiblingIndex(originalIndex);
                cardRect.anchorMin = anchorMin; cardRect.anchorMax = anchorMax; cardRect.pivot = pivot;
                cardRect.sizeDelta = sizeDelta; cardRect.anchoredPosition = anchored; cardRect.localScale = scale;
            }
            if (slotPlaceholder != null) { slotPlaceholder.gameObject.SetActive(false); Destroy(slotPlaceholder.gameObject); }
            foreach (var saved in groups) if (saved.group != null)
            { saved.group.alpha = saved.alpha; saved.group.interactable = saved.interactable; saved.group.blocksRaycasts = saved.blocks; }
            if (backMaterial != null) Destroy(backMaterial);
            if (needleMaterial != null) Destroy(needleMaterial);
            gameObject.SetActive(false); Destroy(gameObject);
        }
        void OnDestroy() { if (!disposed) Cancel(); }
    }

    public sealed class AugmentCardProjection : BaseMeshEffect
    {
        public AugmentSelectionMotion motion;
        public void Refresh() { if (graphic != null) graphic.SetVerticesDirty(); }
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || motion == null) return;
            var v = UIVertex.simpleVert;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                var p = motion.Root.InverseTransformPoint(transform.TransformPoint(v.position));
                v.position = transform.InverseTransformPoint(motion.Root.TransformPoint(motion.Project(p)));
                vh.SetUIVertex(v, i);
            }
        }
    }

    public sealed class AugmentTextProjection : MonoBehaviour
    {
        AugmentSelectionMotion motion;
        TextMeshProUGUI text;
        public void Init(AugmentSelectionMotion owner, TextMeshProUGUI label)
        { motion = owner; text = label; text.OnPreRenderText += Project; }
        public void Refresh() { if (text != null && text.isActiveAndEnabled) text.ForceMeshUpdate(); }
        void Project(TMP_TextInfo info)
        {
            if (motion == null) return;
            foreach (var mesh in info.meshInfo)
                for (int i = 0; i < mesh.vertexCount; i++)
                {
                    var p = motion.Root.InverseTransformPoint(transform.TransformPoint(mesh.vertices[i]));
                    mesh.vertices[i] = transform.InverseTransformPoint(motion.Root.TransformPoint(motion.Project(p)));
                }
        }
        public void Restore()
        { if (text != null) { text.OnPreRenderText -= Project; text.ForceMeshUpdate(); } motion = null; }
        void OnDestroy() { Restore(); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AugmentBackGraphic : MaskableGraphic
    {
        public AugmentSelectionMotion motion;
        public Texture texture;
        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (motion == null || !motion.ShowingBack) return;
            // A subdivided perspective mesh keeps the small signature and sewn
            // characters undistorted. Reverse UVs, so the back is never mirrored.
            const int strips = 64;
            var tint = new Color(1, 1, 1, motion.CardOpacity);
            for (int i = 0; i < strips; i++)
            {
                float a = i / (float)strips, b = (i + 1f) / strips;
                int n = vh.currentVertCount;
                Add(vh, a, 0, tint); Add(vh, a, 1, tint); Add(vh, b, 1, tint); Add(vh, b, 0, tint);
                vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
            }
        }
        void Add(VertexHelper vh, float x, float y, Color c)
        { vh.AddVert(motion.Project(new Vector3((x - .5f) * motion.Size.x, (y - .5f) * motion.Size.y)), c, new Vector2(1 - x, y)); }
    }

    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AugmentOrbitGraphic : MaskableGraphic
    {
        public enum Part { Wind, Needles }
        public AugmentSelectionMotion motion;
        public Part part;
        public bool front;
        public Texture texture;
        public override Texture mainTexture => texture != null ? texture : s_WhiteTexture;
        static readonly int[] Layers = { -1, 0, 1, -1, 1, 0, -1, 1 };
        const float Tilt = -Mathf.PI / 7, Tau = Mathf.PI * 2;
        static readonly Color Mint = new Color(145f / 255, 234f / 255, 223f / 255);

        Vector3 Orbit(float a, float layer)
        {
            float x = 201 * Mathf.Cos(a), y = 78 * Mathf.Sin(a);
            // Preview uses screen y down; Unity UI y up.
            return new Vector3(x * Mathf.Cos(Tilt) - y * Mathf.Sin(Tilt),
                -(x * Mathf.Sin(Tilt) + y * Mathf.Cos(Tilt) + layer * 59), Mathf.Sin(a));
        }
        Vector2 Scale(Vector2 p) => new Vector2(p.x * motion.Size.x / 254f, p.y * motion.Size.y / 412f);
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (motion == null || motion.WindOpacity < .001f) return;
            float spin = motion.Spin, alpha = motion.WindOpacity;
            if (part == Part.Wind)
            {
                for (int k = 0; k < 3; k++)
                {
                    float a = spin - k * 1.8f + .75f;
                    Ribbon(vh, a, 1.45f, k - 1, alpha * .23f, 17);
                    Ribbon(vh, a - .1f, 1.12f, k - 1 + .08f, alpha * .45f, 2);
                }
                for (int i = 0; i < AugmentSelectionMotion.NeedleCount; i++)
                {
                    float phase = spin - i * Tau / 8, a = phase - .22f * Mathf.Sin(2 * phase);
                    for (int j = 0; j < 3; j++) Ribbon(vh, a, .62f * (1 - j * .21f), Layers[i], alpha * .20f, 5 - j * 1.4f);
                }
                return;
            }
            for (int i = 0; i < AugmentSelectionMotion.NeedleCount; i++)
            {
                float phase = spin - i * Tau / 8, a = phase - .22f * Mathf.Sin(2 * phase);
                var p = Orbit(a, Layers[i]); if ((p.z >= 0) != front) continue;
                float tx = -Mathf.Sin(a), ty = 78f / 201 * Mathf.Cos(a), depth = (p.z + 1) * .5f;
                var tangent = new Vector2(tx * Mathf.Cos(Tilt) - ty * Mathf.Sin(Tilt), -(tx * Mathf.Sin(Tilt) + ty * Mathf.Cos(Tilt)));
                float len = 88 * tangent.magnitude * (.92f + .16f * depth), thick = 11 * (.90f + .20f * depth);
                tangent.Normalize(); var side = new Vector2(-tangent.y, tangent.x);
                Vector2 pos = p;
                Quad(vh, pos - tangent * len / 2 - side * thick / 2, pos - tangent * len / 2 + side * thick / 2,
                    pos + tangent * len / 2 + side * thick / 2, pos + tangent * len / 2 - side * thick / 2,
                    new Color(1, 1, 1, alpha * (.64f + .36f * AugmentSelectionMotion.Smooth(depth))), true);
            }
        }
        void Ribbon(VertexHelper vh, float a, float length, float layer, float alpha, float width)
        {
            const int segments = 50;
            for (int k = 0; k < segments; k++)
            {
                float u = k / (float)segments, v = (k + 1f) / segments;
                var p = Orbit(a - length + length * u, layer); var q = Orbit(a - length + length * v, layer);
                if ((p.z >= 0) != front) continue;
                var d = ((Vector2)(q - p)).normalized; var normal = new Vector2(-d.y, d.x);
                float w0 = width * Mathf.Sin(Mathf.PI * u) * .5f, w1 = width * Mathf.Sin(Mathf.PI * v) * .5f;
                var c = Mint; c.a = alpha * (.40f + .60f * AugmentSelectionMotion.Smooth((p.z + 1) * .5f));
                Quad(vh, (Vector2)p + normal * w0, (Vector2)q + normal * w1,
                    (Vector2)q - normal * w1, (Vector2)p - normal * w0, c, false);
            }
        }
        void Quad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, bool uv)
        {
            int n = vh.currentVertCount;
            vh.AddVert(Scale(a), color, Vector2.zero); vh.AddVert(Scale(b), color, uv ? Vector2.up : Vector2.zero);
            vh.AddVert(Scale(c), color, uv ? Vector2.one : Vector2.zero); vh.AddVert(Scale(d), color, uv ? Vector2.right : Vector2.zero);
            vh.AddTriangle(n, n + 1, n + 2); vh.AddTriangle(n, n + 2, n + 3);
        }
    }
}

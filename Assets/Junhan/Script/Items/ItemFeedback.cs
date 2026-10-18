using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Vampire
{
    public enum ItemFeedbackKind { PressureMark, PressureBurst, FireZone, Spirit, Steam, Revival, Echo }

    // Presentation only: exhausting the budget must never cancel a gameplay effect.
    public sealed class ItemFeedback : MonoBehaviour
    {
        public const int Capacity = 24;
        readonly List<ItemFeedbackVisual> pool = new List<ItemFeedbackVisual>();
        Material material;
        Character character;
        float nextCaption;
        public int CreatedCount => pool.Count;
        public static ItemFeedback For(Component owner) => owner.GetComponent<ItemFeedback>() ?? owner.gameObject.AddComponent<ItemFeedback>();
        void Awake() { character = GetComponent<Character>(); }
        void LateUpdate() { if (character != null && !character.IsAlive) OnDisable(); }

        public ItemFeedbackVisual Show(ItemFeedbackKind kind, Vector3 position, float duration, float radius = 1,
            Transform follow = null, int stacks = 0)
        {
            if (material == null) material = new Material(Shader.Find("Sprites/Default"));
            ItemFeedbackVisual visual = null;
            int active = 0;
            foreach (var candidate in pool)
            {
                if (candidate == null) continue;
                if (candidate.gameObject.activeSelf) active++;
                else if (visual == null) visual = candidate;
            }
            // Reserve one slot so a rare, important revival cannot be hidden by hit spam.
            int limit = kind == ItemFeedbackKind.Revival ? Capacity : Capacity - 1;
            if (active >= limit) return null;
            if (visual == null && pool.Count < limit)
            {
                visual = new GameObject("Item feedback").AddComponent<ItemFeedbackVisual>();
                visual.Initialize(material);
                pool.Add(visual);
            }
            if (visual == null) return null;
            string caption = kind == ItemFeedbackKind.PressureBurst ? "점혈!" : kind == ItemFeedbackKind.Steam ? "증기 폭발!" : kind == ItemFeedbackKind.Revival ? "다시 피어나다" : "";
            if (caption.Length > 0 && (Time.time >= nextCaption || kind == ItemFeedbackKind.Revival)) nextCaption = Time.time + .45f;
            else caption = "";
            visual.Play(kind, position, duration, radius, follow, stacks, caption);
            return visual;
        }

        void OnDisable()
        {
            foreach (var visual in pool) if (visual != null) visual.Release();
        }
        void OnDestroy()
        {
            foreach (var visual in pool) if (visual != null) Dispose(visual.gameObject);
            if (material != null) Dispose(material);
        }
        internal static void Dispose(Object value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); }
    }

    // Small procedural ink shapes reuse a mesh and lists; no item-card sprites or collisions.
    public sealed class ItemFeedbackVisual : MonoBehaviour
    {
        Mesh mesh;
        MeshRenderer drawing;
        TextMeshPro caption;
        Material captionMaterial;
        MaterialPropertyBlock tint;
        readonly List<Vector3> vertices = new List<Vector3>(512);
        readonly List<Color> colors = new List<Color>(512);
        readonly List<int> triangles = new List<int>(1536);
        Transform follow;
        bool following;
        float age, duration, radius;
        int stacks;
        public ItemFeedbackKind Kind { get; private set; }
        public int Lease { get; private set; }
        static readonly Color Ink = new Color(.13f, .10f, .18f, .95f);
        static readonly Color Gold = new Color(1f, .78f, .23f, .95f);
        static readonly Color Red = new Color(.96f, .32f, .22f, .9f);
        static readonly Color Blue = new Color(.25f, .82f, 1f, .9f);
        static readonly Color Paper = new Color(1, .96f, .81f, .9f);

        public void Initialize(Material material)
        {
            mesh = new Mesh { name = "Item feedback geometry" }; mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            drawing = gameObject.AddComponent<MeshRenderer>(); drawing.sharedMaterial = material;
            drawing.sortingLayerName = "Monster Full"; drawing.sortingOrder = 35;
            tint = new MaterialPropertyBlock();
            gameObject.SetActive(false);
        }
        public void Play(ItemFeedbackKind kind, Vector3 position, float seconds, float size, Transform target, int count, string text)
        {
            Lease++;
            Kind = kind; duration = Mathf.Max(.05f, seconds); radius = Mathf.Max(.1f, size);
            age = 0; follow = target; following = target != null; stacks = count;
            transform.SetPositionAndRotation(position, Quaternion.identity); transform.localScale = Vector3.one;
            drawing.sortingOrder = kind == ItemFeedbackKind.FireZone ? -2 : 35;
            Build();
            // TMP initializes its renderer in Awake; the pooled parent must be active first.
            gameObject.SetActive(true);
            if (text.Length > 0 && caption == null)
            {
                var go = new GameObject("Proc caption"); go.transform.SetParent(transform, false);
                caption = go.AddComponent<TextMeshPro>();
                var config = Resources.Load<ApothecaryUIConfig>("ApothecaryUIConfig");
                caption.font = config != null && config.font != null ? config.font : TMP_Settings.defaultFontAsset;
                caption.fontSize = 3.6f; caption.alignment = TextAlignmentOptions.Center;
                if(caption.font != null)
                {
                    captionMaterial = new Material(caption.font.material);
                    captionMaterial.SetColor("_OutlineColor",Ink);captionMaterial.SetFloat("_OutlineWidth",.16f);
                    caption.fontSharedMaterial = captionMaterial;
                }
                caption.enableWordWrapping = false; caption.rectTransform.sizeDelta = new Vector2(4, .8f);
                caption.GetComponent<MeshRenderer>().sortingLayerName = "Monster Full";
                caption.GetComponent<MeshRenderer>().sortingOrder = 45;
            }
            if (caption != null) { caption.text = text; caption.gameObject.SetActive(text.Length > 0); }
            gameObject.SetActive(true); Draw(0);
        }
        public void SetStacks(int count)
        {
            if (stacks == count) return;
            stacks = count; Build();
        }
        public void Release() { follow = null; following = false; gameObject.SetActive(false); }
        public void Release(int lease) { if (Lease == lease) Release(); }
        void Update()
        {
            if (Time.timeScale <= 0) return;
            if (following && (follow == null || !follow.gameObject.activeInHierarchy)) { Release(); return; }
            if (following) transform.position = follow.position;
            age += Time.deltaTime;
            if (age >= duration) { Release(); return; }
            Draw(age / duration);
        }
        void Draw(float t)
        {
            bool calm = GamePreferences.Current.reducedMotion;
            bool persistent = Kind == ItemFeedbackKind.FireZone || Kind == ItemFeedbackKind.Spirit || Kind == ItemFeedbackKind.PressureMark;
            float alpha = persistent ? Mathf.Clamp01((1 - t) * 8) : 1 - t;
            tint.SetColor("_Color", new Color(1, 1, 1, alpha * GamePreferences.Current.augmentVfxOpacity));
            drawing.SetPropertyBlock(tint);
            float scale = 1;
            if (!calm)
            {
                if (Kind == ItemFeedbackKind.Steam || Kind == ItemFeedbackKind.Echo) scale = Mathf.Lerp(.35f, 1, Mathf.Sqrt(t));
                else if (Kind == ItemFeedbackKind.Revival) scale = Mathf.Lerp(.3f, 1, Mathf.Min(1, t * 3));
                else if (Kind == ItemFeedbackKind.PressureBurst) scale = 1 + .12f * Mathf.Sin(t * Mathf.PI);
                else if (Kind == ItemFeedbackKind.Spirit) scale = 1 + .05f * Mathf.Sin(age * 14);
            }
            transform.localScale = Vector3.one * (radius * scale);
            if (caption != null && caption.gameObject.activeSelf)
            {
                caption.transform.localScale = Vector3.one / (radius * scale);
                caption.transform.localPosition = new Vector3(0, (radius + .35f + (calm ? 0 : t * .25f)) / (radius * scale), 0);
                caption.color = new Color(1, .9f, .45f, alpha * GamePreferences.Current.augmentVfxOpacity);
            }
        }
        void Build()
        {
            vertices.Clear(); colors.Clear(); triangles.Clear();
            switch (Kind)
            {
                case ItemFeedbackKind.PressureMark:
                    for (int i = 0; i < 4; i++) Disc(new Vector2((i - 1.5f) * .26f, .65f), .09f, i < stacks ? Gold : Ink);
                    break;
                case ItemFeedbackKind.PressureBurst:
                    Ring(.48f, .075f, Gold);
                    for (int i = 0; i < 8; i++) { var d = Polar(i * Mathf.PI / 4); Stroke(d * .6f, d, .055f, i % 2 == 0 ? Red : Paper); }
                    break;
                case ItemFeedbackKind.FireZone:
                    Disc(Vector2.zero, 1, new Color(.8f, .18f, .08f, .16f)); Ring(.98f, .025f, Red);
                    for (int i = 0; i < 7; i++)
                    {
                        Vector2 c = i == 0 ? Vector2.zero : Polar(i * Mathf.PI / 3) * .58f;
                        Flame(c, 1, Red); Flame(c + new Vector2(.01f,-.03f), .53f, Gold);
                    }
                    break;
                case ItemFeedbackKind.Spirit:
                    Disc(new Vector2(0,.05f),.4f,new Color(.25f,.82f,1,.17f));
                    Disc(new Vector2(0, .12f), .29f, Paper);
                    Polygon(new Vector2(0,-.07f),SpiritOutline,Paper);
                    Disc(new Vector2(-.1f, .15f), .045f, Ink); Disc(new Vector2(.1f, .15f), .045f, Ink);
                    Stroke(new Vector2(0,.23f),new Vector2(0,.38f),.045f,Red);
                    Stroke(new Vector2(-.06f,.30f),new Vector2(.06f,.30f),.025f,Red);
                    Disc(new Vector2(0,-.02f),.045f,Ink);
                    break;
                case ItemFeedbackKind.Steam:
                    Ring(.86f, .05f, Paper);
                    for (int i = 0; i < 8; i++)
                    {
                        var c = Polar(i * Mathf.PI / 4) * .52f;
                        Disc(c, .27f, i % 2 == 0 ? new Color(1, .45f, .28f, .65f) : new Color(.4f, .85f, 1, .65f));
                        Disc(c+Vector2.up*.12f,.18f,new Color(.94f,.96f,1,.65f));
                        Stroke(c+Vector2.up*.24f,c+new Vector2(.05f,.4f),.025f,Paper);
                    }
                    break;
                case ItemFeedbackKind.Revival:
                    for (int i = 0; i < 8; i++)
                    {
                        Petal(i * Mathf.PI / 4, i%2==0 ? new Color(1,.55f,.7f,.85f) : Paper);
                    }
                    Disc(Vector2.zero, .2f, Gold); Ring(1.08f, .025f, Gold);
                    break;
                case ItemFeedbackKind.Echo: Ring(.95f, .04f, Blue); Ring(.7f, .025f, Paper); break;
            }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }
        static Vector2 Polar(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        void Polygon(Vector2 center, Vector2[] points, Color color)
        { for(int i=0;i<points.Length;i++)Triangle(center,points[i],points[(i+1)%points.Length],color); }
        static readonly Vector2[] FlameOutline = {new Vector2(-.14f,-.18f),new Vector2(-.22f,-.02f),new Vector2(-.13f,.19f),new Vector2(-.08f,.06f),new Vector2(.05f,.34f),new Vector2(.06f,.13f),new Vector2(.18f,-.01f),new Vector2(.15f,-.17f),new Vector2(0,-.23f)};
        static readonly Vector2[] SpiritOutline = {new Vector2(-.28f,.12f),new Vector2(-.26f,-.16f),new Vector2(-.17f,-.34f),new Vector2(-.06f,-.23f),new Vector2(.03f,-.35f),new Vector2(.13f,-.23f),new Vector2(.24f,-.27f),new Vector2(.28f,.12f)};
        void Flame(Vector2 center,float scale,Color color)
        { for(int i=0;i<FlameOutline.Length;i++)Triangle(center+new Vector2(0,-.05f)*scale,center+FlameOutline[i]*scale,center+FlameOutline[(i+1)%FlameOutline.Length]*scale,color); }
        void Petal(float angle,Color color)
        {
            Vector2 d=Polar(angle),n=new Vector2(-d.y,d.x);
            for(int i=0;i<16;i++)
            {
                float a=i*Mathf.PI/8,b=(i+1)*Mathf.PI/8;
                Vector2 p=d*(.48f+.48f*Mathf.Cos(a))+n*(.21f*Mathf.Sin(a));
                Vector2 q=d*(.48f+.48f*Mathf.Cos(b))+n*(.21f*Mathf.Sin(b));
                Triangle(d*.48f,p,q,color);
            }
        }
        void Triangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            int start = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c);
            colors.Add(color); colors.Add(color); colors.Add(color);
            triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        }
        void Disc(Vector2 center, float r, Color color)
        { for (int i = 0; i < 24; i++) Triangle(center, center + Polar(i * Mathf.PI / 12) * r, center + Polar((i + 1) * Mathf.PI / 12) * r, color); }
        void Ring(float r, float width, Color color)
        { for (int i = 0; i < 40; i++) Stroke(Polar(i * Mathf.PI / 20) * r, Polar((i + 1) * Mathf.PI / 20) * r, width, color); }
        void Stroke(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = (b - a).normalized; Vector2 n = new Vector2(-d.y, d.x) * width * .5f;
            Triangle(a - n, a + n, b + n, color); Triangle(a - n, b + n, b - n, color);
        }
        void OnDestroy()
        {
            if (mesh != null) ItemFeedback.Dispose(mesh);
            if (captionMaterial != null) ItemFeedback.Dispose(captionMaterial);
        }
    }
}

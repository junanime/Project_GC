using System.Collections.Generic;
using UnityEngine;

namespace Vampire
{
    // Routes approved phoenix effects to painted radiance atlases, retaining the ink
    // geometry for OrganCompression. No collisions or damage callbacks live here.
    // A pooled SyringeAugmentVfx still owns lifetime, targeting, size and sorting.
    [DefaultExecutionOrder(1000)]
    public sealed class PhoenixNobleVfx : MonoBehaviour
    {
        SpriteRenderer carrier;
        MeshRenderer drawing;
        Transform ink;
        Mesh mesh;
        MaterialPropertyBlock properties;
        string theme;
        float age, duration;
        bool looping, featherOnly;
        PhoenixRadianceVisual radiance;
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> triangles = new List<int>();
        static readonly Color Ink = new Color(.12f, .15f, .17f, .9f);
        static readonly Color Wash = new Color(.37f, .4f, .4f, .6f);
        static readonly Color Paper = new Color(.96f, .92f, .8f, .82f);
        static readonly string[] Prefixes = { "LifeBurn", "Hedgehog", "HeavySnipe", "CursorControl", "NeuralBlock", "OrganCompression", "GastricPeristalsisWave", "MucosalFortress", "HungrySpirit", "NeedleShotgun" };
        public string Theme => theme;
        public MeshRenderer Drawing => radiance != null ? radiance.FirstRenderer : drawing;

        public static bool Supports(string key)
        {
            if (key == "OrbitFeather") return true;
            if (key.StartsWith("Clone")) return key == "CloneCulture" || key == "CloneSpawn" || key == "CloneDisappear";
            foreach (var prefix in Prefixes)
                if (key.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
            return false;
        }

        public static PhoenixNobleVfx Attach(SpriteRenderer source, string key, bool loop, float lifetime)
        {
            if (source == null || !Supports(key)) return null;
            var effect = source.GetComponent<PhoenixNobleVfx>();
            if (effect == null) effect = source.gameObject.AddComponent<PhoenixNobleVfx>();
            effect.carrier = source;
            effect.age = 0;
            effect.duration = Mathf.Max(.05f, lifetime);
            effect.looping = loop;
            effect.featherOnly = key == "OrbitFeather";
            if (PhoenixRadianceVisual.Supports(key))
            {
                effect.theme = key;
                if (effect.drawing != null) effect.drawing.enabled = false;
                effect.radiance = source.GetComponent<PhoenixRadianceVisual>() ?? source.gameObject.AddComponent<PhoenixRadianceVisual>();
                effect.radiance.Bind(source,key,loop,lifetime);
                source.forceRenderingOff = true;
                effect.enabled = true;
                return effect;
            }
            if (effect.theme != key || effect.mesh == null) { effect.theme = key; effect.Build(); }
            // Keep sprite bounds/animation available for existing sizing logic, without drawing old art.
            source.forceRenderingOff = true;
            effect.enabled = true;
            effect.Sync();
            return effect;
        }

        void Build()
        {
            if (drawing == null)
            {
                ink = new GameObject("Phoenix ink geometry").transform;
                ink.SetParent(transform, false);
                drawing = ink.gameObject.AddComponent<MeshRenderer>();
                mesh = new Mesh { name = "Phoenix " + theme };
                ink.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                drawing.sharedMaterial = carrier.sharedMaterial;
                properties = new MaterialPropertyBlock();
            }
            vertices.Clear(); colors.Clear(); triangles.Clear();
            Color accent = new Color(.73f, .23f, .13f, .95f);
            if (theme.StartsWith("LifeBurn"))
            {
                Wings(.05f); Feather(new Vector2(0, -.16f), new Vector2(0, .22f), .075f, Ink);
                Feather(new Vector2(0, -.05f), new Vector2(0, .085f), .047f, accent);
            }
            else if (theme.StartsWith("Clone"))
            {
                Wings(.035f); Ring(.34f, .011f, Wash, -.5f, 4.4f);
                Feather(new Vector2(0, -.1f), new Vector2(.02f, .17f), .047f, Paper);
            }
            else if (theme == "OrbitFeather")
                Feather(new Vector2(0, -.43f), new Vector2(0, .43f), .18f, Ink);
            else if (theme.StartsWith("Hedgehog"))
            {
                for (int i = 0; i < 8; i++) { Vector2 p = Polar(i * Mathf.PI / 4) * .36f; Feather(p - Vector2.up * .085f, p + Vector2.up * .085f, .032f, i % 2 == 0 ? Ink : Wash); }
                Ring(.35f, .008f, Paper, 0, Mathf.PI * 2);
            }
            else if (theme.StartsWith("HeavySnipe") || theme.StartsWith("CursorControlGlow"))
            {
                Feather(new Vector2(-.42f, 0), new Vector2(.45f, 0), .11f, Ink);
                Stroke(new Vector2(.25f, 0), new Vector2(.46f, 0), .014f, accent);
                for (int i = -1; i <= 1; i += 2) Feather(new Vector2(-.35f, i * .15f), new Vector2(.05f, i * .035f), .036f, Wash);
            }
            else if (theme == "CursorControl")
            {
                for (int i = 0; i < 64; i++)
                {
                    float t = i * Mathf.PI / 32, u = (i + 1) * Mathf.PI / 32;
                    Stroke(new Vector2(Mathf.Sin(t) * .44f, Mathf.Sin(t * 2) * .17f), new Vector2(Mathf.Sin(u) * .44f, Mathf.Sin(u * 2) * .17f), .008f, Wash);
                }
                Feather(new Vector2(.25f, -.05f), new Vector2(.41f, .1f), .035f, Paper);
            }
            else if (theme.StartsWith("NeuralBlock"))
            {
                Ring(.3f, .013f, Paper, 0, Mathf.PI * 2);
                for (int i = -1; i <= 1; i++) Feather(new Vector2(i * .13f, -.18f), new Vector2(i * .13f, .19f), .05f, Ink);
                Stroke(new Vector2(-.25f, 0), new Vector2(.25f, 0), .023f, new Color(.52f, .78f, .8f, .9f));
            }
            else if (theme.StartsWith("OrganCompression"))
            {
                for (int i = 0; i < 3; i++)
                    for (int j = 0; j < 25; j++)
                    { float t = j / 25f, u = (j + 1) / 25f, a = i * Mathf.PI * 2 / 3;
                      Stroke(Polar(a + t * 4) * (.43f - t * .35f), Polar(a + u * 4) * (.43f - u * .35f), .014f, i == 0 ? Ink : Wash); }
                Wings(.025f);
            }
            else if (theme.StartsWith("GastricPeristalsisWave"))
            {
                Ring(.44f, .022f, Paper, 0, Mathf.PI * 2); Ring(.37f, .014f, Wash, 0, Mathf.PI * 2);
                RadialFeathers(8, .34f, .46f);
            }
            else if (theme.StartsWith("MucosalFortress"))
            {
                Ring(.42f, .018f, Paper, 0, Mathf.PI * 2);
                Wings(.04f);
                Feather(new Vector2(0, -.18f), new Vector2(0, .03f), .055f, new Color(.52f, .77f, .66f, .8f));
            }
            else if (theme.StartsWith("HungrySpirit"))
            {
                Wings(.045f);
                for (int i = -1; i <= 1; i++) Feather(new Vector2(i * .09f, -.39f), new Vector2(i * .035f, -.09f), .036f, Wash);
                Feather(new Vector2(0, .08f), new Vector2(0, .22f), .033f, accent);
            }
            else if (theme.StartsWith("NeedleShotgun"))
            {
                for (int i = -2; i <= 2; i++) { Vector2 p = Polar(i * .25f); Feather(p * .05f, p * .44f, .045f, i == 0 ? Paper : Ink); }
            }
            else { RadialFeathers(5, .12f, .4f); }
            mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
        }

        void Wings(float width)
        {
            for (int s = -1; s <= 1; s += 2)
                for (int i = 0; i < 5; i++)
                    Feather(new Vector2(s * (.025f + i * .024f), -.04f - i * .018f),
                        new Vector2(s * (.2f + i * .046f), .31f - i * .094f), width, i % 2 == 0 ? Ink : Wash);
        }
        void RadialFeathers(int count, float from, float to)
        { for (int i = 0; i < count; i++) { Vector2 p = Polar(i * Mathf.PI * 2 / count); Feather(p * from, p * to, .033f, i % 2 == 0 ? Ink : Paper); } }
        static Vector2 Polar(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        void Feather(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a, side = new Vector2(-d.y, d.x).normalized;
            // Thin warm outline keeps dark ink readable on the stomach-floor palette.
            Blade(a, b, side, width * 1.1f, Paper);
            Blade(a + d * .02f, b - d * .01f, side, width, color);
            Stroke(a, b, width * .08f, Paper);
            for (int i = 2; i < 7; i++)
            {
                float t = i / 8f, w = Mathf.Sin(t * Mathf.PI) * width * .8f;
                Vector2 p = a + d * t;
                Stroke(p - d * .08f, p + side * w, width * .035f, Paper);
                Stroke(p - d * .08f, p - side * w, width * .035f, Paper);
            }
        }
        void Blade(Vector2 a, Vector2 b, Vector2 side, float width, Color color)
        {
            // Uneven tapered vanes, rather than a diamond silhouette. Small notches suggest barbs.
            Vector2 previousLeft = a, previousRight = a;
            for (int i = 1; i <= 12; i++)
            {
                float t = i / 12f;
                Vector2 center = Vector2.Lerp(a, b, t);
                float taper = Mathf.Pow(Mathf.Sin(t * Mathf.PI), .8f) * (1.2f - .45f * t);
                float notch = i % 3 == 0 ? .91f : 1;
                Vector2 left = center + side * width * taper * notch;
                Vector2 right = center - side * width * taper * .72f;
                Tri(previousLeft, left, right, color); Tri(previousLeft, right, previousRight, color);
                previousLeft = left; previousRight = right;
            }
        }
        void Ring(float radius, float width, Color color, float start, float end)
        { for (int i = 0; i < 48; i++) Stroke(Polar(Mathf.Lerp(start, end, i / 48f)) * radius, Polar(Mathf.Lerp(start, end, (i + 1) / 48f)) * radius, width, color); }
        void Stroke(Vector2 a, Vector2 b, float width, Color color)
        { Vector2 d = b - a, n = new Vector2(-d.y, d.x).normalized * width / 2; Tri(a - n, a + n, b + n, color); Tri(a - n, b + n, b - n, color); }
        void Tri(Vector2 a, Vector2 b, Vector2 c, Color color)
        { int k = vertices.Count; vertices.Add(a); vertices.Add(b); vertices.Add(c); colors.Add(color); colors.Add(color); colors.Add(color); triangles.Add(k); triangles.Add(k + 1); triangles.Add(k + 2); }

        void LateUpdate() { age += Time.deltaTime; Sync(); }
        void Sync()
        {
            if (radiance != null) return;
            if (carrier == null || drawing == null) return;
            drawing.enabled = carrier.enabled && carrier.gameObject.activeInHierarchy;
            drawing.sortingLayerID = carrier.sortingLayerID; drawing.sortingOrder = carrier.sortingOrder;
            var size = carrier.sprite != null ? carrier.sprite.bounds.size : Vector3.one;
            float phase = Mathf.Clamp01(age / duration);
            bool reduced = GamePreferences.Current.reducedMotion;
            float pulse = featherOnly || reduced ? 1 : looping ? 1 + Mathf.Sin(age * 3) * .025f : Mathf.Lerp(.86f, 1.06f, phase);
            if (featherOnly) size = Vector3.one * Mathf.Max(size.x, size.y);
            ink.localScale = new Vector3(size.x * pulse, size.y * pulse, 1);
            float turn = !reduced && theme.StartsWith("OrganCompression") ? -age * 35 : 0;
            ink.localRotation = Quaternion.Euler(0, 0, turn);
            properties.SetTexture("_MainTex", Texture2D.whiteTexture);
            float fade = looping ? 1 : 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.55f, 1, phase));
            properties.SetColor("_Color", new Color(1, 1, 1, carrier.color.a * fade));
            drawing.SetPropertyBlock(properties);
        }
        void OnDestroy() { if (mesh != null) Destroy(mesh); }
    }
}
